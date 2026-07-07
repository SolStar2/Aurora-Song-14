// Aurora Song - Department Bonus Dispensation Machine System
// Handles periodic bonus allocations and currency dispensing

using Content.Server._NF.Bank;
using Content.Server.Popups;
using Content.Server.Stack;
using Content.Shared._AS.Bank;
using Content.Shared._NF.Bank.BUI;
using Content.Shared.Stacks;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._AS.Bank;

public sealed partial class DepartmentBonusDispensationMachineSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private BankSystem _bank = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DepartmentBonusDispensationMachineComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<DepartmentBonusDispensationMachineComponent, DepartmentBonusDispensationMachineEjectMessage>(OnEjectMessage);
    }

    private void OnStartup(EntityUid uid, DepartmentBonusDispensationMachineComponent component, ComponentStartup args)
    {
        // Set initial bonus allocation time
        if (component.NextWithdrawal == TimeSpan.Zero)
            component.NextWithdrawal = _timing.CurTime + TimeSpan.FromSeconds(component.WithdrawalInterval);

        UpdateUI(uid, component);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DepartmentBonusDispensationMachineComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (!component.Enabled)
                continue;

            // Update UI periodically for open windows (every second)
            component.UiUpdateAccumulator += frameTime;
            if (component.UiUpdateAccumulator >= 1.0f)
            {
                component.UiUpdateAccumulator = 0f;
                UpdateUI(uid, component);
            }

            if (_timing.CurTime < component.NextWithdrawal)
                continue;

            // Attempt bonus allocation
            AttemptWithdrawal(uid, component);

            // Schedule next bonus allocation
            component.NextWithdrawal = _timing.CurTime + TimeSpan.FromSeconds(component.WithdrawalInterval);
            UpdateUI(uid, component);
        }
    }

    private void AttemptWithdrawal(EntityUid uid, DepartmentBonusDispensationMachineComponent component)
    {
        // Check if storage is full
        if (component.StoredAmount >= component.MaxStoredAmount)
            return;

        // Get current department balance
        if (!_bank.TryGetBalance(component.TargetDepartment, out var currentBalance))
            return;

        var balance = currentBalance;
        if (balance <= 0)
            return;

        // Calculate bonus allocation amount (percentage of current balance)
        var withdrawAmount = (int)(balance * component.AllocationRate); // Aurora Song - Renamed from TaxRate to AllocationRate

        if (withdrawAmount <= 0)
            return;

        // Round down to nearest 10 (minimum denomination) to avoid partial currency
        withdrawAmount = (withdrawAmount / 10) * 10;

        if (withdrawAmount <= 0)
            return;

        // Ensure we don't exceed storage capacity
        var spaceLeft = component.MaxStoredAmount - component.StoredAmount;
        withdrawAmount = Math.Min(withdrawAmount, spaceLeft);

        // Round down again after capacity check to ensure multiple of 10
        withdrawAmount = (withdrawAmount / 10) * 10; // Aurora Song - Ensure result is still multiple of 10

        if (withdrawAmount <= 0)
            return;

        // Attempt to allocate from department budget
        if (_bank.TrySectorWithdraw(component.TargetDepartment, withdrawAmount, LedgerEntryType.DepartmentTax))
        {
            component.StoredAmount += withdrawAmount;

            // Play sound
            if (component.WithdrawSound != null)
                _audio.PlayPvs(component.WithdrawSound, uid);

            UpdateUI(uid, component);
        }
    }

    private void OnEjectMessage(EntityUid uid, DepartmentBonusDispensationMachineComponent component, DepartmentBonusDispensationMachineEjectMessage args)
    {
        if (component.StoredAmount <= 0)
        {
            _popup.PopupEntity("The machine is empty!", uid, args.Actor);
            return;
        }

        // Round down to nearest 10 to match what can actually be spawned
        var amountToEject = (component.StoredAmount / 10) * 10; // Aurora Song - Only dispense multiples of 10

        if (amountToEject <= 0)
        {
            _popup.PopupEntity("Not enough funds to dispense! (Minimum 10 spesos)", uid, args.Actor);
            return;
        }

        // Spawn currency
        SpawnCurrency(uid, component, amountToEject);

        // Clear storage
        component.StoredAmount = 0;

        // Play sound
        if (component.EjectSound != null)
            _audio.PlayPvs(component.EjectSound, uid);

        _popup.PopupEntity($"Dispensed {amountToEject} spesos in staff bonuses!", uid, args.Actor); // Aurora Song - Changed "spacebucks" to "spesos"

        UpdateUI(uid, component);
    }

    private void SpawnCurrency(EntityUid uid, DepartmentBonusDispensationMachineComponent component, int amount)
    {
        var xform = Transform(uid);

        // Spawn in appropriate denominations
        while (amount >= 10) // Aurora Song - Only spawn if we have at least 10 spesos (minimum denomination)
        {
            // Determine denomination to spawn
            string protoId;
            int denom;

            if (amount >= 5000)
            {
                protoId = "SpaceCash5000";
                denom = 5000;
            }
            else if (amount >= 1000)
            {
                protoId = "SpaceCash1000";
                denom = 1000;
            }
            else if (amount >= 500)
            {
                protoId = "SpaceCash500";
                denom = 500;
            }
            else if (amount >= 100)
            {
                protoId = "SpaceCash100";
                denom = 100;
            }
            else // amount >= 10, minimum denomination
            {
                protoId = "SpaceCash10";
                denom = 10;
            }

            // Calculate how many of this denomination to spawn
            var count = amount / denom;
            if (count > 0)
            {
                // Spawn the cash entity (already has default stack count = denom)
                var cashEnt = Spawn(protoId, xform.Coordinates);

                // If we need more than 1 of this denomination, adjust the stack count
                if (count > 1 && TryComp<StackComponent>(cashEnt, out var stack))
                {
                    var maxCount = _stack.GetMaxCount(stack);
                    var actualCount = Math.Min(count, maxCount);

                    // SetCount sets the total spesos value, so multiply by denomination
                    _stack.SetCount(cashEnt, actualCount * denom, stack); // Aurora Song - Multiply by denom to get total value
                    amount -= denom * actualCount;
                }
                else
                {
                    // Single bill, already has correct value from prototype
                    amount -= denom;
                }
            }
            else
            {
                // Safety: if count is 0, break to prevent infinite loop
                break;
            }
        }
    }

    private void UpdateUI(EntityUid uid, DepartmentBonusDispensationMachineComponent component)
    {
        // Get current department balance for UI display
        var currentBalance = 0;
        if (_bank.TryGetBalance(component.TargetDepartment, out var balance))
            currentBalance = balance;

        var state = new DepartmentBonusDispensationMachineBoundUserInterfaceState(
            component.TargetDepartment.ToString(),
            component.AllocationRate, // Aurora Song - Renamed from TaxRate to AllocationRate
            component.StoredAmount,
            component.MaxStoredAmount,
            component.Enabled,
            component.NextWithdrawal,
            currentBalance // Aurora Song - Pass current balance to calculate expected next withdrawal
        );

        _ui.SetUiState(uid, DepartmentBonusDispensationMachineUiKey.Key, state);
    }
}
