using Content.Server._EE.Silicon.WeldingHealing;
using Content.Shared.Tools.Components;
using Content.Shared._EE.Silicon.WeldingHealing;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools;
using SharedToolSystem = Content.Shared.Tools.Systems.SharedToolSystem;

namespace Content.Server._EE.Silicon.WeldingHealable;

public sealed partial class WeldingHealableSystem : SharedWeldingHealableSystem
{
    [Dependency] private SharedToolSystem _toolSystem = default!;
    [Dependency] private DamageableSystem _damageableSystem = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<WeldingHealableComponent, InteractUsingEvent>(Repair);
        SubscribeLocalEvent<WeldingHealableComponent, SiliconRepairFinishedEvent>(OnRepairFinished);
    }

    private void OnRepairFinished(EntityUid uid, WeldingHealableComponent healableComponent, SiliconRepairFinishedEvent args)
    {
        if (args.Cancelled || args.Used == null
            || !TryComp<DamageableComponent>(args.Target, out var damageable)
            || !TryComp<InjurableComponent>(args.Target, out var injurable) // Aurora's Song - Use Injurable
            || !TryComp<WeldingHealingComponent>(args.Used, out var component)
            || injurable.DamageContainer is null // Aurora's Song - Use Injurable
            || !component.DamageContainers.Contains(injurable.DamageContainer.Value.Id) // Aurora's Song - Use Injurable
            || !HasDamage((args.Target.Value, damageable), component) // Aurora's Song - Use Injurable
            || !TryComp<WelderComponent>(args.Used, out var welder)
            || !_solutionContainer.TryGetSolution(args.Used.Value, welder.FuelSolutionName, out var solution)) // Aurora's Song - Solution Refactor
            return;

        _damageableSystem.TryChangeDamage(uid, component.Damage, true, false, origin: args.User);

        _solutionContainer.RemoveReagent((args.Used.Value, solution), welder.FuelReagent, component.FuelCost); // Aurora's Song - Solution Refactor

        var str = Loc.GetString("comp-repairable-repair",
            ("target", uid),
            ("tool", args.Used!));
        _popup.PopupEntity(str, uid, args.User);

        if (!args.Used.HasValue)
            return;

        args.Handled = _toolSystem.UseTool
            (args.Used.Value,
            args.User,
            uid,
            args.Delay,
            component.QualityNeeded,
            new SiliconRepairFinishedEvent
            {
                Delay = args.Delay
            });
    }
    private async void Repair(EntityUid uid, WeldingHealableComponent healableComponent, InteractUsingEvent args)
    {
        if (args.Handled
            // Aurora's Song Start - Use Injurable
            || !TryComp<WeldingHealingComponent>(args.Used, out var component)
            || !TryComp<DamageableComponent>(args.Target, out var damageable)
            || !TryComp<InjurableComponent>(args.Target, out var injurable)
            || injurable.DamageContainer is null
            || !component.DamageContainers.Contains(injurable.DamageContainer.Value.Id)
            || !HasDamage((args.Target, damageable), component)
            // Aurora's Song End
            || !_toolSystem.HasQuality(args.Used, component.QualityNeeded)
            || args.User == args.Target && !(component.AllowSelfHeal && healableComponent.AllowSelfHeal)) // DeltaV - self heal disabled by WeldingHealable
            return;

        float delay = args.User == args.Target
            ? component.DoAfterDelay * component.SelfHealPenalty
            : component.DoAfterDelay;

        args.Handled = _toolSystem.UseTool
            (args.Used,
            args.User,
            args.Target,
            delay,
            component.QualityNeeded,
            new SiliconRepairFinishedEvent
            {
                Delay = delay,
            });
    }

 private bool HasDamage(Entity<DamageableComponent> ent, WeldingHealingComponent healable)
    {
        var damage = _damageableSystem.GetPositiveDamage(ent);

        // Aurora's Song - Replace with TryGetValue
        foreach (var type in healable.Damage.DamageDict)
        {
            if (damage.DamageDict.TryGetValue(type.Key, out var value) && value > 0)
                return true;
        }

        return false;
    }
}

