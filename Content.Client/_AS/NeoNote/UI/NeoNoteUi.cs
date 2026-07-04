using Content.Client.UserInterface.Fragments;
using Content.Shared._AS.CartridgeLoader.Cartridges;
using Content.Shared.CartridgeLoader;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._AS.NeoNote.UI;

/// <summary>
/// Handles the client side state of the neonote ui.
/// </summary>
[UsedImplicitly]
public sealed partial class NeoNoteUi : UIFragment
{
    private NeoNoteUiFragment? _fragment;
    private BoundUserInterface? _userInterface;

    // A cache of the list of notes.
    private List<NeoNoteEntry> _notes = new();

    // The currently active note. (if any)
    private int? _currentNoteId;

    // Represents the currently active screen.
    private UiState _activeUi = UiState.List;

    // Each entry represents a different screen the NeoNote app can be on.
    private enum UiState
    {
        List, // The main screen that shows a list of all your notes
        Edit, // A screen with editable fields to modify a note.
        View, // A screen displaying the contents of a note.
    }

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    // This gets run when you open up the app.
    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new NeoNoteUiFragment();
        _userInterface = userInterface;
        switch (_activeUi) // return to the screen we were last on.
        {
            case UiState.List:
                ShowList();
                break;
            case UiState.View:
                if (_currentNoteId is not null)
                    OpenNote(_currentNoteId.Value);
                else
                    ShowList(); // if note is not found we go back to the list
                break;
            case UiState.Edit:
                if (_currentNoteId is not null)
                    EditNote(_currentNoteId.Value);
                else
                    ShowList(); // if note is not found we go back to the list
                break;
            default:
                ShowList();
                break;
        }
    }

    /// <summary>
    /// called to pass a new state to the ui, updating the list of notes the client has.
    /// </summary>
    /// <param name="state">The state passed to the client. Non NeoNoteUiStates will be ignored.</param>
    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not NeoNoteUiState noteState)
            return;

        _notes = noteState.Notes;
        switch (_activeUi)
        {
            case UiState.List: // If in list view we refresh the list to reflect the new list of notes.
                ShowList();
                break;
            case UiState.View: // if in note view then we check that the note still exists and drop to list view if not.
                if (_currentNoteId is { } currentNoteId
                    && noteState.Notes.Exists(n => n.RecordId == currentNoteId))
                {
                    OpenNote(_currentNoteId!.Value);
                }
                else
                    ShowList();
                break;
            case UiState.Edit: // dont interrupt a unsaved edit
                break;
        }
    }

    /// <summary>
    /// Populate the list view.
    /// </summary>
    private void ShowList()
    {
        _activeUi = UiState.List;
        _currentNoteId = null;

        // clear and create a fresh list ui.
        _fragment?.RemoveAllChildren();
        var list = new NeoNoteUiFragmentList();

        // subscribe to the button presses.
        list.OnCreateButtonPressed += OnCreatePressed;
        list.OnOpenButtonPressed += OnOpenPressed;
        list.OnDeleteButtonPressed += OnDeletePressed;

        list.PopulateNotes(_notes);
        _fragment?.AddChild(list);
    }

    /// <summary>
    /// Populate the ui with a notes contents.
    /// </summary>
    /// <param name="noteId">note to display</param>
    private void OpenNote(int noteId)
    {
        _activeUi = UiState.View;
        _currentNoteId = noteId;

        // clear and create a fresh view ui.
        _fragment?.RemoveAllChildren();
        var view = new NeoNoteUiFragmentView(_notes.Find(n => n.RecordId == noteId));

        // subscribe to the button presses.
        view.OnClosePressed += OnClosePressed;
        view.OnDeletePressed += OnDeletePressed;
        view.OnEditPressed += OnEditPressed;

        _fragment?.AddChild(view);
    }

    /// <summary>
    /// populate the ui with a note with editable fields.
    /// </summary>
    /// <param name="noteId">note to edit</param>
    private void EditNote(int? noteId)
    {
        _activeUi =  UiState.Edit;
        _currentNoteId = noteId;

        NeoNoteEntry? entry = null;
        if (noteId is not null)
            entry = _notes.Find(n => n.RecordId == noteId); // Retrieve the note from the cache.

        // clear and create a fresh edit ui.
        _fragment?.RemoveAllChildren();
        var edit = new NeoNoteUiFragmentEdit(entry);

        // subscribe to the button presses.
        edit.OnExitPressed += OnExitPressed;
        edit.OnSavePressed += OnSavePressed;

        _fragment?.AddChild(edit);
    }

    private void OnCreatePressed()
    {
        EditNote(null);
    }

    private void OnOpenPressed(int noteId)
    {
        OpenNote(noteId);
    }

    private void OnDeletePressed(int noteId)
    {
        SendMessage(new NeoNoteHideMessage(noteId));
        ShowList();
    }

    private void OnEditPressed(int noteId)
    {
        EditNote(noteId);
    }

    private void OnClosePressed()
    {
        ShowList();
    }

    private void OnSavePressed(NeoNoteUiFragmentEdit.SaveData save)
    {
        if (save.RecordId is { } recordId && _notes.Exists(n => n.RecordId == recordId))
        {
            SendMessage(new NeoNoteSaveMessage(save.RecordId.Value,  save.Title, save.Body));
            OpenNote(save.RecordId.Value);
        }
        else
        {
            SendMessage(new NeoNoteCreateMessage(save.Title, save.Body));
            ShowList();
        }
    }

    private void OnExitPressed()
    {
        if (_currentNoteId is { } currentNoteId)
            OpenNote(currentNoteId);
        else
            ShowList();
    }

    // Send a payload to the server.
    private void SendMessage(INeoNoteUiMessagePayload msg)
    {
        _userInterface?.SendMessage(new CartridgeUiMessage(new NeoNoteUiMessageEvent(msg)));
    }
}
