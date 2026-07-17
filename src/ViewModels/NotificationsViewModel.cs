using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using FlexFamilyCalendar.Services.Api;
using System.Collections.ObjectModel;
using System.Globalization;

namespace FlexFamilyCalendar.ViewModels;

/// <summary>Ergebnis eines Benachrichtigungs-Klicks: zur Woche springen oder eine Umplanung starten.</summary>
public record NotificationResult(DateOnly? NavigateDate, string? ReplanUserId, DateOnly? ReplanDate);

/// <summary>Eine Zeile in der Benachrichtigungsliste; Text wird in der Sprache des Empfängers formatiert.</summary>
public partial class NotificationItemViewModel : ViewModelBase
{
    public string Id { get; }
    public string Text { get; }
    public string TimeLabel { get; }
    public DateOnly? RelatedDate { get; }
    public string? Action { get; }
    public string? RelatedUserId { get; }

    [ObservableProperty] private bool _isRead;

    public NotificationItemViewModel(Notification n)
    {
        Id = n.Id;
        IsRead = n.IsRead;
        TimeLabel = n.CreatedAt.ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture);
        RelatedDate = DateOnly.TryParse(n.RelatedDate, out var d) ? d : null;
        Action = n.Action;
        RelatedUserId = n.RelatedUserId;
        Text = Format(n.MessageKey, n.Args);
    }

    /// <summary>Urlaubswunsch mit angehängter Entry-Id ("approve-entry:<id>") — Approve/Reject sind sichtbar.</summary>
    public bool IsVacationRequest => Action is not null && Action.StartsWith("approve-entry:");
    public string? PendingEntryId => IsVacationRequest ? Action![("approve-entry:".Length)..] : null;

    private static string Format(string key, IReadOnlyList<string> args)
    {
        var template = Localizer.Instance[key];
        try { return args.Count > 0 ? string.Format(template, args.ToArray()) : template; }
        catch (FormatException) { return template; }
    }
}

public partial class NotificationsViewModel : ViewModelBase
{
    private readonly NotificationService _notifications;
    private readonly User _user;
    private readonly ApiClient? _api;

    public ObservableCollection<NotificationItemViewModel> Items { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _loaded;

    public bool IsEmpty => Loaded && Items.Count == 0;
    public bool HasItems => Items.Count > 0;

    /// <summary>Schließt den Dialog; das Ergebnis steuert Navigation bzw. Start einer Umplanung.</summary>
    public event Action<NotificationResult?>? CloseRequested;

    public NotificationsViewModel(NotificationService notifications, User user, ApiClient? api = null)
    {
        _notifications = notifications;
        _user = user;
        _api = api;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task ApproveVacation(NotificationItemViewModel? item)
    {
        if (item?.PendingEntryId is not { } entryId || _api is null) return;
        try
        {
            await _api.ApproveEntryAsync(entryId);
            await _notifications.MarkReadAsync(item.Id);
            Items.Remove(item);
        }
        catch (Exception ex) { LogService.Warn("Approve fehlgeschlagen: {0}", ex.Message); }
    }

    [RelayCommand]
    private async Task RejectVacation(NotificationItemViewModel? item)
    {
        if (item?.PendingEntryId is not { } entryId || _api is null) return;
        try
        {
            await _api.RejectEntryAsync(entryId);
            await _notifications.MarkReadAsync(item.Id);
            Items.Remove(item);
        }
        catch (Exception ex) { LogService.Warn("Reject fehlgeschlagen: {0}", ex.Message); }
    }

    private async Task LoadAsync()
    {
        var list = await _notifications.GetForUserAsync(_user.Id);
        Items.Clear();
        foreach (var n in list)
            Items.Add(new NotificationItemViewModel(n));
        Loaded = true;
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasItems));
    }

    [RelayCommand]
    private async Task MarkAllRead()
    {
        await _notifications.MarkAllReadAsync(_user.Id);
        foreach (var item in Items)
            item.IsRead = true;
    }

    [RelayCommand]
    private async Task Open(NotificationItemViewModel? item)
    {
        if (item == null) return;
        await _notifications.MarkReadAsync(item.Id);
        item.IsRead = true;
        CloseRequested?.Invoke(item.Action == "ReplanSick" && item.RelatedUserId != null
            ? new NotificationResult(item.RelatedDate, item.RelatedUserId, item.RelatedDate)
            : new NotificationResult(item.RelatedDate, null, null));
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(null);
}
