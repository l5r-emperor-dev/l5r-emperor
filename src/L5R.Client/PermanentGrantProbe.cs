using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>Explicit, read-only view fixtures. Never registers unsupported cards with the engine.</summary>
public partial class PermanentGrantProbe : Control
{
    private readonly List<CardTile> _cards=[];
    private string _output="/tmp/permanent-grants.png";
    private bool _comparison,_small,_perspective,_interactive,_long;
    private string? _lastClicked;
    private int _stage,_clicks;private double _clock;
    private TableSurface? _surface;private Control? _host;
    private CardView _hero=null!;
    private Control? _detail;
    public override void _Ready()
    {
        foreach(var arg in OS.GetCmdlineUserArgs())
        { if(arg.StartsWith("--output="))_output=arg[9..];_comparison|=arg=="--comparison";_small|=arg=="--small";_perspective|=arg=="--perspective";_interactive|=arg=="--interactive";_long|=arg=="--long"; }
        Theme=SidebarSkin.Theme();GetWindow().Title="Permanent grants · native review";GetWindow().GrabFocus();
        var size=GetViewportRect().Size;
        AddChild(new TextureRect { Texture=GD.Load<Texture2D>("res://Art/navy-mat-v4.png"),Size=size,ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,MouseFilter=MouseFilterEnum.Ignore });
        Caption("PERMANENT CHANGES · NATIVE REVIEW",new Vector2(32,18),27,true);
        Caption("Approved A seal + B details · Read-only visual fixtures",new Vector2(32,70),18);
        _hero=new CardView("permanent-demo",CardCatalog.Get("oracle-l5r-1645"),0,Zone.Home,true,false,null,null,5,3,
            GrantedKeywords:[new("Berserker","oracle-l5r-7420","Permanent")],UnitForce:5,
            GrantedEffects:[new("+2 Force","","oracle-l5r-7420","Permanent","Stat"),
                new("Granted Reaction","After another player's action targets this Personality: Delay his destruction from the action's effects until before this turn ends.","oracle-l5r-7420","Permanent")]);
        if(_long)_hero=_hero with { GrantedEffects=_hero.GrantedEffects!.Concat(Enumerable.Range(0,5).Select(i=>new GrantedEffectView("Additional public effect "+(i+1),string.Join(" ",Enumerable.Repeat("This is deliberately long public ability text for overflow and source-layout review.",3)),i%2==0?"oracle-l5r-7420":"unknown",i%2==0?"Permanent":"Turn"))).ToArray() };
        if(_comparison)
        {
            var source=new AtlasTexture { Atlas=CardChangeArt.B,Region=CardChangeArt.PanelSource };
            AddChild(new TextureRect { Texture=source,Position=new Vector2(50,155),Size=CardChangeArt.PanelSource.Size,ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize });
            Caption("Approved B source",new Vector2(50,112),21);
            var panel=CardChangePanel.Create(_hero,Source);AddChild(panel);panel.Position=new Vector2(745,155);panel.FixedSize=CardChangeArt.PanelSource.Size;panel.Size=CardChangeArt.PanelSource.Size;_detail=panel;
            Caption("Native renderer · live text",new Vector2(745,112),21);
            Face(_hero,new Vector2(65,750),235);
            Face(_hero with { Id="one",GrantedEffects=null },new Vector2(390,750),235);
            Caption("Original A cap pixels + live count",new Vector2(745,805),21);
            Caption("Original B medallions + frame pixels",new Vector2(745,845),21);
            Caption("WeatheredQuill headings / Caudex body",new Vector2(745,885),21);
        }
        else
        {
            if(_perspective)
            {
                _surface=new TableSurface();AddChild(_surface);_surface.ResizePlane(size,1);
                _host=new Control { Position=_surface.CanvasSize/2-size*.80f/2,Scale=Vector2.One*.8f,MouseFilter=MouseFilterEnum.Ignore };_surface.Canvas.AddChild(_host);
            }
            var side=_small?170:260;var spacing=_small?215:310;
            for(var i=0;i<3;i++)Face(_hero with { Id="rail-"+i,Tokens=Tokens(new[]{0,4,6}[i]),GrantedEffects=i==0?null:_hero.GrantedEffects },new Vector2(30+i*spacing,140),side);
            Face(_hero with {Id="bowed",OwnerId=1,Bowed=true,Zone=Zone.Battlefield,UnitForce=0,Tokens=Tokens(5)},new Vector2(30,side+260),side);
            Face(_hero with {Id="opponent",OwnerId=1,GrantedKeywords=[new("Berserker","oracle-l5r-7420","Permanent"),new("Naval","oracle-l5r-10133","Attachment")],Tokens=Tokens(1)},new Vector2(30+spacing,side+260),side);
            if(!_small)
            {
                var item=CardCatalog.Cards.Values.First(d=>!d.ScenarioOnly&&d.Type==CardType.Item);
                var child=Face(new CardView("granted-attachment",item,0,Zone.Attached,true,true,null,"attachment-host",item.Force,item.Chi,
                    GrantedEffects:[new("Action protection","Effects of actions this card performs will not be negated.","oracle-l5r-7420","Permanent")]),new Vector2(660,side+260),side,36);
                var host=Face(_hero with {Id="attachment-host",GrantedEffects=null,Tokens=Tokens(1)},new Vector2(660,side+320),side);
                child.TokenRail!.Companions=[host,child];host.TokenRail!.Companions=[host,child];
                if(!_perspective)Caption("Bowed attachment · owns its seal",new Vector2(660,side*2+360),17);
            }
            if(!_perspective)
            {
                _detail=CardChangePanel.Tooltip(_hero,Source,size,maxHeight:size.Y-165);AddChild(_detail);
                var available=size.X-(30+3*spacing);
                var factor=Math.Min(1,available/_detail.Size.X);_detail.Scale=Vector2.One*factor;
                _detail.Position=new Vector2(30+3*spacing,140);
            }
            if(_perspective)
                Caption("Projected card rails · both seats, crowded tokens and independently bowed attachments",new Vector2(55,size.Y-70),18);
            else
            {
                Caption("One grant",new Vector2(55,side+155),18);
                Caption("Four token types",new Vector2(55+spacing,side+155),18);
                Caption("Six token types",new Vector2(55+spacing*2,side+155),18);
                Caption("Bowed · five token types",new Vector2(45,side*2+280),18);
                Caption("Opponent · permanent + attached",new Vector2(45+spacing,side*2+280),18);
            }
        }
        ValidateProjection();
    }
    private static string? Source(string id)=>id switch { "oracle-l5r-7420"=>"Spirit of the Berserker","oracle-l5r-10133"=>"Suitengu's Path",_=>null };
    private static CardToken[] Tokens(int n)=>Enumerable.Range(0,n).Select(i=>new CardToken("token-"+i,new[]{"Fire","Wealth","Favor","Poison","Blood","Training"}[i],"source")).ToArray();
    private void Caption(string text,Vector2 position,int size,bool heading=false){var label=Palette.Text(text,size,Palette.Paper,heading);label.Position=position;AddChild(label);}
    private CardTile Face(CardView card,Vector2 position,float height,float offset=0)
    {
        var tile=new CardTile(card,true,c=>{_clicks++;_lastClicked=c.Id;},_=>{}) { Position=position,Size=new Vector2(height,height),FaceHeight=height-12,
            AlignUnitRightEdge=true,ReducedMotion=true,TokenRailOffset=offset*(height-12)/198,TokenSourceName=Source,ProjectToScreen=_surface is null?null:_surface.ProjectCanvas };
        ((Node?)_host??this).AddChild(tile);_cards.Add(tile);
        if(card.UnitForce.HasValue)((Node?)_host??this).AddChild(new UnitStandard { CardAnchor=tile,Card=card,Projection=_surface is null?null:_surface.ProjectCanvas,Explanation=UnitStandard.ExplainUnit(card) });
        return tile;
    }
    private void ValidateProjection()
    {
        void Check(bool value,string message){if(!value)throw new Exception(message);}
        Check(CardChanges.Permanent(_hero).Length==(_long?6:3),"Count distinct permanent changes, not tokens");
        Check(CardChanges.Permanent(_hero with { Definition=null }).Length==0,"Hidden identity suppression");
        Check(!CardChanges.HasSeal(_hero with { FaceUp=false }),"Face-down seal suppression");
        Check(!CardChanges.HasSeal(_hero with { Zone=Zone.Hand }),"No board seal in private hand");
        Check(CardChanges.Permanent(_hero with { GrantedKeywords=[new("Naval","source","Attachment")],GrantedEffects=null }).Length==0,"Attachment is not permanent");
        Check(CardChanges.Permanent(_hero with { GrantedKeywords=null,GrantedEffects=null,Modifiers=[new("source","bonus","Force",2)] }).Length==0,"Temporary stat changes never acquire a permanent seal");
        Check(CardChanges.SourceName("secret-instance",Source)=="Source unavailable","No raw IDs or guessed identities");
        Check(CardChanges.Summary(_hero,Source).Contains("Spirit of the Berserker"),"Readable source");
    }
    public override void _Input(InputEvent e){if(_surface is not null&&e is InputEventMouse mouse)_surface.ForwardMouse(mouse);}
    public override void _Process(double delta)
    {
        if(_interactive)return;
        _clock+=delta;if(_clock<1.3)return;
        try
        {
            var seal=_cards[0].TokenRail!.GrantSeal!;
            var point=seal.GetGlobalTransform()*(seal.Size/2);if(_surface is not null)point=_surface.ProjectCanvas(point);
            if(_stage==0)
            {
                foreach(var tile in _cards)
                {
                    if(tile.TokenRail?.GrantSeal is null)throw new Exception("Missing seal: "+tile.Card.Id);
                    if(tile.PublicStatusLabels.Any(x=>x.Contains("Berserker")))throw new Exception("Permanent grant still covers printed face");
                    if(!tile.TokenRail.IconsWithinCardHeight())throw new Exception("Icons run past the card's height: "+tile.Card.Id);
                }
                CardChangePanel.LastCardId=null;
                GetViewport().NotifyMouseEntered();
                GetViewport().WarpMouse(point);
                GetViewport().PushInput(new InputEventMouseMotion { Position=point,GlobalPosition=point,Relative=Vector2.One },true);_stage=1;_clock=0;return;
            }
            if(_stage==1)
            {
                if(!_perspective&&CardChangePanel.LastCardId!=_cards[0].Card.Id)throw new Exception($"Native seal hover did not open its detail panel: {GetViewport().GuiGetHoveredControl()?.GetPath()} at {point}");
                Input.ParseInputEvent(new InputEventMouseButton { Position=point,ButtonIndex=MouseButton.Left,Pressed=true });
                Input.ParseInputEvent(new InputEventMouseButton { Position=point,ButtonIndex=MouseButton.Left,Pressed=false });
                _stage=2;_clock=0;return;
            }
            if(_stage==2)
            {
                if(_clicks!=1)throw new Exception($"Seal must select its owner exactly once: {_clicks}");
                seal.ReleaseFocus();seal.GrabFocus();_stage=3;_clock=0;return;
            }
            if(_stage==3)
            {
                if(CardChangePanel.LastCardId!=_cards[0].Card.Id||!seal.HasFocusPanel)throw new Exception("Keyboard focus did not expose effects");
                seal.ReleaseFocus();Input.ParseInputEvent(new InputEventMouseMotion { Position=new Vector2(5,5) });_stage=4;_clock=0;return;
            }
            var attachment=_cards.FirstOrDefault(c=>c.Card.AttachedTo is not null);
            if(_stage==4&&attachment is not null)
            {
                var attachedSeal=attachment.TokenRail!.GrantSeal!;
                var at=attachedSeal.GetGlobalTransform()*(attachedSeal.Size/2);if(_surface is not null)at=_surface.ProjectCanvas(at);
                Input.ParseInputEvent(new InputEventMouseMotion { Position=at });
                Input.ParseInputEvent(new InputEventMouseButton { Position=at,ButtonIndex=MouseButton.Left,Pressed=true });
                Input.ParseInputEvent(new InputEventMouseButton { Position=at,ButtonIndex=MouseButton.Left,Pressed=false });
                _stage=5;_clock=0;return;
            }
            if(attachment is not null&&(_lastClicked!=attachment.Card.Id||_clicks!=2))throw new Exception("Attachment seal selected its host or did not select exactly once");
            if(_long&&_stage==5&&_detail is not null)
            {
                GetViewport().GetTexture().GetImage().SavePng(_output.Replace(".png","-top.png"));
                var scroll=_detail.FindChildren("*","ScrollContainer",true,false).OfType<ScrollContainer>().Single();
                if(scroll.GetVScrollBar().MaxValue<=scroll.GetVScrollBar().Page)throw new Exception("Long content did not get a scrollable range");
                scroll.ScrollVertical=(int)scroll.GetVScrollBar().MaxValue;
                _stage=6;_clock=0;return;
            }
            attachment?.TokenRail?.GrantSeal?.ReleaseFocus();
            Input.ParseInputEvent(new InputEventMouseMotion { Position=new Vector2(5,5) });
            RenderingServer.ForceDraw();GetViewport().GetTexture().GetImage().SavePng(_output);
            GD.Print("PERMANENT_GRANT_PASS: native hover, focus and one owner click; public-only metadata; permanent/timed distinction; live source names; tokens 4/5/6; bowed and both seats.");GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetViewport().GetTexture().GetImage().SavePng(_output+".failure.png");GetTree().Quit(1);}
    }
}
