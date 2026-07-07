using Content.Server.Radio.EntitySystems;
using Content.Shared.Implants; // Aurora's Song: Retriggers
using Content.Shared._AS.Traits;
using Content.Shared.Humanoid;
using Content.Shared.Implants.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Radio;
using Content.Shared.Station;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing; // Aurora's Song: Death Times

namespace Content.Server._AS.Trigger.Systems;

public sealed partial class ASRattleTriggerSystem : XOnTriggerSystem<RattleOnTriggerComponent>
{
    [Dependency] private SharedStationSystem _station = default!;
    [Dependency] private RadioSystem _radioSystem = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IGameTiming _timing = default!; // Aurora's Song: Death Times & Retriggering

    // Have old functionality of rattle available for NF and Coyote functionality
    protected override void OnTrigger(Entity<RattleOnTriggerComponent> ent, EntityUid target, ref TriggerEvent args)
    {
        if (!TryComp<SubdermalImplantComponent>(target, out var implanted))
            return;

        if (implanted.ImplantedEntity == null)
            return;

        // Coyote
        if (!TryComp<MobStateComponent>(implanted.ImplantedEntity, out var mobstate)
            || mobstate.CurrentState == MobState.Alive)
            return;


        // Gets location of the implant
        var ownerXform = Transform(target);
        var pos = ownerXform.MapPosition;
        var x = (int)pos.X;
        var y = (int)pos.Y;
        var posText = $"({x}, {y})";

        // Frontier: Gets station location of the implant
        var station = _station.GetOwningStation(target);
        var stationText = station is null ? null : $"{Name(station.Value)} ";

        if (stationText == null)
            stationText = "";

        // Frontier: Gets species of the implant user
        var speciesText = $"";
        if (TryComp<HumanoidProfileComponent>(implanted.ImplantedEntity, out var species)) // Aurora's Song - Nubody
        {

            if (HasComp<ReplicantComponent>(implanted.ImplantedEntity)) // AS: Replika
            {
                speciesText = $" ({Loc.GetString("species-name-replicant", ("species", species!.Species))})";  // AS: Replika
            }
            else
            {
                speciesText = $" ({species!.Species})";
            }
        }
        // Begin Aurora's Song: Death Times
        var deathTime = "";
        if(mobstate.CurrentState == MobState.Dead)
        {
            if(ent.Comp.DeathTime == TimeSpan.Zero)
                ent.Comp.DeathTime = _timing.CurTime;

            TimeSpan deltaTime = _timing.CurTime - ent.Comp.DeathTime;
            deathTime = deltaTime.ToString("mm\\:ss");
        } // End Aurora's Song

        // Start Coyote
        string localeKey = ent.Comp.Messages[mobstate.CurrentState];

        var message = Loc.GetString(
            localeKey,
            ("user", implanted.ImplantedEntity.Value),
            ("specie", speciesText),
            ("grid", stationText!),
            ("position", posText),
            ("deathtime", deathTime)); // Aurora's Song: Death Times

        _radioSystem.SendRadioMessage(
            target,
            message,
            _prototypeManager.Index<RadioChannelPrototype>(ent.Comp.RadioChannel),
            target);
        // End Coyote

        ent.Comp.NextTrigger = _timing.CurTime + ent.Comp.RetriggerDelay; // Aurora's Song: Implant retriggering
        args.Handled = true;
    }

    public override void Update(float frameTime) // Aurora's Song: Handles retriggering implants when needed, and resetting any timers
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ImplantedComponent, MobStateComponent>();
        while (query.MoveNext(out var mobUid, out var implants, out var state))
        {
            bool retrigger = false;
            foreach (var implant in implants.ImplantContainer.ContainedEntities)
            {
                if(!TryComp<RattleOnTriggerComponent>(implant, out var rattle))
                    continue;

                if(state.CurrentState == MobState.Alive && (rattle.NextTrigger != TimeSpan.Zero || rattle.DeathTime != TimeSpan.Zero)) // If they are alive, and any timers need to be reset, reset them then continue
                {
                    rattle.DeathTime = TimeSpan.Zero;
                    rattle.NextTrigger = TimeSpan.Zero;
                }
                else if (_timing.CurTime > rattle.NextTrigger && rattle.NextTrigger != TimeSpan.Zero)
                {
                    retrigger = true;
                }
            }

            if(retrigger == true) // At least one implant needs to be retriggered, so tell the body to try and retrigger all of its rattle implants.
            {                     // Unfortunantly, we can't just leave it up to the implant to reject a trigger if not enough time has elapsed, as things independent of the timer want to retrigger them too.
                var deathrattleEvent = new ReTriggerRattleImplantEvent(mobUid, state.CurrentState);
                RaiseLocalEvent(mobUid, deathrattleEvent);
            }
        }
    }
}
