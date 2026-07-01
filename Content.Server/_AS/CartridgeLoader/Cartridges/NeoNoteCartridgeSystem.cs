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

public sealed class NeoNoteCartridgeSystem : EntitySystem
{
    [Dependency] private readonly PersonalRecordSystem _record = default!;
    [Dependency] private readonly CartridgeLoaderSystem _cartridge = default!;
    [Dependency] private readonly IdCardSystem _id = default!;
    [Dependency] private readonly ActorSystem _actor = default!;
    [Dependency] private readonly RecordLogging _logging = default!;
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
        if (args.Container.ID != PdaComponent.PdaIdSlotId)
            return;
        UpdateUiState(args.Loader);
    }

    private void OnUiReady(Entity<NeoNoteCartridgeComponent> ent, ref CartridgeUiReadyEvent args)
    {
        UpdateUiState(args.Loader);
    }

    private void OnUiMessage(EntityUid uid, NeoNoteCartridgeComponent component, ref CartridgeMessageEvent args)
    {
        if (args is not NeoNoteUiMessageEvent message)
            return;

        var actor = args.Actor;
        var loader = GetEntity(args.LoaderUid);

        switch (message.Payload)
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

    private int? GetProfileId(EntityUid ent)
    {
        if (!_id.TryGetIdCard(ent, out var idCard))
            return null;
        return idCard.Comp.ProfileId;
    }
}
