using Content.Shared.CartridgeLoader;
using Robust.Shared.Serialization;

namespace Content.Shared._AS.CartridgeLoader.Cartridges;

public interface INeoNoteUiMessagePayload
{
}

[Serializable, NetSerializable]
public sealed class NeoNoteHideMessage(int recordId) : INeoNoteUiMessagePayload
{
    public readonly int RecordId = recordId;
}

[Serializable, NetSerializable]
public sealed class NeoNoteSaveMessage(int recordId, string title, string body) : INeoNoteUiMessagePayload
{
    public readonly int RecordId = recordId;
    public readonly string Title = title;
    public readonly string Body = body;
}

[Serializable, NetSerializable]
public sealed class NeoNoteCreateMessage(string title, string body) : INeoNoteUiMessagePayload
{
    public readonly string Title = title;
    public readonly string Body = body;
}

[Serializable, NetSerializable]
public sealed class NeoNoteUiMessageEvent(INeoNoteUiMessagePayload payload) : CartridgeMessageEvent
{
    public readonly INeoNoteUiMessagePayload Payload = payload;
}

[NetSerializable, Serializable]
public sealed class NeoNoteUiState(List<NeoNoteEntry> notes) : BoundUserInterfaceState
{
    public readonly List<NeoNoteEntry> Notes = notes;
}

[NetSerializable, Serializable]
public record struct NeoNoteEntry(int RecordId, string Title, string Body, DateTime CreatedAt, DateTime? ModifiedAt)
{
    public readonly int RecordId = RecordId;
    public readonly string Title = Title;
    public readonly string Body = Body;
    public readonly DateTime CreatedAt = CreatedAt;
    public readonly DateTime? ModifiedAt = ModifiedAt;
}
