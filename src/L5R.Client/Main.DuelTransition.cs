using Godot;
using L5R.Core;
using System.Text.Json;
namespace L5R.Client;

public partial class Main
{
    private bool _transitionReview,_transitionPreparing,_transitionStarted,_transitionReady;
    private double _transitionClock;
    private CanvasLayer? _transitionLayer;
    private SubViewport? _transitionRoom;
    private Control? _transitionTools;
    private DuelScreen? _transitionDuel;
    private TableDuelTransition? _tableDuelTransition;
    private readonly HashSet<string> _transitionCaptures=[];
    private int _transitionFilmFrame;
    private long _transitionVersion;
    private double _transitionOpponentClock;
    private bool _transitionPreserved,_transitionInputChecked,_transitionResized;
    private string _transitionReplay="";
    private readonly List<double> _transitionFrameTimes=[];
    private ulong _transitionStamp;
    private void StartTransitionReview()
    {
        _transitionReview=true;_autoOpponent=false;
        _reducedMotion=_preferences.ReducedMotion||OS.GetCmdlineUserArgs().Contains("--transition-reduced");
        _table.ReducedMotion=_reducedMotion;_timeline.ReducedMotion=_reducedMotion;
        GD.Print("TRANSITION_PREP: seeded encounter");
        _session.StartDuelDemo("steel",205,0);
        GD.Print("TRANSITION_PREP: entering table");
        EnterTable();_turnBloom.Cancel();
        GD.Print("TRANSITION_PREP: table ready");
        _transitionLayer=new CanvasLayer{Layer=100};AddChild(_transitionLayer);
        BuildTransitionTools(false);
    }
    private void BuildTransitionTools(bool ready)
    {
        if(IsInstanceValid(_transitionTools)){_transitionTools!.GetParent().RemoveChild(_transitionTools);_transitionTools.QueueFree();}
        _transitionTools=new Control{MouseFilter=Control.MouseFilterEnum.Ignore};_transitionLayer!.AddChild(_transitionTools);
        _transitionTools.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var size=GetViewport().GetVisibleRect().Size;
        if(!ready)
        {
            var button=SidebarSkin.Button("Preview duel transition",()=>_ = BeginTableTransition(),true);
            button.Position=new Vector2(size.X/2-145,120);button.Size=new Vector2(290,50);_transitionTools.AddChild(button);
        }
        else
        {
            var button=DuelUi.Button("Replay transition",()=>GetTree().ReloadCurrentScene(),fontSize:19);
            button.Position=new Vector2(20,20);button.Size=new Vector2(200,42);_transitionTools.AddChild(button);
            var note=DuelUi.Text("Transition preview · prepared encounter",16,Palette.PaperDim);note.Position=new Vector2(20,68);_transitionTools.AddChild(note);
        }
    }
    private async Task Drawn()
    {
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        // macOS may suppress drawable callbacks for an occluded review window.
        // Finish offscreen preparation explicitly instead of waiting indefinitely.
        RenderingServer.ForceDraw(false);RenderingServer.ForceSync();
    }
    private async Task BeginTableTransition()
    {
        if(_transitionPreparing||_transitionStarted)return;
        _transitionPreparing=true;
        try
        {
            _transitionTools!.Hide();_turnBloom.Cancel();ReleaseTableFocus();HideTableTooltip();
            // This is the sole game command of the entrance. Production activation must wait for Focusing.
            await _session.BeginDuelDemo();
            GD.Print("TRANSITION_PREP: command accepted");
            Render();
            GD.Print("TRANSITION_PREP: table rendered");
            foreach(var flight in _flights.Where(IsInstanceValid))flight.Finish();
            _flights.Clear();
            var snapshot=_session.Snapshot!;
            if(snapshot.View.PendingDuel?.Stage!="Focusing")throw new InvalidOperationException("Transition preview requires an accepted challenge with no unresolved reaction choice.");
            _transitionVersion=snapshot.View.Version;_transitionReplay=_session.ExportPracticeReplay()!;
            var record=snapshot.View.LastDuel!;
            var publicCards=record.Attachments.Concat(new[]{record.Challenger,record.Challenged}).ToArray();
            var ids=publicCards.Select(c=>c.Id).ToHashSet();
            var origins=_tiles.Where(t=>ids.Contains(t.Card.Id)).ToDictionary(t=>t.Card.Id,t=>t.FaceCornersOnViewport());
            if(publicCards.Any(c=>!origins.ContainsKey(c.Id)))throw new InvalidOperationException("Participant is not visible on the table.");
            GD.Print("TRANSITION_PREP: waiting for table draw");
            await Drawn();
            GD.Print("TRANSITION_PREP: public table frame");
            var full=GetViewport().GetTexture().GetImage();
            // A stable frozen cover hides viewport preparation. The live duel is warmed beneath it.
            var cover=new TextureRect{Texture=ImageTexture.CreateFromImage(full),Size=GetViewport().GetVisibleRect().Size,ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize};
            _transitionLayer!.AddChild(cover);
            var hidden=_root.GetChildren().OfType<CanvasItem>().Where(n=>n!=_tableViewport&&n.Visible).ToArray();
            foreach(var item in hidden)item.Hide();
            foreach(var tile in _tiles.Where(t=>ids.Contains(t.Card.Id)))tile.Hide();
            foreach(var rail in _statTokenLayer?.GetChildren().OfType<CanvasItem>()??[])rail.Hide();
            await Drawn();

            _transitionRoom=new SubViewport{Size=(Vector2I)GetViewport().GetVisibleRect().Size,OwnWorld3D=true,HandleInputLocally=false,RenderTargetUpdateMode=SubViewport.UpdateMode.Always};
            AddChild(_transitionRoom);_table.Reparent(_transitionRoom,false);_root.Reparent(_transitionRoom,false);
            _table.Show();_table.SetProcess(false);
            _transitionDuel=new DuelScreen{ReducedMotion=_reducedMotion,ExternalEntrance=true,InputLocked=true};
            var warmup=new SubViewport{Size=(Vector2I)GetViewport().GetVisibleRect().Size,HandleInputLocally=false,RenderTargetUpdateMode=SubViewport.UpdateMode.Always};
            AddChild(warmup);warmup.AddChild(_transitionDuel);
            _transitionDuel.SetGuidanceEnabled(false);_transitionDuel.Present(snapshot);_transitionDuel.SetEntranceCovered(!_reducedMotion);
            _transitionDuel.CommandRequested+=command=>_ = SubmitTransitionCommand(command);
            _transitionDuel.PresentationFinished+=()=>GetTree().ReloadCurrentScene();
            await Drawn();await Drawn();
            GD.Print("TRANSITION_PREP: destination warmed");
            var destination=warmup.GetTexture().GetImage();
            _transitionDuel.Reparent(_transitionLayer,false);_transitionLayer.MoveChild(_transitionDuel,0);warmup.QueueFree();
            var cards=publicCards.Select(c=>new TableDuelTransition.Card(c,origins[c.Id],_transitionDuel.EntranceQuad(c.Id))).ToArray();
            _tableDuelTransition=new TableDuelTransition{ReducedMotion=_reducedMotion};
            _transitionLayer.AddChild(_tableDuelTransition);
            GD.Print("TRANSITION_PREP: composing");
            _tableDuelTransition.Build(_transitionRoom.GetTexture(),full,destination,cards,GetViewport().GetVisibleRect().Size,record.Challenger.Definition!.Name,record.Challenged.Definition!.Name);
            var initialPosition=_tableViewport.Position;var initialSize=_tableViewport.Size;
            _tableDuelTransition.CameraProgress+=amount=>
            {
                // Keep the source units absent if a deferred table layout rebuilt their controls.
                foreach(var tile in _tiles.Where(t=>ids.Contains(t.Card.Id)))tile.Hide();
                _tableViewport.Position=initialPosition.Lerp(new Vector2((_layoutWidth-initialSize.X)/2,330),amount);
                _tableSurface.PlanePitch=Mathf.Lerp(TableSurface.Pitch,.40f,amount);
                _tableSurface.ResizePlane(_tableViewport.Size,_root.Scale.X);
                _world.Position=_tableSurface.CanvasSize/2-_cameraCenter*_cameraZoom;
                _table.SetPlaneCamera(_tableViewport.GetGlobalRect().GetCenter(),_root.Scale.X,_cameraCenter,_cameraZoom,new Vector2(WorldWidth,_worldHeight),FitZoom,_tableSurface.PlanePitch);
            };
            _tableDuelTransition.ControlsRevealed+=()=>_transitionDuel.RevealEntranceControls();
            _tableDuelTransition.Finished+=()=>
            {
                _transitionDuel.SetEntranceCovered(false);_transitionDuel.InputLocked=false;_transitionDuel.CompleteExternalEntrance(true);
                _transitionReady=true;_tableDuelTransition=null;
                _transitionRoom.RenderTargetUpdateMode=SubViewport.UpdateMode.Disabled;_tableSurface.Canvas.RenderTargetUpdateMode=SubViewport.UpdateMode.Disabled;
                if(_session.Snapshot!.View.Version!=_transitionVersion||_session.ExportPracticeReplay()!=_transitionReplay)throw new InvalidOperationException("Presentation altered the match.");
                _transitionPreserved=true;BuildTransitionTools(true);GD.Print("TRANSITION_CHECK: handoff preserves version/replay and exposes real legal choices");
            };
            await Drawn();
            GD.Print("TRANSITION_PREP: starting");
            cover.QueueFree();_transitionStarted=true;_transitionClock=0;_transitionPreparing=false;_tableDuelTransition.Start();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
    private async Task SubmitTransitionCommand(GameCommand command)
    {
        if(!_transitionReady||_transitionDuel is null)return;
        _transitionDuel.InputLocked=true;
        try{await _session.Submit(command);_transitionDuel.Present(_session.Snapshot!);}
        finally{_transitionDuel.InputLocked=false;_transitionDuel.SubmissionFinished();}
    }
    private void ProcessTransitionReview(double delta)
    {
        _turnBloom.Cancel();_transitionClock+=delta;
        var now=Time.GetTicksUsec();if(_transitionStarted&&!_transitionReady&&_transitionStamp>0)_transitionFrameTimes.Add((now-_transitionStamp)/1000d);_transitionStamp=now;
        bool auto=_capturePath is not null||OS.GetCmdlineUserArgs().Contains("--transition-auto");
        if(auto&&!_transitionStarted&&!_transitionPreparing&&_transitionClock>2){GD.Print("TRANSITION_PREP: starting request");_ = BeginTableTransition();}
        if(_transitionReady&&_transitionDuel is not null&&!_transitionDuel.PresentationBusy&&!_transitionDuel.HasPrivateInspection)
        {
            _transitionOpponentClock+=delta;
            if(_transitionOpponentClock>.85)
            {
                _transitionOpponentClock=0;
                if(_session.StepPracticeOpponent(L5R.AI.DuelOpponent.Choose))_transitionDuel.Present(_session.Snapshot!);
            }
        }
        if(_capturePath is null||!_transitionStarted)return;
        // Native captures must render even when macOS occludes the test window.
        RenderingServer.ForceDraw(false);RenderingServer.ForceSync();
        var film=OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--transition-film="))?[18..];
        if(film is not null)
        {
            System.IO.Directory.CreateDirectory(film);
            GetViewport().GetTexture().GetImage().SaveJpg(System.IO.Path.Combine(film,$"{_transitionFilmFrame++:D5}.jpg"),.96f);
        }

        var t=_tableDuelTransition?.Elapsed??TableDuelTransition.Duration;
        if(OS.GetCmdlineUserArgs().Contains("--transition-skip")&&t>.95&&_tableDuelTransition is not null)
        {
            Input.ParseInputEvent(new InputEventKey{Pressed=true,Keycode=Key.Space});Input.ParseInputEvent(new InputEventKey{Pressed=false,Keycode=Key.Space});
        }
        if(OS.GetCmdlineUserArgs().Contains("--transition-resize")&&t>1.1&&!_transitionResized)
        {_transitionResized=true;GetWindow().Size=new Vector2I(1440,900);}
        foreach(var (name,at) in new[]{("1-challenge",.25),("2-lift",1.55),("3-ink",2.65),("4-ready",6.0)})
            if(!OS.GetCmdlineUserArgs().Contains("--transition-benchmark")&&(_transitionReady?_transitionClock:t)>=at&&(name!="4-ready"||_transitionReady&&!_transitionDuel!.PresentationBusy)&&_transitionCaptures.Add(name))
            {
                var path=_capturePath+"-"+name+".png";System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
                GetViewport().GetTexture().GetImage().SavePng(path);
            }
        if(!_transitionReady||_transitionClock<8.2||_transitionDuel!.PresentationBusy)return;
        if(!_transitionInputChecked)
        {
            // Native click on the first legal hand card; selection must not be a command.
            var card=_session.Snapshot!.View.Players[0].Hand.First();
            var before=_session.Snapshot.View.Version;
            var point=_transitionDuel.CardClickPoint(card.Id);
            GetViewport().WarpMouse(point);
            Input.ParseInputEvent(new InputEventMouseMotion{Position=point,GlobalPosition=point});
            Input.ParseInputEvent(new InputEventMouseButton{Position=point,GlobalPosition=point,ButtonIndex=MouseButton.Left,Pressed=true});
            Input.ParseInputEvent(new InputEventMouseButton{Position=point,GlobalPosition=point,ButtonIndex=MouseButton.Left,Pressed=false});
            Input.FlushBufferedEvents();
            if(_transitionDuel.SelectedInspectionId!=card.Id||_session.Snapshot.View.Version!=before)throw new InvalidOperationException("Handoff did not preserve native card selection.");
            _transitionInputChecked=true;GD.Print("TRANSITION_CHECK: native hand selection works after handoff without extra command");
            return;
        }
        var frames=_transitionFrameTimes.Order().ToArray();
        var result=new{Passed=true,ReducedMotion=_reducedMotion,Skipped=OS.GetCmdlineUserArgs().Contains("--transition-skip"),Version=_transitionVersion,UnchangedReplayAtHandoff=_transitionPreserved,NativeInput=_transitionInputChecked,
            Resized=_transitionResized,DecisionPrompt=_transitionDuel.EssentialStatus,Resolution=GetViewport().GetVisibleRect().Size.ToString(),Frames=frames.Length,FrameIntervalP95Ms=frames.Length>0?frames[(int)(frames.Length*.95)]:0,TimingUse="Diagnostic only; forced rendering and image writes affect frame intervals",ImageCapture=!OS.GetCmdlineUserArgs().Contains("--transition-benchmark")};
        System.IO.File.WriteAllText(_capturePath+".json",JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
        GD.Print("TRANSITION_CHECK: completed",JsonSerializer.Serialize(result));GetTree().Quit();
    }
}
