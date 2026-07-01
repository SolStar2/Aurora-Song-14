using Content.Client.UserInterface.Fragments;
using Content.Shared._AS.CartridgeLoader.Cartridges;
using Content.Shared.CartridgeLoader;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._AS.NeoNote.UI;

[UsedImplicitly]
public sealed partial class NeoNoteUi : UIFragment
{
    private NeoNoteUiFragment? _fragment;
    private BoundUserInterface? _userInterface;
    private List<NeoNoteEntry> _notes = new();

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }
    private int? _currentNoteId;

    private UiState _activeUi;

    private enum UiState
    {
        List,
        Edit,
        View,
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new NeoNoteUiFragment();
        _userInterface = userInterface;
        ShowList();
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not NeoNoteUiState noteState)
            return;

        _notes = noteState.Notes;
        switch (_activeUi)
        {
            case UiState.List:
                ShowList();
                break;
            case UiState.View:
                if (_currentNoteId is { } currentNoteId
                    && noteState.Notes.Exists(n => n.RecordId == currentNoteId))
                {
                    OpenNote(_currentNoteId!.Value);
                }
                else
                    ShowList();
                break;
            case UiState.Edit:
                break;
        }
    }

    private void ShowList()
    {
        _activeUi = UiState.List;
        _currentNoteId = null;

        _fragment?.RemoveAllChildren();
        var list = new NeoNoteUiFragmentList();
        list.OnCreateButtonPressed += OnCreatePressed;
        list.OnOpenButtonPressed += OnOpenPressed;
        list.OnDeleteButtonPressed += OnDeletePressed;
        list.PopulateNotes(_notes);
        _fragment?.AddChild(list);
    }

    private void OpenNote(int noteId)
    {
        _activeUi = UiState.View;
        _currentNoteId = noteId;

        _fragment?.RemoveAllChildren();
        var view = new NeoNoteUiFragmentView(_notes.Find(n => n.RecordId == noteId));
        view.OnClosePressed += OnClosePressed;
        view.OnDeletePressed += OnDeletePressed;
        view.OnEditPressed += OnEditPressed;
        _fragment?.AddChild(view);
    }

    private void EditNote(int? noteId)
    {
        _activeUi =  UiState.Edit;
        _currentNoteId = noteId;

        NeoNoteEntry? entry = null;
        if (noteId is not null)
            entry = _notes.Find(n => n.RecordId == noteId);

        _fragment?.RemoveAllChildren();
        var edit = new NeoNoteUiFragmentEdit(entry);
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

    private void SendMessage(INeoNoteUiMessagePayload msg)
    {
        _userInterface?.SendMessage(new CartridgeUiMessage(new NeoNoteUiMessageEvent(msg)));
    }
}
