using Content.Server.Silicons.Laws;
using Content.Server.StationEvents.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Station.Components;
//Moffstation - EMP Vulnerability - Begin
using Content.Shared._Moffstation.Traits.Components;
using Content.Shared._Moffstation.Traits.EntitySystems;
//Moffstation - End
using Robust.Shared.Random; // Aurora's Song - EMP Vulnerability Chance

namespace Content.Server.StationEvents.Events;

public sealed partial class IonStormRule : StationEventSystem<IonStormRuleComponent>
{
    [Dependency] private IonStormSystem _ionStorm = default!;
    [Dependency] private SharedEmpVulnerableSystem _empVulnerable = default!; //Moffstation - EMP Vulnerability
    [Dependency] private IRobustRandom _robustRandom = default!; // Aurora's Song - EMP Vulnerability Chance

    protected override void Started(EntityUid uid, IonStormRuleComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);

        // Frontier - Affect all silicon beings in the sector, not just on-station.
        // if (!TryGetRandomStation(out var chosenStation))
        //     return;
        // End Frontier

        var query = EntityQueryEnumerator<SiliconLawBoundComponent, TransformComponent, IonStormTargetComponent>();
        while (query.MoveNext(out var ent, out var lawBound, out var xform, out var target))
        {
            // Frontier - Affect all silicon beings in the sector, not just on-station.
            // // only affect law holders on the station
            // if (CompOrNull<StationMemberComponent>(xform.GridUid)?.Station != chosenStation)
            //     continue;
            // End Frontier

            _ionStorm.IonStormTarget((ent, lawBound, target));
        }

        //Moffstation - Begin - EMP Vulnerability
        var empAffectedQuery = EntityQueryEnumerator<EmpVulnerableComponent, TransformComponent>();
        while (empAffectedQuery.MoveNext(out var ent, out var empVulnerable, out var xform))
        {
            // only affect vulnerable entities on the station
            // if(CompOrNull<StationMemberComponent>(xform.GridUid)?.Station != chosenStation)
            //     continue; Aurora's Song - removed as it does not pertain to shuttle fork.

            if (!_robustRandom.Prob(empVulnerable.IonStunChance)) // Aurora's Song - EMP Vulnerability Chance
                return;

            _empVulnerable.IonStormTarget((ent, empVulnerable));
        }
        //Moffstation - End
    }
}
