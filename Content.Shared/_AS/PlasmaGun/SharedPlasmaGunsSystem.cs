using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Serialization;

namespace Content.Shared._AS.PlasmaGun;

public abstract partial class SharedPlasmaGunsSystem : EntitySystem
{
    [Dependency] protected SharedContainerSystem Container = default!;
    [Dependency] protected SharedPopupSystem Popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlasmaGunComponent, AttemptShootEvent>(OnAttemptShoot);
    }

    private void OnAttemptShoot(EntityUid uid, PlasmaGunComponent component, ref AttemptShootEvent args)
    {
        // if the cannon doesn't need gas then it will always predict firing
        if (component.GasUsage == 0f)
            return;

        // we don't have atmos on shared, so just predict by the existence of a slot item
        // server will handle auto ejecting/not adding the slot item if it doesn't have enough gas,
        // so this won't mispredict
        if (!Container.TryGetContainer(uid, PlasmaGunComponent.TankSlotId, out var container) ||
            container is not ContainerSlot slot || slot.ContainedEntity is null)
        {
            args.Cancelled = true;
        }
    }
}
