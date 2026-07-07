using System.Linq;
using System.Threading.Tasks;
using Content.Server._AS.PersistentSystems;
using Content.Server.Access.Systems;
using Content.Server.CartridgeLoader;
using Content.Shared._AS.CartridgeLoader.Cartridges;
using Content.Shared._AS.PersistentSystems;
using Content.Shared.CartridgeLoader;
using Content.Shared.PDA;
using Robust.Shared.Player;

namespace Content.Server._AS.CartridgeLoader.Cartridges;

/// <summary>
/// Server side handling of the NeoNote system. Handles messages and sends states to client and interfaces with Personal Note API.
/// </summary>
public sealed partial class NeoNoteCartridgeSystem : EntitySystem
{
    [Dependency] private PersonalRecordSystem _record = default!;
    [Dependency] private CartridgeLoaderSystem _cartridge = default!;
    [Dependency] private IdCardSystem _id = default!;
    [Dependency] private ActorSystem _actor = default!;
    [Dependency] private RecordLogging _logging = default!;
    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<NeoNoteCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<NeoNoteCartridgeComponent, CartridgeMessageEvent>(OnUiMessage);
        SubscribeLocalEvent<NeoNoteCartridgeComponent, LoaderContentsChangedEvent>(OnLoaderContentsChanged);
        _sawmill = Logger.GetSawmill("record");

        base.Initialize();
    }

    private void OnLoaderContentsChanged(Entity<NeoNoteCartridgeComponent> ent, ref LoaderContentsChangedEvent args)
    {
        // update the ui state when the id being used gets changed.
        if (args.Container.ID != PdaComponent.PdaIdSlotId)
            return;
        UpdateUiState(args.Loader);
    }

    private void OnUiReady(Entity<NeoNoteCartridgeComponent> ent, ref CartridgeUiReadyEvent args)
    {
        // update ui state when ui is ready to be opened.
        UpdateUiState(args.Loader);
    }

    private void OnUiMessage(EntityUid uid, NeoNoteCartridgeComponent component, ref CartridgeMessageEvent args)
    {
        if (args is not NeoNoteUiMessageEvent message)
            return;

        var actor = args.Actor;
        var loader = GetEntity(args.LoaderUid);

        switch (message.Payload) // reads the payload type sent and calls the appropriate handler.
        {
            case NeoNoteCreateMessage create:
                OnNeoNoteCreateMessage(create, actor, loader);
                break;
            case NeoNoteHideMessage hide:
                OnNeoNoteHideMessage(hide, actor, loader);
                break;
            case NeoNoteSaveMessage save:
                OnNeoNoteSaveMessage(save, actor, loader);
                break;
        }
    }

    /// <summary>
    /// Handles client request to create a note.
    /// </summary>
    /// <param name="create">the payload sent by the client</param>
    /// <param name="actor">the player mob who initiated the request</param>
    /// <param name="loader">the cartridge loader that the cartridge is in.</param>
    private async void OnNeoNoteCreateMessage(NeoNoteCreateMessage create, EntityUid actor, EntityUid loader)
    {
        try
        {
            if (GetProfileId(loader) is not { } profileId || _actor.GetSession(actor) is not { } session)
                return;
            await _record.CreatePersonalNote(session, profileId, create.Title, create.Body);
            await AsyncUpdateUiState(loader);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to create personal note: {e}");
        }
    }

    /// <summary>
    /// Handles client request to delete a note.
    /// </summary>
    /// <param name="hide">the payload sent by the client</param>
    /// <param name="actor">the player mob who initiated the request</param>
    /// <param name="loader">the cartridge loader that the cartridge is in.</param>
    private async void OnNeoNoteHideMessage(NeoNoteHideMessage hide, EntityUid actor, EntityUid loader)
    {
        try
        {
            if (GetProfileId(loader) is not { } profileId || _actor.GetSession(actor) is not { } session)
                return;
            await _record.HidePersonalNote(session, profileId, hide.RecordId);
            await AsyncUpdateUiState(loader);
        }
        catch (Exception)
        {
            var updateResult = new RecordUpdateResult { Status = RecordUpdateStatus.Failed };
            _logging.LogRecordUpdated(null, null, updateResult);
        }
    }

    /// <summary>
    /// Handles client request to save a note.
    /// </summary>
    /// <param name="save">the payload sent by the client</param>
    /// <param name="actor">the player mob who initiated the request</param>
    /// <param name="loader">the cartridge loader that the cartridge is in.</param>
    private async void OnNeoNoteSaveMessage(NeoNoteSaveMessage save, EntityUid actor, EntityUid loader)
    {
        try
        {
            if (GetProfileId(loader) is not { } profileId || _actor.GetSession(actor) is not { } session)
                return;
            await _record.UpdatePersonalNote(session, profileId, save.RecordId, save.Title, save.Body);
            await AsyncUpdateUiState(loader);
        }
        catch (Exception)
        {
            var updateResult = new RecordUpdateResult { Status = RecordUpdateStatus.Failed };
            _logging.LogRecordUpdated(null, null, updateResult);
        }
    }

    private async void UpdateUiState(EntityUid loader)
    {
        try
        {
            await AsyncUpdateUiState(loader);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to get personal notes: {e}");
        }
    }

    /// <summary>
    /// Retrieve the PersonalNote records for the profile id found on the card in the loader and update the NeoNote cartridge state.
    /// </summary>
    /// <param name="loader">The cartridge loader that contains the </param>
    private async Task AsyncUpdateUiState(EntityUid loader)
    {
        var notes = new List<NeoNoteEntry>();
        if (GetProfileId(loader) is { } profileId)
        {
            var entries = await _record.GetPersonalNotes(profileId);
            notes = entries.Select(n => new NeoNoteEntry(
                    n.RecordCharacterId,
                    n.Title,
                    n.Body,
                    n.RecordCharacter.CreatedAt,
                    n.RecordCharacter.LastEdit?.CreatedAt))
                .ToList();
        }
        var state = new NeoNoteUiState(notes);
        _cartridge.UpdateCartridgeUiState(loader, state);
    }

    /// <summary>
    /// Gets the profile id from a id card on an entity.
    /// </summary>
    /// <param name="ent">The entity to check for an id card.</param>
    /// <returns>The profile id found on the entity's id card if one is found.</returns>
    private int? GetProfileId(EntityUid ent)
    {
        if (!_id.TryGetIdCard(ent, out var idCard))
            return null;
        return idCard.Comp.ProfileId;
    }
}
