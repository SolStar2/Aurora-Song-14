using Content.Server._AS.PersistentSystems;

namespace Content.Server._AS.CartridgeLoader.Cartridges;

/// <summary>
/// Component to denote a NeoNote Cartridge.
/// </summary>
[RegisterComponent, Access(typeof(PersonalRecordSystem))]
public sealed partial class NeoNoteCartridgeComponent : Component;
