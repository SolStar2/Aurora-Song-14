using Content.Client.Overlays;
using Content.Shared._AS.License; // Aurora's Song
using Content.Shared.Access.Systems;
using Content.Shared.Mindshield.Components; // Aurora's Song
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.Access.Systems;

public sealed partial class JobStatusSystem : SharedJobStatusSystem
{
    [Dependency] private ShowJobIconsSystem _showJobIcons = default!;
    // [Dependency] private ShowCrewIconsSystem _showCrewIcons = default!; // Aurora's Song
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private LicenseSystem _license = default!; // Aurora's Song

    private static readonly ProtoId<SecurityIconPrototype> CrewBorderIcon = "CrewBorderIcon";
    private static readonly ProtoId<SecurityIconPrototype> CrewUncertainBorderIcon = "CrewUncertainBorderIcon";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<JobStatusComponent, GetStatusIconsEvent>(OnGetStatusIconsEvent);
    }

    // show the status icons if the player has the correponding HUDs
    private void OnGetStatusIconsEvent(Entity<JobStatusComponent> ent, ref GetStatusIconsEvent ev)
    {
        if (_showJobIcons.IsActive && ent.Comp.JobStatusIcon != null)
            ev.StatusIcons.Add(_prototype.Index(ent.Comp.JobStatusIcon));

        // Aurora's Song Start - Disable crew indicators because they're too similar to the license border
        /**
        if (_showCrewIcons.IsActive)
        {
            if (_showCrewIcons.UncertainCrewBorder)
                ev.StatusIcons.Add(_prototype.Index(CrewUncertainBorderIcon));
            else if (ent.Comp.IsCrew)
                ev.StatusIcons.Add(_prototype.Index(CrewBorderIcon));
        }
        */ // Aurora's Song End

        // Aurora - This is yucky gross and evil but there isnt really another way to do this while keeping my sanity.
        // Don't display if mindshield is active since mindshield roles are kinda an upgrade to a license anyway.
        if (_showJobIcons.IsActive && _license.TryGetActiveLicenses(ent, out var licenses) && !HasComp<MindShieldComponent>(ent))
        {
            // for now just grab the first licenses icon since there is no differentiation between them
            // the order of the licenses will be id slot first then hands
            var license = licenses[0];
            if (_prototype.TryIndex(license.LicenseStatusIcon, out var licenseIconPrototype))
                ev.StatusIcons.Add(licenseIconPrototype);
        }
    }
}
