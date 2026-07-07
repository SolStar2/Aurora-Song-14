using Content.Shared.Inventory.Events;
// using Content.Shared.Tag; // Frontier
using Content.Shared.Humanoid;
using Content.Shared._NF.Clothing.Components; // Frontier
using Content.Shared.Inventory; // Aurora's Song

namespace Content.Shared._DV.Harpy;

public sealed partial class HarpyVisualsSystem : EntitySystem
{
    // [Dependency] private TagSystem _tagSystem = default!; // Frontier
    [Dependency] private SharedHideableHumanoidLayersSystem _humanoidSystem = default!; // Aurora's Song

    //    [ValidatePrototypeId<TagPrototype>] // Frontier
    //    private const string HarpyWingsTag = "HidesHarpyWings"; // Frontier

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HarpySingerComponent, DidEquipEvent>(OnDidEquipEvent);
        SubscribeLocalEvent<HarpySingerComponent, DidUnequipEvent>(OnDidUnequipEvent);
    }

    private void OnDidEquipEvent(EntityUid uid, HarpySingerComponent component, DidEquipEvent args)
    {
        if (args.Slot == "outerClothing" && HasComp<HarpyHideWingsComponent>(args.Equipment)) // Frontier: Swap tag to comp
        {
            _humanoidSystem.SetLayerOcclusion(uid, HumanoidVisualLayers.RArmExtension, true, SlotFlags.OUTERCLOTHING); // Frontier: RArm<RArmExtension // Aurora's Song
            _humanoidSystem.SetLayerOcclusion(uid, HumanoidVisualLayers.Tail, true, SlotFlags.OUTERCLOTHING); // Aurora's Song
        }
    }

    private void OnDidUnequipEvent(EntityUid uid, HarpySingerComponent component, DidUnequipEvent args)
    {
        if (args.Slot == "outerClothing" && HasComp<HarpyHideWingsComponent>(args.Equipment)) // Frontier: Swap tag to comp
        {
            _humanoidSystem.SetLayerOcclusion(uid, HumanoidVisualLayers.RArmExtension, false, SlotFlags.OUTERCLOTHING); // Frontier: RArm<RArmExtension // Aurora's Song
            _humanoidSystem.SetLayerOcclusion(uid, HumanoidVisualLayers.Tail, false, SlotFlags.OUTERCLOTHING); // Aurora's Song
        }
    }
}
