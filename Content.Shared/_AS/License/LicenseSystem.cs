using Content.Shared._AS.License.Components;
using Content.Shared._AS.License.Events;
using Content.Shared.Access.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Shared._AS.License;

public sealed partial class LicenseSystem : EntitySystem
{
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private LicenseSystem _license = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LicenseConsoleComponent, PrintLicenseMessage>(OnPrintLicenseMessage);
        SubscribeLocalEvent<LicenseConsoleComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<LicenseConsoleComponent, ComponentRemove>(OnRemove);
    }

    private void OnInit(Entity<LicenseConsoleComponent> ent, ref ComponentInit args)
    {
        _slots.AddItemSlot(ent, LicenseConsoleComponent.HolderIdSlotId, ent.Comp.HolderIdSlot);
    }

    private void OnRemove(Entity<LicenseConsoleComponent> ent, ref ComponentRemove args)
    {
        _slots.RemoveItemSlot(ent, ent.Comp.HolderIdSlot);
    }

    private void OnPrintLicenseMessage(Entity<LicenseConsoleComponent> ent, ref PrintLicenseMessage args)
    {
        // No license to spawn
        if (string.IsNullOrWhiteSpace(ent.Comp.LicenseName))
            return;

        // If fail notify player of the error
        if (!TryGetIdCardName(ent, out var idCardName))
        {
            _audio.PlayPredicted(ent.Comp.FailSound, Transform(ent).Coordinates, null);
            _popup.PopupEntity(Loc.GetString(ent.Comp.FailPopup), ent, PopupType.MediumCaution);
            return;
        }

        var xform = Transform(ent);
        var licenceEnt = SpawnAtPosition(ent.Comp.LicenseName, xform.Coordinates);

        _xform.SetLocalRotation(licenceEnt, Angle.Zero);

        if (!TryComp<LicenseComponent>(licenceEnt, out var license))
            return;

        _audio.PlayPredicted(ent.Comp.SuccessSound, Transform(ent).Coordinates, null);
        _license.SetName((licenceEnt, license), idCardName);
    }

    public void SetName(Entity<LicenseComponent> ent, string? owner = null, string? licenseName = null)
    {
        if (owner != null)
            ent.Comp.OwnerName = owner;

        if (licenseName != null)
            ent.Comp.LicenseName = licenseName;

        if (ent.Comp.LicenseName is not { } license)
            return;

        var newItemName = ent.Comp.OwnerName != null
            ? Loc.GetString(ent.Comp.OwnerLoc, ("owner", ent.Comp.OwnerName), ("license", license))
            : Loc.GetString(ent.Comp.NoOwnerLoc, ("license", license));
        _meta.SetEntityName(ent, newItemName);
    }

    private bool TryGetIdCardName(
        Entity<LicenseConsoleComponent> ent,
        out string? name)
    {
        name = null;
        if (_slots.GetItemOrNull(ent, LicenseConsoleComponent.HolderIdSlotId) is not { } slotItem)
            return false;
        if (!TryComp<IdCardComponent>(slotItem, out var idCard))
            return false;
        name = idCard.FullName;

        return true;
    }

    public bool TryGetActiveLicenses(EntityUid user, out List<LicenseComponent> licenses)
    {
        bool hasLicense = false;
        licenses = [];

        if (_inventory.TryGetSlotEntity(user, "id", out var slotEnt))
        {
            if (TryComp<LicenseComponent>(slotEnt, out var license))
            {
                licenses.Add(license);
                hasLicense = true;
            }

            if (_container.TryGetContainer(slotEnt.Value, "PDA-license", out var container))
            {
                foreach (var containerEnt in container.ContainedEntities)
                {
                    if (TryComp<LicenseComponent>(containerEnt, out license))
                    {
                        licenses.Add(license);
                        hasLicense = true;
                    }
                }
            }
        }

        foreach (var heldEnt in _hands.EnumerateHeld(user))
        {
            if (TryComp<LicenseComponent>(heldEnt, out var license))
            {
                licenses.Add(license);
                hasLicense = true;
            }
        }

        return hasLicense;
    }

    public bool CheckLicence(string licenseName, EntityUid user)
    {
        if (!TryGetActiveLicenses(user, out var licenses))
            return false;

        foreach (var license in licenses)
        {
            if (license.LicenseName == licenseName)
                return true;
        }
        return false;
    }
}
