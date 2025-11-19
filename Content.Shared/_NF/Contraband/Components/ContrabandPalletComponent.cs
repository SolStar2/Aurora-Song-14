using Content.Shared.Access;
using Content.Shared.Stacks;
using Content.Shared.Store;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._NF.Contraband.Components;

[RegisterComponent] // Aurora: Contraband Pallet component for contraband registration and licensing
[Access(typeof(SharedContrabandTurnInSystem))]
public sealed partial class ContrabandPalletConsoleComponent : Component
{
    // Aurora - Renamed to SleRewardCurrency to reflect the distinction between the reward for sle for receiving contraband versus the reward for the person turning in the contraband
    [ViewVariables(VVAccess.ReadWrite), DataField(serverOnly: true, customTypeSerializer: typeof(PrototypeIdSerializer<StackPrototype>))]
    public ProtoId<CurrencyPrototype> SleRewardCurrency = "FrontierUplinkCoin";

    [ViewVariables(VVAccess.ReadWrite), DataField(serverOnly: true, customTypeSerializer: typeof(PrototypeIdSerializer<StackPrototype>))]
    public ProtoId<CurrencyPrototype> TurnInRewardCurrency = "ExchangeCoin"; // SpaceCash5000 > ExchangeCoin | switched from cash to ExchangeCoin as economy experiment - Aurora

    [DataField]
    public SoundSpecifier ErrorSound = new SoundCollectionSpecifier("CargoError"); // Aurora: add deny sound

    /// <summary>
    /// Leave null for no licence required
    /// </summary>
    [DataField]
    public string? LicenseRequired = "contraband handling license";

    [ViewVariables(VVAccess.ReadWrite), DataField(serverOnly: true)]
    public string Faction = "NFSD";

    [ViewVariables(VVAccess.ReadWrite), DataField]
    public string LocStringPrefix = string.Empty;

    [ViewVariables(VVAccess.ReadWrite), DataField]
    public int PalletDistance = 8;

    [DataField]
    public Dictionary<EntProtoId, EntProtoId> RegisterRecipies = new()
    {
        {"NFWeaponPistolMk58Expedition","NFWeaponPistolMk58"},
        {"NFWeaponPistolPollockExpedition","NFWeaponPistolPollockHighCapacityMag"},
        {"NFWeaponPistolUniversalExpedition","NFWeaponPistolUniversal"},
        {"NFWeaponRevolverArgentiExpedition","NFWeaponRevolverArgenti"},
        {"NFWeaponRevolverFaithExpedition","NFWeaponRevolverFaith"},
        {"NFWeaponRevolverDeckardExpedition","NFWeaponRevolverDeckard"},
        {"NFWeaponRevolverPython45Expedition","NFWeaponRevolverPython45"},
        {"NFWeaponRifleBarlowsBoltExpedition","NFWeaponRifleBarlowsBolt"},
        {"NFWeaponRifleCeremonialExpedition","NFWeaponRifleCeremonial"},
        {"NFWeaponRifleRepeaterExpedition","NFWeaponRifleRepeater"},
        {"NFWeaponPistolCobraExpedition","NFWeaponPistolCobra"},
        {"NFWeaponPistolN1984Expedition","NFWeaponPistolN1984"},
        {"NFWeaponPistolViperExpedition","NFWeaponPistolViper"},
        {"NFWeaponRevolverFitzExpedition","NFWeaponRevolverFitz"},
        {"NFWeaponRevolverLuckyExpedition","NFWeaponRevolverLucky"},
        {"NFWeaponRevolverPirateExpedition","NFWeaponRevolverPirate"},
        {"NFWeaponRevolverWard45Expedition","NFWeaponRevolverWard45"},
        {"NFWeaponShotgunKammererExpedition","NFWeaponShotgunKammerer"},
        {"NFWeaponSubMachineGunC20rExpedition","NFWeaponSubMachineGunC20r"},
        {"NFWeaponRifleAssaultNovaliteC1Expedition","NFWeaponRifleAssaultNovaliteC1"},
        {"NFWeaponRifleAssaultJackdawExpedition","NFWeaponRifleAssaultJackdaw"},
        {"NFWeaponRifleAssaultGestioExpedition","NFWeaponRifleAssaultGestio"},
        {"NFWeaponRifleSVSExpedition","NFWeaponRifleSVS"},
        {"NFWeaponEnergyPistolLaserExpedition","NFWeaponEnergyPistolLaser"},
        {"NFWeaponRifleAssaultLecterExpedition","ASWeaponRifleAssaultSurpLecter"},
        {"NFWeaponRifleAssaultM90Expedition","NFWeaponRifleAssaultM90"},
        {"NFWeaponRifleSniperHristovExpedition","NFWeaponRifleSniperHristov"},
        {"NFWeaponRifleMusketExpedition","NFWeaponRifleMusket"},
        {"NFWeaponSubMachineGunWt550Expedition","ASWeaponSubMachineGunSurpWt550"},
        {"NFWeaponSubMachineGunDrozdExpedition","ASWeaponSubMachineGunSurpDrozd"},
        {"NFWeaponSubMachineGunAtreidesExpedition","ASWeaponSubMachineGunSurpAtreides"},
        {"NFWeaponSubMachineGunTypewriterExpedition","NFWeaponSubMachineGunTypewriter"},
        {"NFWeaponEnergyPistolLaserSvalinnExpedition","NFWeaponEnergyPistolLaserSvalinn"},
        {"NFWeaponShotgunEnforcerExpedition","NFWeaponShotgunEnforcer"},
        {"NFWeaponShotgunBulldogExpedition","NFWeaponShotgunBulldog"},
        {"NFWeaponRifleAssaultSmExpedition","ASWeaponRifleAssaultSurpSm"},
        {"NFWeaponRifleAssaultVulcanExpedition","NFWeaponRifleAssaultVulcan"},
        {"NFWeaponEnergyRifleCarbineExpedition","NFWeaponEnergyRifleCarbine"},
        {"NFWeaponEnergyPistolLaserAdvancedExpedition","NFWeaponEnergyPistolLaserAdvanced"},
        {"NFWeaponEnergyPistolLaserAntiqueExpedition","NFWeaponEnergyPistolLaserAntique"},
        {"NFWeaponEnergySubMachineGunDeltaVExpedition","NFWeaponEnergySubMachineGunDeltaV"},
        {"NFWeaponEnergyRifleSniperXrayCannonExpedition","NFWeaponEnergyRifleSniperXrayCannon"},
        {"NFWeaponEnergyRifleSniperCannonExpedition","NFWeaponEnergyRifleSniperCannon"},
        {"NFWeaponEnergyRifleTemperatureExpedition","NFWeaponEnergyRifleTemperature"},

        {"ClothingOuterHardsuitSyndie","ClothingOuterHardsuitShanlinUnpainted"},
        {"ClothingOuterHardsuitSyndieElite","ClothingOuterHardsuitShiweiUnpainted"},

        {"ClothingShoesBootsMagSyndie","ClothingShoesBootsMagSyndieRegistered"},

        {"CrateSyndicateLockedHardsuitFilled","ClothingOuterHardsuitShanlinUnpainted"},
        {"CrateSyndicateLockedEliteHardsuitFilled","ClothingOuterHardsuitShiweiUnpainted"},
    };
}
