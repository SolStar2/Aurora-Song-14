using Content.Server.Actions;
using Content.Server.Humanoid;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Toggleable;
using Robust.Shared.Prototypes;

namespace Content.Server._AS.Togglable;

/// <summary>
/// Handles the action buttons for the undergarment toggles
/// </summary>
public sealed partial class ToggleUndergarmentSystem : EntitySystem
{
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private SharedHideableHumanoidLayersSystem _humanoid = default!;

    private static readonly EntProtoId ToggleTopAction = "ActionToggleUndergarmentTop";
    private static readonly EntProtoId ToggleBottomAction = "ActionToggleUndergarmentBottom";
    private const HumanoidVisualLayers UndergarmentTop = HumanoidVisualLayers.UndergarmentTop;
    private const HumanoidVisualLayers UndergarmentBottom = HumanoidVisualLayers.UndergarmentBottom;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ToggleUndergarmentComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ToggleUndergarmentComponent, ToggleUndergarmentTopActionEvent>(OnToggleTop);
        SubscribeLocalEvent<ToggleUndergarmentComponent, ToggleUndergarmentBottomActionEvent>(OnToggleBottom);
    }

    private void OnMapInit(Entity<ToggleUndergarmentComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.ToggleTopActionEntity, ToggleTopAction);
        _actions.AddAction(ent, ref ent.Comp.ToggleBottomActionEntity, ToggleBottomAction);
    }

    private void OnToggleTop(Entity<ToggleUndergarmentComponent> ent, ref ToggleUndergarmentTopActionEvent _)
    {
        ent.Comp.UndergarmentTopEnabled = !ent.Comp.UndergarmentTopEnabled;

        _humanoid.SetLayerOcclusion(ent.Owner, UndergarmentTop, !ent.Comp.UndergarmentTopEnabled, SlotFlags.PREVENTEQUIP);
    }

    private void OnToggleBottom(Entity<ToggleUndergarmentComponent> ent, ref ToggleUndergarmentBottomActionEvent _)
    {
        ent.Comp.UndergarmentBottomEnabled = !ent.Comp.UndergarmentBottomEnabled;

        _humanoid.SetLayerOcclusion(ent.Owner, UndergarmentBottom, !ent.Comp.UndergarmentBottomEnabled, SlotFlags.PREVENTEQUIP);
    }
}
