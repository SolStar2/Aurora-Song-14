using Content.Shared._NF.Pinpointer;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Emag.Systems;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Robust.Shared.Serialization;

namespace Content.Shared.Pinpointer;

public abstract partial class SharedPinpointerSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private EmagSystem _emag = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!; // Frontier

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PinpointerComponent, GotEmaggedEvent>(OnEmagged);
        SubscribeLocalEvent<PinpointerComponent, GotUnEmaggedEvent>(OnUnemagged); // Frontier
        SubscribeLocalEvent<PinpointerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<PinpointerComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<PinpointerComponent, PinpointerDoAfterEvent>(OnPinpointerDoAfter); // Frontier
    }

    /// <summary>
    ///     Set the target if capable
    /// </summary>
    private void OnAfterInteract(Entity<PinpointerComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.CanReach || args.Target is not { } target)
            return;

        if (!ent.Comp.CanRetarget || ent.Comp.IsActive)
            return;

        // Frontier: disallow pinpointing mobs
        if (!ent.Comp.CanTargetMobs && HasComp<MobStateComponent>(args.Target))
            return;

        // TODO add doafter once the freeze is lifted
        args.Handled = true;

        // Frontier: the below was made into a do-after, see OnPinpointerDoAfter.
        // ent.Comp.Target = args.Target;
        // _adminLogger.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(args.User):player} set target of {ToPrettyString(ent):pinpointer} to {ToPrettyString(ent.Comp.Target.Value):target}");
        // if (ent.Comp.UpdateTargetName)
        //     ent.Comp.TargetName = ent.Comp.Target == null ? null : Identity.Name(ent.Comp.Target.Value, EntityManager);

        var daArgs = new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(ent.Comp.RetargetDoAfter),
            new PinpointerDoAfterEvent(), ent, args.Target, ent)
        {
            BreakOnDamage = true,
            BreakOnWeightlessMove = true,
            CancelDuplicate = true,
            BreakOnHandChange = true,
            NeedHand = true,
            BreakOnMove = true,
        };
        _doAfter.TryStartDoAfter(daArgs);
        // End Frontier
    }

    private void OnPinpointerDoAfter(EntityUid uid, PinpointerComponent component, PinpointerDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        // Frontier: two-way pinpointer tracking
        if (component.SetsTarget)
        {
            if (TryComp<PinpointerTargetComponent>(component.Target, out var pinpointerTarget))
            {
                pinpointerTarget.Entities.Remove(uid);
                if (pinpointerTarget.Entities.Count <= 0)
                    RemComp<PinpointerTargetComponent>(component.Target.Value);
            }
            if (args.Target != null)
            {
                pinpointerTarget = EnsureComp<PinpointerTargetComponent>(args.Target.Value);
                pinpointerTarget.Entities.Add(uid);
            }
        }
        // End Frontier: two-way pinpointer tracking

        component.Target = args.Target;
        _adminLogger.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(args.User):player} set target of {ToPrettyString(uid):pinpointer} to {ToPrettyString(component.Target):target}");
        if (component.UpdateTargetName)
            component.TargetName = component.Target == null ? null : Identity.Name(component.Target.Value, EntityManager);
    }

    /// <summary>
    ///     Set pinpointers target to track
    /// </summary>
    public virtual void SetTarget(Entity<PinpointerComponent?> ent, EntityUid? target)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        var pinpointer = ent.Comp;

        if (pinpointer.Target == target)
            return;

        // Frontier: two-way pinpointer tracking
        if (pinpointer.SetsTarget)
        {
            if (TryComp<PinpointerTargetComponent>(pinpointer.Target, out var pinpointerTarget))
            {
                pinpointerTarget.Entities.Remove(ent);
                if (pinpointerTarget.Entities.Count <= 0)
                    RemComp<PinpointerTargetComponent>(pinpointer.Target.Value);
            }
            if (target != null)
            {
                pinpointerTarget = EnsureComp<PinpointerTargetComponent>(target.Value);
                pinpointerTarget.Entities.Add(ent);
            }
        }
        // End Frontier: two-way pinpointer tracking

        pinpointer.Target = target;
        if (pinpointer.UpdateTargetName)
            pinpointer.TargetName = target == null ? null : Identity.Name(target.Value, EntityManager);
        if (pinpointer.IsActive)
            UpdateDirectionToTarget(ent);
    }

    /// <summary>
    ///     Update direction from pinpointer to selected target (if it was set)
    /// </summary>
    protected virtual void UpdateDirectionToTarget(Entity<PinpointerComponent?> ent)
    {

    }

    private void OnExamined(Entity<PinpointerComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || ent.Comp.TargetName == null)
            return;

        args.PushMarkup(Loc.GetString("examine-pinpointer-linked", ("target", ent.Comp.TargetName)));
    }

    /// <summary>
    ///     Manually set distance from pinpointer to target
    /// </summary>
    public void SetDistance(Entity<PinpointerComponent?> ent, Distance distance)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        if (distance == ent.Comp.DistanceToTarget)
            return;

        ent.Comp.DistanceToTarget = distance;
        Dirty(ent);
    }

    /// <summary>
    ///     Try to manually set pinpointer arrow direction.
    ///     If difference between current angle and new angle is smaller than
    ///     pinpointer precision, new value will be ignored and it will return false.
    /// </summary>
    public bool TrySetArrowAngle(Entity<PinpointerComponent?> ent, Angle arrowAngle)
    {
        if (!Resolve(ent, ref ent.Comp))
            return false;

        if (ent.Comp.ArrowAngle.EqualsApprox(arrowAngle, ent.Comp.Precision))
            return false;

        ent.Comp.ArrowAngle = arrowAngle;
        Dirty(ent);

        return true;
    }

    /// <summary>
    ///     Activate/deactivate pinpointer screen. If it has target it will start tracking it.
    /// </summary>
    public void SetActive(Entity<PinpointerComponent?> ent, bool isActive)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        if (isActive == ent.Comp.IsActive)
            return;

        ent.Comp.IsActive = isActive;
        Dirty(ent);
    }


    /// <summary>
    ///     Toggle Pinpointer screen. If it has target it will start tracking it.
    /// </summary>
    /// <returns>True if pinpointer was activated, false otherwise</returns>
    public virtual bool TogglePinpointer(Entity<PinpointerComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return false;

        var isActive = !ent.Comp.IsActive;
        SetActive(ent, isActive);
        return isActive;
    }

    private void OnEmagged(Entity<PinpointerComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction))
            return;

        if (_emag.CheckFlag(ent, EmagType.Interaction))
            return;

        if (ent.Comp.CanRetarget)
            return;

        args.Handled = true;
        ent.Comp.CanRetarget = true;
    }

    // Frontier: demag
    private void OnUnemagged(EntityUid uid, PinpointerComponent component, ref GotUnEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction))
            return;

        if (!_emag.CheckFlag(uid, EmagType.Interaction))
            return;

        if (component.CanRetarget)
            component.CanRetarget = false;

        args.Handled = true;
    }
    // End Frontier: demag
}

// Frontier - do-after
[Serializable, NetSerializable]
public sealed partial class PinpointerDoAfterEvent : SimpleDoAfterEvent
{
}
