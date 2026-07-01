using System.Threading.Tasks;
using Content.Server.Database;
using Content.Server.GameTicking.Events;
using Content.Shared._AS.PersistentSystems;
using Robust.Shared.Player;

namespace Content.Server._AS.PersistentSystems;

public sealed class PersonalRecordSystem : EntitySystem
{
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly RecordLogging _logging = default!;
    private int _roundId;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundStartingEvent>(ev => _roundId = ev.Id);
    }

    public async Task<List<RecordPersonalNote>> GetPersonalNotes(int profileId)
    {
        return await _db.GetPersonalNotes(profileId);
    }

    public async Task CreatePersonalNote(ICommonSession session, int profileId, string title, string body)
    {
        if (title == string.Empty)
            title = "Untitled";
        var newRecord = await _db.AddPersonalNote(session.UserId, profileId, title, body, _roundId);
        _logging.LogPersonalNoteCreated(newRecord, session);
    }

    public async Task<RecordUpdateResult> UpdatePersonalNote(ICommonSession session, int profileId, int recordId, string? title, string? body)
    {
        var result = await _db.UpdatePersonalNote(session.UserId, profileId, recordId, title, body);
        _logging.LogRecordUpdated(session, recordId, result);
        if (result.Status == RecordUpdateStatus.NotFound)
            await CreatePersonalNote(session, profileId, title ?? "", body ?? "");
        return result;
    }

    public async Task<RecordUpdateStatus> HidePersonalNote(ICommonSession session, int? profileId, int recordId)
    {
        var result = await _db.HideRecord(recordId, session.UserId, profileId);
        _logging.LogRecordHidden(session, recordId, result);
        return result;
    }
}
