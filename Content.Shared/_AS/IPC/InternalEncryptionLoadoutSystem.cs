using Content.Shared.Radio.Components;
using Content.Shared.Roles;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared._AS.IPC;

public sealed partial class InternalEncryptionLoadoutSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public bool TryEquipLoadoutEquipment(Entity<EncryptionKeyHolderComponent?> ent, IEquipmentLoadout loadout)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        var keysToInsert = new List<EntityUid>();

        if (loadout.Equipment.TryGetValue("ears", out var earProto) && _prototype.HasIndex(new EntProtoId(earProto)))
        {
            var earEquipment = Spawn(earProto, _transform.GetMapCoordinates(ent));
            if (TryComp<EncryptionKeyHolderComponent>(earEquipment, out var earHolder))
            {
                var keys = _container.EmptyContainer(earHolder.KeyContainer);

                keysToInsert.AddRange(keys);
            }
            QueueDel(earEquipment);
        }

        foreach (var key in keysToInsert)
        {
            if (!_container.Insert(key, ent.Comp.KeyContainer))
            {
                QueueDel(key);
            }
        }

        return true;
    }

    public bool TryEquipLoadoutEncryptionKeys(Entity<EncryptionKeyHolderComponent?> ent, IEnumerable<EntProtoId> keys)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        foreach (var protoId in keys)
        {
            TrySpawnInContainer(protoId, ent, EncryptionKeyHolderComponent.KeyContainerName, out _);
        }

        return true;
    }
}
