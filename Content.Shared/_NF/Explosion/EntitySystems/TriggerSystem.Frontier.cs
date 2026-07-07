// Aurora's Song - Basically this whole thing is rewritten

using Content.Shared._NF.Explosion.Components;
using Content.Shared.Implants;
using Content.Shared.Body.Components;
using Content.Shared._NF.Interaction.Events;
using Content.Shared.Body.Systems;
using Content.Shared.Gibbing;
using Content.Shared.Inventory;
using Content.Shared.Projectiles;
using Content.Shared.Station;
using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.Containers;

namespace Content.Shared.Trigger.Systems;

public sealed partial class TriggerSystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    // [Dependency] private GibbingSystem _gibbing = default!; // Aurora's Song
    // [Dependency] private SharedStationSystem _station = default!; // Aurora's Song
    [Dependency] private InventorySystem _inventory = default!;


    private void NFInitialize()
    {
        SubscribeLocalEvent<TriggerOnBeingGibbedComponent, BeingGibbedEvent>(OnBeingGibbed); // Aurora's Song - No longer triggers
        SubscribeLocalEvent<TriggerOnInteractionPopupUseComponent, InteractionPopupOnUseFailureEvent>(OnPopupInteractionFailure);
        SubscribeLocalEvent<TriggerOnInteractionPopupUseComponent, InteractionPopupOnUseSuccessEvent>(OnPopupInteractionSuccess);

        SubscribeLocalEvent<ReplaceOnTriggerComponent, TriggerEvent>(OnReplaceTrigger);
        SubscribeLocalEvent<TriggerOnProjectileHitComponent, ProjectileHitEvent>(OnProjectileHitEvent);
    }

    // Aurora's Song - Allow GibOnTrigger to overwrite subsequent gibbing
    private void OnBeingGibbed(EntityUid uid, TriggerOnBeingGibbedComponent component,  ref BeingGibbedEvent args)
    {
        if (!TryComp<GibOnTriggerComponent>(uid, out var comp))
            return;

        if (comp.DeleteItems)
        {
            var items = _inventory.GetHandOrInventoryEntities(uid);
            foreach (var item in items)
            {
                PredictedQueueDel(item);
            }
        }

        if (args.dropGiblets) // Make sure we don't drop giblets when we're not supposed to from the original gib
        {
            args.dropGiblets = !comp.DeleteOrgans;
        }
    }

    private void OnPopupInteractionFailure(EntityUid uid, TriggerOnInteractionPopupUseComponent component, InteractionPopupOnUseFailureEvent args)
    {
        if (component.TriggerOnFailure)
            Trigger(uid);
    }

    private void OnPopupInteractionSuccess(EntityUid uid, TriggerOnInteractionPopupUseComponent component, InteractionPopupOnUseSuccessEvent args)
    {
        if (component.TriggerOnSuccess)
            Trigger(uid);
    }

    private void OnReplaceTrigger(Entity<ReplaceOnTriggerComponent> ent, ref TriggerEvent args)
    {
        var xform = Transform(ent);

        if (_container.TryGetContainingContainer((ent, xform), out var container))
        {
            _container.Remove(ent.Owner, container, force: true);
            SpawnInContainerOrDrop(ent.Comp.Proto, container.Owner, container.ID);
        }
        else
        {
            Spawn(ent.Comp.Proto, xform.Coordinates);
        }
        QueueDel(ent);
    }

    private void OnProjectileHitEvent(EntityUid uid, TriggerOnProjectileHitComponent component, ref ProjectileHitEvent args)
    {
        Trigger(uid, args.Target);
    }
}
