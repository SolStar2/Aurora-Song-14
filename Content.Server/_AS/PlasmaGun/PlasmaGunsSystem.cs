using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Storage.EntitySystems;
using Content.Server.Stunnable;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction;
using Content.Shared._AS.PlasmaGun;
using Content.Shared.PneumaticCannon;
using Content.Shared.StatusEffect;
using Content.Shared.Tools.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;

namespace Content.Server._AS.PlasmaGun;

// This is essentially a more specialised implementation of the PneumaticCannonSystem to be used with plasma guns, avoiding
// the use of certain features (like self stun on high power, throwing items)

public sealed partial class PlasmaGunsSystem : SharedPlasmaGunsSystem
{
    [Dependency] private GasTankSystem _gasTank = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlasmaGunComponent, GunShotEvent>(OnShoot);
        SubscribeLocalEvent<PlasmaGunComponent, ContainerIsInsertingAttemptEvent>(OnContainerInserting);
        SubscribeLocalEvent<PlasmaGunComponent, GunRefreshModifiersEvent>(OnGunRefreshModifiers);
    }

    private void OnContainerInserting(EntityUid uid, PlasmaGunComponent component, ContainerIsInsertingAttemptEvent args)
    {
        if (args.Container.ID != PlasmaGunComponent.TankSlotId)
            return;

        if (!TryComp<GasTankComponent>(args.EntityUid, out var gas))
            return;

        // Check that the tank has the required amount of plasma in it, rather than just any gas
        if (gas.Air.GetMoles(Gas.Plasma) < component.GasUsage)
        {
            args.Cancel();
            return;
        }

        // only accept tanks if it uses gas
        if (gas.Air.TotalMoles >= component.GasUsage && component.GasUsage > 0f)
            return;

        args.Cancel();
    }

    private void OnShoot(Entity<PlasmaGunComponent> cannon, ref GunShotEvent args)
    {
        var (uid, component) = cannon;
        // require a gas tank if it uses gas
        var gas = GetGas(cannon);
        if (gas == null && component.GasUsage > 0f)
            return;

        // ignore gas stuff if the cannon doesn't use any
        if (gas == null)
            return;

        // Check the tank has enough plasma
        if (gas.Value.Comp.Air.GetMoles(Gas.Plasma) < component.GasUsage)
        {
            // Eject the tank if it no longer has enough plasma
            _slots.TryEject(uid, PlasmaGunComponent.TankSlotId, args.User, out _);
            return;
        }
        // Not the most elegant solution, but it removes the gas without poisoning everyone nearby. Take it or leave it.
        var removed = _gasTank.RemoveAir(gas.Value, component.GasUsage);

        // Just in case we wanted to use the pneumatic cannon way of polluting local atmos with plasma, would be funny
        // var environment = _atmos.GetContainingMixture(cannon.Owner, false, true);
        // var removed = _gasTank.RemoveAir(gas.Value, component.GasUsage);
        // if (environment != null && removed != null)
        // {
        //     _atmos.Merge(environment, removed);
        // }

        if (gas.Value.Comp.Air.TotalMoles >= component.GasUsage)
            return;

        // eject gas tank
        _slots.TryEject(uid, PlasmaGunComponent.TankSlotId, args.User, out _);
    }

    private void OnGunRefreshModifiers(Entity<PlasmaGunComponent> ent, ref GunRefreshModifiersEvent args)
    {
        if (ent.Comp.ProjectileSpeed is { } speed)
            args.ProjectileSpeed = speed;
    }

    /// <summary>
    ///     Returns whether the plasma gun has enough gas to shoot, as well as the tank itself.
    /// </summary>
    private Entity<GasTankComponent>? GetGas(EntityUid uid)
    {
        if (!Container.TryGetContainer(uid, PlasmaGunComponent.TankSlotId, out var container) ||
            container is not ContainerSlot slot || slot.ContainedEntity is not {} contained)
            return null;

        return TryComp<GasTankComponent>(contained, out var gasTank) ? (contained, gasTank) : null;
    }
}
