using Content.Server._AS.PersistentSystems;

namespace Content.Server._AS.CartridgeLoader.Cartridges;

[RegisterComponent, Access(typeof(PersonalRecordSystem))]
public sealed partial class NeoNoteCartridgeComponent : Component;
