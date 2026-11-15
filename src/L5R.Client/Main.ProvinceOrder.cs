using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private PanelContainer? _provinceOrderTray;
    private string? _provinceOrderSelection, _provinceOrderIdentity;
    private long _provinceOrderVersion;
    private Button? _provinceOrderConfirm;

    private void RenderProvinceOrder()
    {
        if (_provinceOrderTray is not null) { _provinceOrderTray.GetParent().RemoveChild(_provinceOrderTray); _provinceOrderTray.QueueFree(); _provinceOrderTray = null; }
        _provinceOrderConfirm = null;
        var order = View.PendingProvinceOrder;
        if (order is null || order.PlayerId != View.ViewerId)
        { _provinceOrderSelection = null; _provinceOrderIdentity = null; return; }
        if (_provinceOrderIdentity != order.Id || _provinceOrderVersion != View.Version)
        { _provinceOrderSelection = null; _provinceOrderIdentity = order.Id; _provinceOrderVersion = View.Version; }
        var province = View.Players.Single(p => p.Id == order.OwnerId).Provinces.Single(p => p.Index == order.Province);
        var width = Math.Min(790, _layoutWidth - SidebarWidth - 180);
        _provinceOrderTray = new PanelContainer { Theme = SidebarSkin.Theme(), MouseFilter = Control.MouseFilterEnum.Stop, ZIndex = 100 };
        _provinceOrderTray.AddThemeStyleboxOverride("panel", SidebarSkin.Surface());
        _root.AddChild(_provinceOrderTray);
        _provinceOrderTray.Position = new Vector2(150 + (_layoutWidth - SidebarWidth - 180 - width) / 2, 526);
        _provinceOrderTray.Size = new Vector2(width, 480);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 8); _provinceOrderTray.AddChild(column);
        var title = Palette.Text($"PROVINCE {order.Province + 1} · CHOOSE ORDER", 26, title: true);
        title.HorizontalAlignment = HorizontalAlignment.Center; column.AddChild(title);
        var prompt = Palette.Wrapped(order.Prompt, 19, Palette.Paper); prompt.HorizontalAlignment = HorizontalAlignment.Center; column.AddChild(prompt);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; row.AddThemeConstantOverride("separation", 16); column.AddChild(row);
        if (province.Region is { } region)
        {
            var source = new VBoxContainer { CustomMinimumSize = new Vector2(135, 0) };
            source.AddChild(Palette.Text("ATTACHED REGION", 14, Palette.PaperDim));
            source.AddChild(CardPreview.Face(region, 180));
            row.AddChild(source); row.AddChild(new VSeparator());
        }
        var selected = order.Options.FirstOrDefault(o => o.Id == _provinceOrderSelection);
        foreach (var slot in ProvincePresentation.Slots(province))
        {
            var item = new VBoxContainer { CustomMinimumSize = new Vector2(170, 0) };
            var label = selected is null ? "DYNASTY " + ProvincePresentation.SlotName(slot.Index)
                : ProvincePresentation.SlotName(slot.Index) + " · " + (Array.IndexOf(selected.Slots, slot.Index) switch { 0 => "1ST", 1 => "2ND", _ => "—" });
            var heading = Palette.Text(label, 19, Palette.Gold); heading.HorizontalAlignment = HorizontalAlignment.Center; item.AddChild(heading);
            if (slot.Card is { } card)
            {
                var face = new CardTile(card, true, _ => SelectFirstProvinceSlot(slot.Index), _ => { })
                {
                    CustomMinimumSize = new Vector2(170, 226),
                    FaceHeight = 214,
                    ReducedMotion = _reducedMotion,
                    QuietSelection = true,
                    Highlighted = selected?.Slots.FirstOrDefault(-1) == slot.Index,
                    ContextHelp = "Choose this slot first. Confirm the order below."
                };
                item.AddChild(face);
            }
            else item.AddChild(Palette.Wrapped("Empty slot", 20, Palette.PaperDim));
            var option = order.Options.FirstOrDefault(o => o.Slots.FirstOrDefault(-1) == slot.Index);
            var choose = SidebarSkin.Button("Choose " + ProvincePresentation.SlotName(slot.Index) + " first", () => SelectFirstProvinceSlot(slot.Index));
            choose.Disabled = option is null || ProvincePresentation.CurrentOption(View, order.Id, _provinceOrderVersion, option.Id) is null;
            choose.TooltipText = option?.Reason ?? ""; item.AddChild(choose); row.AddChild(item);
        }
        var footer = new HBoxContainer(); footer.AddThemeConstantOverride("separation", 12); column.AddChild(footer);
        var summary = Palette.Wrapped(selected?.Label ?? "Choose which slot goes first.", 18, Palette.PaperDim);
        summary.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; footer.AddChild(summary);
        var decisionId = order.Id; var version = View.Version;
        _provinceOrderConfirm = SidebarSkin.Button("Confirm order", () => ConfirmProvinceOrder(decisionId, version, _provinceOrderSelection), true);
        _provinceOrderConfirm.Disabled = selected is null || ProvincePresentation.CurrentOption(View, decisionId, version, selected.Id) is null;
        _provinceOrderConfirm.CustomMinimumSize = new Vector2(210, 58); footer.AddChild(_provinceOrderConfirm);
    }

    private void SelectFirstProvinceSlot(int slot)
    {
        var order = View.PendingProvinceOrder;
        var option = order?.Options.FirstOrDefault(o => o.Slots.FirstOrDefault(-1) == slot);
        if (order is null || option is null || ProvincePresentation.CurrentOption(View, order.Id, View.Version, option.Id) is null) return;
        _provinceOrderSelection = option.Id; RenderProvinceOrder();
    }

    private void ConfirmProvinceOrder(string decisionId, long version, string? optionId)
    {
        var option = optionId is null ? null : ProvincePresentation.CurrentOption(View, decisionId, version, optionId);
        if (option is null || _session.Busy || PresentationBusy) return;
        if (_provinceReviewView is not null)
        {
            _provinceReviewView = _provinceReviewView with { Version = _provinceReviewView.Version + 1, PendingProvinceOrder = null };
            _provinceReviewConfirmed = option.Id; Render();
            SetNotice("Preview order: " + option.Label + ". No game effect was resolved."); return;
        }
        _ = Submit(option.Command!);
    }

    private void LayoutProvinceOrder()
    {
        if (_provinceOrderTray is null) return;
        var width = Math.Min(790, _layoutWidth - SidebarWidth - 180);
        _provinceOrderTray.Size = new Vector2(width, _provinceOrderTray.Size.Y);
        _provinceOrderTray.Position = new Vector2(150 + (_layoutWidth - SidebarWidth - 180 - width) / 2, 526);
    }

    private void PassProvinceDecision()
    {
        // Closing this curtain keeps the original seat. Only the explicit ready button switches.
        _root.Hide();
        var (dialog, body) = Dialog("Pass the device", 530, 220);
        HideDialogButtons(dialog);
        dialog.TreeExiting += () => { if (IsInstanceValid(_root)) { Render(); _root.Show(); } };
        body.AddChild(Palette.Wrapped("Hand the device to the deciding player before revealing their private hand.", 21));
        body.AddChild(SidebarSkin.Button("I'm ready · reveal my hand", () =>
        { _selected = null; _chosen = null; _session.SwitchPracticeSeat(); dialog.QueueFree(); }, true));
    }
}
