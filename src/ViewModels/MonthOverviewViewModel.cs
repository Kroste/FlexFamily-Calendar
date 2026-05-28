using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using System.Collections.ObjectModel;
using System.Globalization;

namespace FlexFamilyCalendar.ViewModels;

public partial class MonthOverviewViewModel : ViewModelBase
{
    private readonly StorageService _storage;
    private readonly User _currentUser;
    private readonly bool _personalView;
    private List<User> _allUsers = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MonthLabel))]
    private DateOnly _monthStart;

    public ObservableCollection<WeeklyHoursViewModel> Rows { get; } = new();

    public string MonthLabel => MonthStart.ToString("MMMM yyyy", CultureInfo.CurrentCulture);

    public MonthOverviewViewModel(StorageService storage, User currentUser, bool personalView)
    {
        _storage = storage;
        _currentUser = currentUser;
        _personalView = personalView;
        var today = DateOnly.FromDateTime(DateTime.Today);
        _monthStart = new DateOnly(today.Year, today.Month, 1);
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task PreviousMonthAsync()
    {
        MonthStart = MonthStart.AddMonths(-1);
        await LoadMonthAsync();
    }

    [RelayCommand]
    private async Task NextMonthAsync()
    {
        MonthStart = MonthStart.AddMonths(1);
        await LoadMonthAsync();
    }

    private async Task LoadAsync()
    {
        _allUsers = await _storage.LoadUsersAsync();
        await LoadMonthAsync();
    }

    private async Task LoadMonthAsync()
    {
        var daysInMonth = DateTime.DaysInMonth(MonthStart.Year, MonthStart.Month);

        var entries = new List<CalendarEntry>();
        for (int d = 1; d <= daysInMonth; d++)
        {
            var day = await _storage.LoadDayAsync(new DateOnly(MonthStart.Year, MonthStart.Month, d));
            entries.AddRange(day.Entries);
        }

        var overnight = _allUsers.ToDictionary(u => u.Id, u => u.OvernightHoursPerDay);
        var actualByUser = WeeklyHoursCalculator.ActualHoursByUser(entries, overnight);
        var people = WeeklyHoursCalculator.RelevantUsers(_allUsers, _currentUser, _personalView);

        Rows.Clear();
        foreach (var u in people.OrderBy(u => u.DisplayName))
        {
            var actual = actualByUser.GetValueOrDefault(u.Id);
            var target = WeeklyHoursCalculator.MonthlyTarget(u.WeeklyHoursQuota, daysInMonth);
            var name = string.IsNullOrEmpty(u.DisplayName) ? u.Username : u.DisplayName;
            Rows.Add(new WeeklyHoursViewModel(name, actual, target));
        }

        LogService.Info("Monatsübersicht geladen: {0}", MonthLabel);
    }
}
