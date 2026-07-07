using System.Threading.Tasks;
using Content.Server.Database;
using Content.Server.GameTicking.Events;
using Content.Shared._AS.PersistentSystems;
using Robust.Shared.Player;

namespace Content.Server._AS.PersistentSystems;

/// <summary>
/// Handles interfacing with PersonalNotes with logging.
/// </summary>
public sealed partial class PersonalRecordSystem : EntitySystem
{
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private RecordLogging _logging = default!;
    private int _roundId;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundStartingEvent>(ev => _roundId = ev.Id);
    }

    /// <summary>
    /// Retrieve the list of personal notes for a given character.
    /// </summary>
    /// <param name="profileId">the profile id corresponding to a character whos notes are to be retrieved.</param>
    /// <returns>The list of PersonalNotes for the character.</returns>
    public async Task<List<RecordPersonalNote>> GetPersonalNotes(int profileId)
    {
        return await _db.GetPersonalNotes(profileId);
    }

    /// <summary>
    /// Creates a new PersonalRecord containing the note's contents associated to the provided profile id.
    /// </summary>
    /// <param name="session">The session corresponding to the real player authoring the note for auditing.</param>
    /// <param name="profileId">The profile id corresponding to the character to associate the note to.</param>
    /// <param name="title">the title of the note to be created.</param>
    /// <param name="body">the body of the note to be created.</param>
    public async Task CreatePersonalNote(ICommonSession session, int profileId, string title, string body)
    {
        if (title == string.Empty)
            title = "Untitled";
        var newRecord = await _db.AddPersonalNote(session.UserId, profileId, title, body, _roundId);
        _logging.LogPersonalNoteCreated(newRecord, session);
    }

    /// <summary>
    /// Updates a PersonalRecord with new contents.
    /// </summary>
    /// <param name="session">The session corresponding to the real player authoring the note for auditing.</param>
    /// <param name="profileId">The profile id corresponding to the character authoring the edit for auditing.</param>
    /// <param name="recordId">The Record to update.</param>
    /// <param name="title">The new title to populate the record with. null leaves title as is.</param>
    /// <param name="body">The new body to populate the record with. null leaves body as is.</param>
    /// <returns>The result of the update.</returns>
    public async Task<RecordUpdateResult> UpdatePersonalNote(ICommonSession session, int profileId, int recordId, string? title, string? body)
    {
        var result = await _db.UpdatePersonalNote(session.UserId, profileId, recordId, title, body);
        _logging.LogRecordUpdated(session, recordId, result);
        if (result.Status == RecordUpdateStatus.NotFound)
            await CreatePersonalNote(session, profileId, title ?? "", body ?? "");
        return result;
    }

    /// <summary>
    /// Hides a PersonalRecord.
    /// </summary>
    /// <param name="session">The session corresponding to the real player authoring the note for auditing.</param>
    /// <param name="profileId">The profile id corresponding to the character authoring the edit for auditing.</param>
    /// <param name="recordId">The record to hide.</param>
    /// <returns>The result of the update.</returns>
    public async Task<RecordUpdateStatus> HidePersonalNote(ICommonSession session, int? profileId, int recordId)
    {
        var result = await _db.HideRecord(recordId, session.UserId, profileId);
        _logging.LogRecordHidden(session, recordId, result);
        return result;
    }
}
