// Originally from https://github.com/DeltaV-Station/Delta-v/pull/3875, Edited by snezshiba with permission

using Content.Shared._AS.Humanoid.Markings;
using Content.Shared.Body;

namespace Content.Server._AS.Humanoid.Markings;

public sealed partial class SnoutHelmetSystem : EntitySystem
{
    // [Dependency] private SharedVisualBodySystem _visualBody = default!; // Aurora's Song

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SnoutHelmetComponent, ApplyOrganMarkingsEvent>(OnApplyVisualOrgans);
    }

    private void OnApplyVisualOrgans(EntityUid uid, SnoutHelmetComponent component, ApplyOrganMarkingsEvent args)
    {
        if (!args.Markings.TryGetValue(component.Organ, out var markingSet))
            return;

        if (!markingSet.TryGetValue(component.Layer, out var markings))
            return;

        foreach (var marking in markings)
        {
            // Cannot figure out how to dynamically set custom race
            var markingLower = marking.MarkingId.Id.ToLower();
            if (markingLower.Contains("vulp"))
            {
                component.AlternateHelmet = "vulpkanin";
                Dirty(uid, component);
            }
            else if (markingLower.Contains("feroxi") || markingLower.Contains("synth"))
            {
                component.AlternateHelmet = "feroxi";
                Dirty(uid, component);
            }
        }
    }
}
