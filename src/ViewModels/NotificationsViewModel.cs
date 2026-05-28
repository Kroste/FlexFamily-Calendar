using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using System.Collections.ObjectModel;
using System.Globalization;

namespace FlexFamilyCalendar.ViewModels;

/// <summary>Eine Zeile in der Benachrichtigungsliste; Text wird in der Sprache des Empfängers formatiert.</summary>
public partial class NotificationItemViewModel : ViewModelBase
{
    public string Id { get; }
    public string Text { get; }
    public string TimeLabel { get; }
    public DateOnly? RelatedDate { get; }

    [ObservableProperty] private bool _isRead;

    public NotificationItemViewModel(Notification n)
    {
        Id = n.Id;
        IsRead = n.IsRead;
        TimeLabel = n.CreatedAt.ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture);
        RelatedDate = DateOnly.TryParse(n.RelatedDate, out var d) ? d : null;
        Text = Format(n.MessageKey, n.Args);
    }

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

    public ObservableCollection<NotificationItemViewModel> Items { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _loaded;

    public bool IsEmpty => Loaded && Items.Count == 0;
    public bool HasItems => Items.Count > 0;

    /// <summary>Schließt den Dialog; optionales Datum → Kalender soll zu dieser Woche springen.</summary>
    public event Action<DateOnly?>? CloseRequested;

    public NotificationsViewModel(NotificationService notifications, User user)
    {
        _notifications = notifications;
        _user = user;
        _ = LoadAsync();
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
        CloseRequested?.Invoke(item.RelatedDate);
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(null);
}
