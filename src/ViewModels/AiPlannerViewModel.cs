using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using FlexFamilyCalendar.Services.AI;
using System.Collections.ObjectModel;

namespace FlexFamilyCalendar.ViewModels;

/// <summary>
/// KI-Planungs-Assistent (Admin): zwei Bereiche.
/// Oben: persistente „Hinweise" (PlannerNotes), die der Chat als Kontext mitbekommt.
/// Unten: ephemerer Chat-Verlauf — beim Schließen weg, der nächste Aufruf startet leer.
/// Closed-Event ohne Payload, da der Dialog nur Notes mutiert (eigene Persistierung).
/// </summary>
public partial class AiPlannerViewModel : ViewModelBase
{
    private readonly IStorageService _storage;
    private readonly AiChatService _chat;
    private readonly Func<PlannerContext> _buildContext;
    private string _contextBlock = "";

    public ObservableCollection<PlannerNoteRow> Notes { get; } = new();
    public ObservableCollection<ChatBubble> Messages { get; } = new();

    [ObservableProperty] private string _newNoteText = "";
    [ObservableProperty] private string _newMessageText = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _hasNoProvider;

    public event Action? CloseRequested;

    public AiPlannerViewModel(IStorageService storage, AiService ai, AiChatService chat,
        Func<PlannerContext> buildContext)
    {
        _storage = storage;
        _chat = chat;
        _buildContext = buildContext;
        HasNoProvider = ai.ActiveProvider is null || !ai.ActiveProvider.IsConfigured;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var notes = await _storage.LoadPlannerNotesAsync();
        foreach (var n in notes.OrderBy(x => x.CreatedAtUtc))
            Notes.Add(PlannerNoteRow.Create(n, this));
        _contextBlock = PlannerContextBuilder.Render(_buildContext() with { Notes = notes });
    }

    [RelayCommand]
    private async Task AddNoteAsync()
    {
        var text = (NewNoteText ?? "").Trim();
        if (string.IsNullOrEmpty(text)) return;
        var note = new PlannerNote { Text = text };
        Notes.Add(PlannerNoteRow.Create(note, this));
        NewNoteText = "";
        await PersistNotesAsync();
    }

    internal async Task RemoveNoteAsync(PlannerNoteRow row)
    {
        Notes.Remove(row);
        await PersistNotesAsync();
    }

    private async Task PersistNotesAsync()
    {
        var list = Notes.Select(r => new PlannerNote { Id = r.Id, Text = r.Text, CreatedAtUtc = r.CreatedAtUtc }).ToList();
        await _storage.SavePlannerNotesAsync(list);
        _contextBlock = PlannerContextBuilder.Render(_buildContext() with { Notes = list });
        LogService.UserAction("Admin", $"KI-Hinweise aktualisiert ({list.Count})");
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        var msg = (NewMessageText ?? "").Trim();
        if (string.IsNullOrEmpty(msg) || IsBusy) return;
        NewMessageText = "";
        Messages.Add(new ChatBubble(ChatRole.User, msg));
        IsBusy = true;
        StatusMessage = Localizer.Instance["AiPlanner_Thinking"];

        var history = Messages.SkipLast(1).Select(b => new ChatMessage(b.Role, b.Text)).ToList();
        var answer = await _chat.AskAsync(_contextBlock, history, msg);

        IsBusy = false;
        StatusMessage = "";
        if (string.IsNullOrWhiteSpace(answer))
        {
            Messages.Add(new ChatBubble(ChatRole.Assistant, Localizer.Instance["AiPlanner_NoAnswer"]));
            return;
        }
        Messages.Add(new ChatBubble(ChatRole.Assistant, answer.Trim()));
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();
}

public partial class PlannerNoteRow : ObservableObject
{
    private readonly AiPlannerViewModel _parent;
    public string Id { get; }
    public string Text { get; }
    public DateTime CreatedAtUtc { get; }
    public IRelayCommand RemoveCommand { get; }

    private PlannerNoteRow(AiPlannerViewModel parent, string id, string text, DateTime createdAtUtc)
    {
        _parent = parent;
        Id = id; Text = text; CreatedAtUtc = createdAtUtc;
        RemoveCommand = new AsyncRelayCommand(() => _parent.RemoveNoteAsync(this));
    }

    public static PlannerNoteRow Create(PlannerNote n, AiPlannerViewModel parent) =>
        new(parent, n.Id, n.Text, n.CreatedAtUtc);
}

/// <summary>Ein Eintrag im Chat-Verlauf — UI-Anzeige (linke/rechte Sprechblase) leitet sich von Role ab.</summary>
public class ChatBubble
{
    public ChatRole Role { get; }
    public string Text { get; }
    public bool IsUser => Role == ChatRole.User;
    public bool IsAssistant => Role == ChatRole.Assistant;

    public ChatBubble(ChatRole role, string text) { Role = role; Text = text; }
}
