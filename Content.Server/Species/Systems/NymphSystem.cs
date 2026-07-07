using Content.Shared._NF.Bank.Components; // Frontier
using Content.Server.Cargo.Components;
using Content.Server.Mind;
using Content.Server.Zombies;
using Content.Shared.Body;
using Content.Shared.Species.Components;
using Content.Shared.Zombies;
using Robust.Shared.Prototypes;

namespace Content.Server.Species.Systems;

public sealed partial class NymphSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _protoManager = default!;
    [Dependency] private MindSystem _mindSystem = default!;
    [Dependency] private ZombieSystem _zombie = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NymphComponent, OrganGotRemovedEvent>(OnRemovedFromPart);
    }

    private void OnRemovedFromPart(EntityUid uid, NymphComponent comp, ref OrganGotRemovedEvent args)
    {
        if (TerminatingOrDeleted(uid) || TerminatingOrDeleted(args.Target))
            return;

        if (!_protoManager.TryIndex<EntityPrototype>(comp.EntityPrototype, out var entityProto))
            return;

        // Get the organs' position & spawn a nymph there
        var coords = Transform(uid).Coordinates;
        var nymph = SpawnAtPosition(entityProto.ID, coords);

        if (HasComp<ZombieComponent>(args.Target)) // Zombify the new nymph if old one is a zombie
            _zombie.ZombifyEntity(nymph);

        // Move the mind if there is one and it's supposed to be transferred
        if (comp.TransferMind && _mindSystem.TryGetMind(uid, out var mindId, out var mind))
        {   // Aurora's Song - Keep as a bracketed function for NF
            _mindSystem.TransferTo(mindId, nymph, mind: mind);

            // Frontier: bank account transfer, mob setup
            EnsureComp<CargoSellBlacklistComponent>(nymph);

            if (HasComp<BankAccountComponent>(args.Target)) // Aurora's Song - OldBody>Target
                EnsureComp<BankAccountComponent>(nymph);
            // End Frontier
        }

        // Delete the old organ
        QueueDel(uid);
    }
}
