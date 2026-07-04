using Content.Shared.CartridgeLoader;
using Robust.Shared.Serialization;

namespace Content.Shared._AS.CartridgeLoader.Cartridges;

public interface INeoNoteUiMessagePayload
{
}

// payload sent by client when a note is to be hidden from in game view.
[Serializable, NetSerializable]
public sealed class NeoNoteHideMessage(int recordId) : INeoNoteUiMessagePayload
{
    public readonly int RecordId = recordId;
}

// payload sent by client when a note is to be edited.
[Serializable, NetSerializable]
public sealed class NeoNoteSaveMessage(int recordId, string title, string body) : INeoNoteUiMessagePayload
{
    public readonly int RecordId = recordId;
    public readonly string Title = title;
    public readonly string Body = body;
}

// payload sent by client when a note is to be created.
[Serializable, NetSerializable]
public sealed class NeoNoteCreateMessage(string title, string body) : INeoNoteUiMessagePayload
{
    public readonly string Title = title;
    public readonly string Body = body;
}

// message sent by client containing a payload.
[Serializable, NetSerializable]
public sealed class NeoNoteUiMessageEvent(INeoNoteUiMessagePayload payload) : CartridgeMessageEvent
{
    public readonly INeoNoteUiMessagePayload Payload = payload;
}

// a message sent by the server when the list of notes has changed.
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
