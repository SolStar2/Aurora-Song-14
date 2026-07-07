using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Database;
using Content.Shared._AS.PersistentSystems;
using Content.Shared.Database;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._AS.PersistentSystems;

public sealed partial class RecordLogging : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        _sawmill = Logger.GetSawmill("record");

        base.Initialize();
    }

    private void LogRecordCreated(ICommonSession session, RecordCharacter record, string adminLogDetails, LogImpact impact = LogImpact.Medium)
    {
        _sawmill.Info($"Record {record.RecordType}:{record.Id} created.");
        _adminLog.Add(LogType.RecordCreate, impact,
            $"{session:Player} created {record.RecordType}: {record.Id}.\n{adminLogDetails}");
    }

    public void LogPersonalNoteCreated(RecordPersonalNote record, ICommonSession session)
    {
        LogRecordCreated(session, record.RecordCharacter, $"Title: {record.Title}\nBody: {record.Body}");
    }

    public void LogRecordUpdated(ICommonSession? session, int? recordId, RecordUpdateResult result)
    {
        switch (result.Status)
        {
            case RecordUpdateStatus.NoChange:
                _sawmill.Info($"Record {recordId} updated but no changes were made.");
                break;

            case RecordUpdateStatus.Updated:
                _sawmill.Info($"Record {recordId} updated.");
                var editText = string.Join("\n", result.Edits?.Select(e =>
                    $"({e.UpdateId}) {e.Field}: {e.OldValue} -> {e.NewValue}") ?? []);

                _adminLog.Add(LogType.RecordEdit, LogImpact.Medium,
                    $"{session:Player} updated record {recordId}.\n{editText}");
                break;

            case RecordUpdateStatus.Prohibited:
                _sawmill.Warning($"Attempted to update record {recordId} but it was prohibited.");
                _adminLog.Add(LogType.RecordEdit, LogImpact.High,
                    $"{session:Player} attempted to update record {recordId} as  but it was prohibited.");
                break;

            case RecordUpdateStatus.NotFound:
                _sawmill.Error($"Attempted update on non-existent record {recordId}.");
                break;

            case RecordUpdateStatus.Failed:
                _sawmill.Error($"Failed to update record {recordId}.");
                break;

            default:
                _sawmill.Error($"Unhandled record update status {result.Status}.");
                break;
        }
    }

    public void LogRecordHidden(ICommonSession session, int? recordId, RecordUpdateStatus status)
    {
        switch (status)
        {
            case RecordUpdateStatus.Updated:
                _sawmill.Info($"Record {recordId} was hidden.");
                _adminLog.Add(LogType.RecordHide, LogImpact.Medium,
                    $"{session:Player} hid record {recordId}.");
                break;

            case RecordUpdateStatus.NoChange:
                _sawmill.Warning($"Attempted to hide record {recordId} but it was already hidden.");
                _adminLog.Add(LogType.RecordEdit, LogImpact.Low,
                    $"{session:Player} attempted to hide record {recordId} but it was already hidden.");
                break;

            case RecordUpdateStatus.Prohibited:
                _sawmill.Warning($"Attempted to hide record {recordId} but it was prohibited.");
                _adminLog.Add(LogType.RecordEdit, LogImpact.High,
                    $"{session:Player} attempted to hide record {recordId} but it was prohibited.");
                break;

            case RecordUpdateStatus.NotFound:
                _sawmill.Error($"Attempted hide on non-existent record {recordId}.");
                break;

            case RecordUpdateStatus.Failed:
                _sawmill.Error($"Failed to hide record {recordId}.");
                break;

            default:
                _sawmill.Error($"Unhandled record update status {status}.");
                break;
        }
    }

    public void LogRecordUnhidden(ICommonSession session, int? recordId, RecordUpdateStatus status)
    {
        switch (status)
        {
            case RecordUpdateStatus.Updated:
                _sawmill.Info($"Record {recordId} was unhidden.");
                _adminLog.Add(LogType.RecordHide, LogImpact.Medium,
                    $"{session:Player} unhid record {recordId}.");
                break;

            case RecordUpdateStatus.NoChange:
                _sawmill.Warning($"Attempted to unhide record {recordId} but it was not hidden.");
                _adminLog.Add(LogType.RecordEdit, LogImpact.Low,
                    $"{session:Player} attempted to unhide record {recordId} but it was not hidden.");
                break;

            case RecordUpdateStatus.Prohibited:
                _sawmill.Warning($"Attempted to unhide record {recordId} but it was prohibited.");
                _adminLog.Add(LogType.RecordEdit, LogImpact.Extreme,
                    $"{session:Player} attempted to unhide record {recordId} but it was prohibited.");
                break;

            case RecordUpdateStatus.NotFound:
                _sawmill.Error($"Attempted unhide on non-existent record {recordId}.");
                break;

            case RecordUpdateStatus.Failed:
                _sawmill.Error($"Failed to unhide record {recordId}.");
                break;

            default:
                _sawmill.Error($"Unhandled record update status {status}.");
                break;
        }
    }

    public void LogRecordDeleted(ICommonSession session, int? recordId, RecordUpdateStatus status)
    {
        switch (status)
        {
            case RecordUpdateStatus.Updated:
                _sawmill.Info($"Record {recordId} was deleted.");
                _adminLog.Add(LogType.RecordHide, LogImpact.High,
                    $"{session:Player} deleted record {recordId}.");
                break;

            case RecordUpdateStatus.NoChange:
                _sawmill.Warning($"Attempted to delete record {recordId} but it was already deleted.");
                break;

            case RecordUpdateStatus.Prohibited:
                _sawmill.Warning($"Attempted to delete record {recordId} but it was prohibited.");
                _adminLog.Add(LogType.RecordEdit, LogImpact.Extreme,
                    $"{session:Player} attempted to delete record {recordId} but it was prohibited.");
                break;

            case RecordUpdateStatus.NotFound:
                _sawmill.Error($"Attempted delete on non-existent record {recordId}.");
                break;

            case RecordUpdateStatus.Failed:
                _sawmill.Error($"Failed to delete record {recordId}.");
                break;

            default:
                _sawmill.Error($"Unhandled record update status {status}.");
                break;
        }
    }

    public void LogRecordRestored(ICommonSession session, int? recordId, RecordUpdateStatus status)
    {
        switch (status)
        {
            case RecordUpdateStatus.Updated:
                _sawmill.Info($"Record {recordId} was restored.");
                _adminLog.Add(LogType.RecordHide, LogImpact.High,
                    $"{session:Player} restored record {recordId}.");
                break;

            case RecordUpdateStatus.NoChange:
                _sawmill.Warning($"Attempted to restore record {recordId} but it wasn't deleted.");
                break;

            case RecordUpdateStatus.Prohibited:
                _sawmill.Warning($"Attempted to restore record {recordId} but it was prohibited.");
                _adminLog.Add(LogType.RecordEdit, LogImpact.Extreme,
                    $"{session:Player} attempted to delete record {recordId} but it was prohibited.");
                break;

            case RecordUpdateStatus.NotFound:
                _sawmill.Error($"Attempted restore on non-existent record {recordId}.");
                break;

            case RecordUpdateStatus.Failed:
                _sawmill.Error($"Failed to restore record {recordId}.");
                break;

            default:
                _sawmill.Error($"Unhandled record update status {status}.");
                break;
        }
    }
}
