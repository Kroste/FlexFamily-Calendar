using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using System.Collections.ObjectModel;
using System.Globalization;

namespace FlexFamilyCalendar.ViewModels;

public partial class CalendarViewModel : ViewModelBase
{
    private readonly StorageService _storage;
    private List<User> _allUsers = new();

    public User CurrentUser { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WeekLabel))]
    private DateOnly _weekStart;

    public string WeekLabel
    {
        get
        {
            var kw = ISOWeek.GetWeekOfYear(WeekStart.ToDateTime(TimeOnly.MinValue));
            return $"KW {kw:D2} / {WeekStart.Year}";
        }
    }

    public ObservableCollection<CalendarDayViewModel> Days { get; } = new();

    /// <summary>date, existing (null = neu), users. Vom CalendarView-Code-Behind abonniert.</summary>
    public event Action<DateOnly, CalendarEntry?, IReadOnlyList<User>>? EntryDialogRequested;

    public CalendarViewModel(StorageService storage, User user)
    {
        _storage = storage;
        CurrentUser = user;
        _weekStart = GetMondayOfWeek(DateOnly.FromDateTime(DateTime.Today));
        RebuildDays();
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task PreviousWeekAsync()
    {
        LogService.UserAction(CurrentUser.Username, $"Navigation zurück von {WeekLabel}");
        WeekStart = WeekStart.AddDays(-7);
        RebuildDays();
        await LoadWeekAsync();
    }

    [RelayCommand]
    private async Task NextWeekAsync()
    {
        LogService.UserAction(CurrentUser.Username, $"Navigation vor von {WeekLabel}");
        WeekStart = WeekStart.AddDays(7);
        RebuildDays();
        await LoadWeekAsync();
    }

    [RelayCommand]
    private async Task GoToTodayAsync()
    {
        LogService.UserAction(CurrentUser.Username, "Navigation zur aktuellen Woche");
        WeekStart = GetMondayOfWeek(DateOnly.FromDateTime(DateTime.Today));
        RebuildDays();
        await LoadWeekAsync();
    }

    public void RequestAddEntry(DateOnly date)
    {
        var users = _allUsers.Count > 0 ? _allUsers : new List<User> { CurrentUser };
        EntryDialogRequested?.Invoke(date, null, users.AsReadOnly());
    }

    public void RequestEditEntry(DateOnly date, CalendarEntry entry)
    {
        if (CurrentUser.Role != UserRole.Admin) return;
        LogService.Click(CurrentUser.Username, $"Eintrag bearbeiten ({date:dd.MM.yyyy}, {entry.TypeLabel})");
        var users = _allUsers.Count > 0 ? _allUsers : new List<User> { CurrentUser };
        EntryDialogRequested?.Invoke(date, entry, users.AsReadOnly());
    }

    /// <summary>Speichert/löscht das Dialog-Ergebnis: ein Pfad für Neu, Edit und Delete.</summary>
    public async Task ApplyEntryResultAsync(DateOnly date, EntryDialogResult result)
    {
        var day = await _storage.LoadDayAsync(date);
        day.Entries.RemoveAll(e => e.Id == result.Entry.Id);
        if (result.Action == EntryDialogAction.Save)
            day.Entries.Add(result.Entry);
        day.Entries.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
        await _storage.SaveDayAsync(day);

        var dayVm = Days.FirstOrDefault(d => d.Date == date);
        dayVm?.LoadFromModel(day);

        var verb = result.Action == EntryDialogAction.Save ? "gespeichert" : "gelöscht";
        LogService.UserAction(CurrentUser.Username,
            $"Eintrag {verb}: {result.Entry.TypeLabel} für {result.Entry.UserDisplayName} am {date:dd.MM.yyyy}");
    }

    private void RebuildDays()
    {
        Days.Clear();
        for (int i = 0; i < 7; i++)
            Days.Add(new CalendarDayViewModel(WeekStart.AddDays(i), this));
    }

    private async Task LoadAsync()
    {
        _allUsers = await _storage.LoadUsersAsync();
        await LoadWeekAsync();
    }

    private async Task LoadWeekAsync()
    {
        LogService.Info("Lade Kalenderwoche {0}", WeekLabel);
        for (int i = 0; i < 7; i++)
        {
            var day = await _storage.LoadDayAsync(WeekStart.AddDays(i));
            Days[i].LoadFromModel(day);
        }
    }

    private static DateOnly GetMondayOfWeek(DateOnly date)
    {
        var dow = (int)date.DayOfWeek;
        return date.AddDays(-(dow == 0 ? 6 : dow - 1));
    }
}
