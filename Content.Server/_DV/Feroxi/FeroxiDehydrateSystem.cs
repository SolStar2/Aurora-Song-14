using Content.Shared.Body; // Aurora's Song
using Content.Shared.Body.Components;
using Content.Shared.Metabolism;
using Content.Shared.Nutrition.Components;

namespace Content.Server._DV.Feroxi;

public sealed partial class FeroxiDehydrateSystem : EntitySystem
{
    [Dependency] private BodySystem _body = default!;

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<FeroxiDehydrateComponent, ThirstComponent>();

        while (query.MoveNext(out var uid, out var feroxiDehydrate, out var thirst))
        {
            var currentThirst = thirst.CurrentThirst;
            var shouldBeDehydrated = currentThirst <= feroxiDehydrate.DehydrationThreshold;

            if (feroxiDehydrate.Dehydrated != shouldBeDehydrated)
            {
                UpdateDehydrationStatus((uid, feroxiDehydrate), shouldBeDehydrated);
            }
        }
    }

    /// <summary>
    /// Checks and changes the lungs when meeting the threshold for a swap of metabolizer
    /// </summary>
    /// <param name="ent"></param>
    /// <param name="shouldBeDehydrated"></param>
    private void UpdateDehydrationStatus(Entity<FeroxiDehydrateComponent> ent, bool shouldBeDehydrated)
    {
        ent.Comp.Dehydrated = shouldBeDehydrated;

        // Aurora's Song - Convert to nubody-ish
        if (!_body.TryGetOrgansWithComponent<LungComponent>(ent.Owner, out var entities))
            return;

        foreach (var entity in entities) // Aurora's Song - Convert to nubody-ish
        {
            if (!TryComp<MetabolizerComponent>(entity, out var metabolizer) || metabolizer.MetabolizerTypes == null)
            {
                continue;
            }
            //Changing the metabolizer to the appropriate value based
            var newMetabolizer = shouldBeDehydrated ? ent.Comp.DehydratedMetabolizer : ent.Comp.HydratedMetabolizer;
            metabolizer.MetabolizerTypes!.Clear();
            metabolizer.MetabolizerTypes.Add(newMetabolizer);
        }
    }
}
