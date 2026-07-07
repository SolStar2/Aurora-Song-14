using Content.Shared.Radiation.Components;

namespace Content.Server.Radiation.Systems;

public sealed partial class RadiationSystem
{
    /// <summary>
    /// Sets the slope of a <see cref="RadiationSourceComponent"/> to the passed slope.
    /// </summary>
    /// <param name="entity">Radiation source we're attempting to update</param>
    /// <param name="slope">Slope we're setting the source to.</param>
    public void SetSlope(Entity<RadiationSourceComponent?> entity, float slope)
    {
        if (!SourceQuery.Resolve(entity, ref entity.Comp, false))
            return;

        entity.Comp.Slope = slope;
    }
}
