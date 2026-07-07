using Content.Shared.Popups;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Interaction;
using Robust.Shared.Network; // Aurora - Added for server-side deletion guard

namespace Content.Shared.Chemistry.EntitySystems;

/// <summary>
///     Entity system used to handle when solution containers are 'spiked'
///     with another entity. Triggers the source entity afterwards.
///     Uses refillable solution as the target solution, as that indicates
///     'easy' refills.
///
///     Examples of spikable entity interactions include pills being dropped into glasses,
///     eggs being cracked into bowls, and so on.
/// </summary>
public sealed partial class SolutionSpikerSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private INetManager _netManager = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RefillableSolutionComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(Entity<RefillableSolutionComponent> entity, ref InteractUsingEvent args)
    {
        if (TrySpike(args.Used, (args.Target, entity.Comp), args.User))
            args.Handled = true;
    }

    /// <summary>
    ///     Immediately transfer all reagents from this entity, to the other entity.
    ///     The source entity will then be acted on by TriggerSystem.
    /// </summary>
    /// <param name="source">Source of the solution.</param>
    /// <param name="target">Target to spike with the solution from source.</param>
    /// <param name="user">User spiking the target solution.</param>
    private bool TrySpike(Entity<SolutionSpikerComponent?> source,
        Entity<RefillableSolutionComponent?> target,
        EntityUid user)
    {
        if (!Resolve(source, ref source.Comp, false)
            || !_solution.TryGetRefillableSolution((target, target.Comp), out var targetSoln, out var targetSolution)
            || !_solution.TryGetSolution(source.Owner, source.Comp.SourceSolution, out _, out var sourceSolution))
        {
            return false;
        }

        if (targetSolution.Volume == 0 && !source.Comp.IgnoreEmpty)
        {
            _popup.PopupClient(Loc.GetString(source.Comp.PopupEmpty, ("spiked-entity", target), ("spike-entity", source)), user, user);
            return false;
        }

        if (!_solution.ForceAddSolution(targetSoln.Value, sourceSolution))
            return false;

        _popup.PopupClient(Loc.GetString(source.Comp.Popup, ("spiked-entity", target), ("spike-entity", source)), user, user);
        sourceSolution.RemoveAllSolution();
        // Aurora - Guard entity deletion to server-only to prevent client prediction errors
        // Client should never predict deletion of networked entities
        if (source.Comp.Delete && _netManager.IsServer)
            QueueDel(source);

        return true;
    }
}
