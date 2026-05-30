using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using FlexFamilyCalendar.Services.AI;
using System.Collections.ObjectModel;
using System.Globalization;

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
    private readonly Func<PlannerSuggestion, Task<bool>> _applySuggestion;
    private readonly Func<PlannerSuggestion, IReadOnlyList<SuggestionWarning>>? _validateSuggestion;
    private readonly Func<DateOnly, Task>? _jumpToDate;
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
        Func<PlannerContext> buildContext,
        Func<PlannerSuggestion, Task<bool>> applySuggestion,
        Func<PlannerSuggestion, IReadOnlyList<SuggestionWarning>>? validateSuggestion = null,
        Func<DateOnly, Task>? jumpToDate = null)
    {
        _storage = storage;
        _chat = chat;
        _buildContext = buildContext;
        _applySuggestion = applySuggestion;
        _validateSuggestion = validateSuggestion;
        _jumpToDate = jumpToDate;
        HasNoProvider = ai.ActiveProvider is null || !ai.ActiveProvider.IsConfigured;
        _ = LoadAsync();
    }

    internal IReadOnlyList<SuggestionWarning> ValidateSuggestion(PlannerSuggestion s)
        => _validateSuggestion?.Invoke(s) ?? Array.Empty<SuggestionWarning>();

    /// <summary>Sprung zur betroffenen Woche + Dialog schließen, ausgelöst per Klick auf eine Warn-Zeile.</summary>
    internal async Task JumpToDateAndCloseAsync(DateOnly date)
    {
        if (_jumpToDate is null) return;
        await _jumpToDate(date);
        CloseRequested?.Invoke();
    }

    private IReadOnlyList<User> _users = Array.Empty<User>();

    private async Task LoadAsync()
    {
        var notes = await _storage.LoadPlannerNotesAsync();
        foreach (var n in notes.OrderBy(x => x.CreatedAtUtc))
            Notes.Add(PlannerNoteRow.Create(n, this));
        var ctx = _buildContext() with { Notes = notes };
        _users = ctx.Users;
        _contextBlock = PlannerContextBuilder.Render(ctx);
    }

    /// <summary>Resolved User-Id → Anzeigename für die Vorschlag-Karte. Fallback = Id, wenn unbekannt.</summary>
    internal string ResolvePersonName(string userId)
    {
        var u = _users.FirstOrDefault(x => x.Id == userId);
        if (u is null) return userId;
        return string.IsNullOrEmpty(u.DisplayName) ? u.Username : u.DisplayName;
    }

    /// <summary>Resolved Entry-Id → Personenname über den aktuellen Wochen-Snapshot. null = nicht gefunden.</summary>
    internal string? ResolvePersonByEntry(string? entryId)
    {
        if (string.IsNullOrEmpty(entryId)) return null;
        var ctx = _buildContext();
        foreach (var (_, entries) in ctx.Week)
        {
            var e = entries.FirstOrDefault(x => x.Id == entryId);
            if (e is null) continue;
            return ResolvePersonName(e.UserId);
        }
        return null;
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
        var bubble = new ChatBubble(ChatRole.Assistant, answer.Trim());
        foreach (var s in PlannerSuggestionParser.Extract(answer))
            bubble.Suggestions.Add(SuggestionCard.Create(s, this));
        Messages.Add(bubble);
    }

    internal async Task ApplySuggestionAsync(SuggestionCard card)
    {
        if (card.IsApplied) return;
        var ok = await _applySuggestion(card.Source);
        card.SetApplied(ok ? SuggestionState.Applied : SuggestionState.Failed);
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
    public ObservableCollection<SuggestionCard> Suggestions { get; } = new();
    public bool HasSuggestions => Suggestions.Count > 0;

    public ChatBubble(ChatRole role, string text) { Role = role; Text = text; }
}

public enum SuggestionState { Pending, Applied, Failed }

public partial class SuggestionCard : ObservableObject
{
    private readonly AiPlannerViewModel _parent;
    public PlannerSuggestion Source { get; }
    public string ActionLabel => Source.Action switch
    {
        SuggestionAction.Add => Localizer.Instance["AiPlanner_ActionAdd"],
        SuggestionAction.Update => Localizer.Instance["AiPlanner_ActionUpdate"],
        SuggestionAction.Delete => Localizer.Instance["AiPlanner_ActionDelete"],
        _ => Source.Action.ToString()
    };
    public string Date => Source.Date.ToString("dddd, dd.MM.yyyy", CultureInfo.GetCultureInfo("de-DE"));
    public string Person { get; }
    public string TimeRange => Source.Start is { } s && Source.End is { } e
        ? $"{s:hh\\:mm}–{e:hh\\:mm}" : "";
    public bool HasTimeRange => !string.IsNullOrEmpty(TimeRange);
    public string TypeLabel => Source.Type?.ToString() ?? "";
    public bool HasType => Source.Type is not null;
    public string? Title => Source.Title;
    public bool HasTitle => !string.IsNullOrWhiteSpace(Source.Title);

    [ObservableProperty] private SuggestionState _state = SuggestionState.Pending;
    public bool IsApplied => State != SuggestionState.Pending;
    public bool CanApply => State == SuggestionState.Pending;
    public string StateLabel => State switch
    {
        SuggestionState.Applied => Localizer.Instance["AiPlanner_SuggestionApplied"],
        SuggestionState.Failed => Localizer.Instance["AiPlanner_SuggestionFailed"],
        _ => ""
    };

    public IRelayCommand ApplyCommand { get; }

    private SuggestionCard(AiPlannerViewModel parent, PlannerSuggestion source, string person)
    {
        _parent = parent;
        Source = source;
        Person = person;
        ApplyCommand = new AsyncRelayCommand(() => _parent.ApplySuggestionAsync(this));
    }

    public void SetApplied(SuggestionState newState)
    {
        State = newState;
        OnPropertyChanged(nameof(IsApplied));
        OnPropertyChanged(nameof(CanApply));
        OnPropertyChanged(nameof(StateLabel));
    }

    public ObservableCollection<WarningRow> WarningRows { get; } = new();
    public bool HasWarnings => WarningRows.Count > 0;

    public static SuggestionCard Create(PlannerSuggestion s, AiPlannerViewModel parent)
    {
        // Bei Add: UserId aus dem Vorschlag. Bei Update/Delete: aus dem referenzierten Entry,
        // den die Calendar-VM kennt — Fallback auf EntryId-String, wenn nicht auflösbar.
        var person = s.Action == SuggestionAction.Add && !string.IsNullOrEmpty(s.UserId)
            ? parent.ResolvePersonName(s.UserId)
            : parent.ResolvePersonByEntry(s.EntryId) ?? (s.EntryId ?? "");
        var card = new SuggestionCard(parent, s, person);
        foreach (var w in parent.ValidateSuggestion(s))
            card.WarningRows.Add(new WarningRow(parent, w));
        return card;
    }
}

/// <summary>Eine einzelne Warnung als Listenitem. Klick führt zum betroffenen Tag.</summary>
public class WarningRow
{
    public string Message { get; }
    public DateOnly JumpToDate { get; }
    public IRelayCommand GoToDateCommand { get; }

    public WarningRow(AiPlannerViewModel parent, SuggestionWarning w)
    {
        Message = w.Message;
        JumpToDate = w.JumpToDate;
        GoToDateCommand = new AsyncRelayCommand(() => parent.JumpToDateAndCloseAsync(JumpToDate));
    }
}
