using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;

namespace Content.Server._White.Blocking;

public sealed partial class RechargeableBlockingSystem : EntitySystem
{
    [Dependency] private BatterySystem _battery = default!;
    [Dependency] private ItemToggleSystem _itemToggle = default!;
    [Dependency] private PopupSystem _popup = default!;
    // [Dependency] private PowerCellSystem _powerCell = default!; // Aurora's Song | Not currently used, possibly in future iterations with swappable power devices?

    public override void Initialize()
    {
        SubscribeLocalEvent<RechargeableBlockingComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<RechargeableBlockingComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<RechargeableBlockingComponent, ItemToggleActivateAttemptEvent>(AttemptToggle);
        SubscribeLocalEvent<RechargeableBlockingComponent, ChargeChangedEvent>(OnChargeChanged);
        SubscribeLocalEvent<RechargeableBlockingComponent, PowerCellChangedEvent>(OnPowerCellChanged);
    }

    private void OnExamined(EntityUid uid, RechargeableBlockingComponent component, ExaminedEvent args)
    {
        // Aurora's Song - overhaul this function to work with modern systems and component behaviour
        if (_battery.TryGetBatteryComponent(uid, out var entBat, out var batUid))
        {
            var charge = _battery.GetChargeLevel((batUid.Value, entBat)) * 100;
            args.PushMarkup(Loc.GetString("power-cell-component-examine-details", ("currentCharge", $"{charge:F0}")));
        }
    }

    private void OnDamageChanged(EntityUid uid, RechargeableBlockingComponent component, DamageChangedEvent args)
    {
        if (!_battery.TryGetBatteryComponent(uid, out var batteryComponent, out var batteryUid)
            || !_itemToggle.IsActivated(uid)
            || args.DamageDelta == null)
            return;

        var currentCharge = _battery.GetCharge((batteryUid.Value, batteryComponent)); // Aurora's Song | Modern way to get current charge

        var batteryUse = Math.Min(args.DamageDelta.GetTotal().Float(), currentCharge); // Aurora's Song | batteryComponent.CurrentCharge->batteryComponent.LastCharge
        _battery.TryUseCharge(batteryUid.Value, batteryUse); // Aurora's Song | Update to use proper amount of arguments, and not pass the battery component in erroneously
    }

    private void AttemptToggle(EntityUid uid, RechargeableBlockingComponent component, ref ItemToggleActivateAttemptEvent args)
    {
        // Aurora's Song Start - Rewrite function to work with modern systems, also get rid of Discharged state
        if (!_battery.TryGetBatteryComponent(uid, out var battery, out var batteryUid))
            return;
        var currentCharge = _battery.GetCharge((batteryUid.Value, battery));
        if (currentCharge < (0.45 * battery.MaxCharge))
        {
            _popup.PopupEntity(Loc.GetString("shield-low-charge-toggle-fail"), args.User ?? uid);
            args.Cancelled = true;
        }
        // Aurora's Song End - Function rewrite
    }
    private void OnChargeChanged(EntityUid uid, RechargeableBlockingComponent component, ChargeChangedEvent args)
    {
        CheckCharge(uid, component);
    }

    private void OnPowerCellChanged(EntityUid uid, RechargeableBlockingComponent component, PowerCellChangedEvent args)
    {
        CheckCharge(uid, component);
    }

    private void CheckCharge(EntityUid uid, RechargeableBlockingComponent component)
    {
        // Aurora's Song Start - basically completely rewrite this entire method to actually work with modern systems and component architecture
        if (!_battery.TryGetBatteryComponent(uid, out var battery, out var batteryUid)) // Aurora's Song | make it output the Uid so we can get the charge
            return;
        var currentCharge = _battery.GetCharge((batteryUid.Value, battery));

        if (currentCharge < 1) // Aurora's Song | batteryComponent.CurrentCharge->currentCharge
        {
            _itemToggle.TryDeactivate(uid, predicted: false);
        }
        // Aurora's Song End - rewrite function
    }
}
