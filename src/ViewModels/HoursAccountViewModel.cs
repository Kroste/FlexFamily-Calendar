using CommunityToolkit.Mvvm.ComponentModel;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using System.Collections.ObjectModel;
using System.Globalization;

namespace FlexFamilyCalendar.ViewModels;

/// <summary>Eine Monatszeile im Stundenkonto-Verlauf.</summary>
public class MonthBalanceRow
{
    public string MonthLabel { get; }
    public double Actual { get; }
    public double Target { get; }
    public double Difference { get; }
    public double Balance { get; }

    public MonthBalanceRow(string monthLabel, double actual, double target, double balance)
    {
        MonthLabel = monthLabel;
        Actual = actual;
        Target = target;
        Difference = actual - target;
        Balance = balance;
    }

    private static string H(double v) => v.ToString("0.#", CultureInfo.CurrentCulture);
    private static string Signed(double v) => (v >= 0 ? "+" : "−") + H(Math.Abs(v)) + " h";

    public string ActualText => H(Actual) + " h";
    public string TargetText => H(Target) + " h";
    public string DifferenceText => Signed(Difference);
    public string BalanceText => Signed(Balance);
    public string BalanceColor => Balance >= 0 ? "#27AE60" : "#C0392B";
}

public partial class HoursAccountViewModel : ViewModelBase
{
    private const int MaxMonths = 36;

    private readonly StorageService _storage;
    private readonly User _currentUser;
    private readonly bool _isAdmin;

    [ObservableProperty] private User? _selectedUser;
    [ObservableProperty] private string _currentBalanceText = "";
    [ObservableProperty] private string _currentBalanceColor = "#27AE60";

    public ObservableCollection<User> AvailableUsers { get; } = new();
    public ObservableCollection<MonthBalanceRow> Rows { get; } = new();
    public bool CanSelectUser => _isAdmin;

    public HoursAccountViewModel(StorageService storage, User currentUser, bool isAdmin)
    {
        _storage = storage;
        _currentUser = currentUser;
        _isAdmin = isAdmin;
        _ = InitAsync();
    }

    private async Task InitAsync()
    {
        var all = await _storage.LoadUsersAsync();
        foreach (var u in WeeklyHoursCalculator.RelevantUsers(all, _currentUser, personalView: !_isAdmin)
                     .OrderBy(u => u.DisplayName))
            AvailableUsers.Add(u);

        SelectedUser = AvailableUsers.FirstOrDefault();  // löst Laden aus
    }

    partial void OnSelectedUserChanged(User? value) => _ = LoadLedgerAsync();

    private async Task LoadLedgerAsync()
    {
        Rows.Clear();
        var user = SelectedUser;
        if (user == null) { CurrentBalanceText = ""; return; }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var currentMonth = new DateOnly(today.Year, today.Month, 1);

        var start = user.AccountStart.Year >= 2000
            ? new DateOnly(user.AccountStart.Year, user.AccountStart.Month, 1)
            : currentMonth.AddMonths(-11);
        var earliest = currentMonth.AddMonths(-(MaxMonths - 1));
        if (start < earliest) start = earliest;

        // Monate sammeln, je Monat Ist/Soll bestimmen
        var months = new List<(DateOnly Month, double Actual, double Target)>();
        for (var m = start; m <= currentMonth; m = m.AddMonths(1))
        {
            var daysInMonth = DateTime.DaysInMonth(m.Year, m.Month);
            var entries = new List<CalendarEntry>();
            for (int d = 1; d <= daysInMonth; d++)
                entries.AddRange((await _storage.LoadDayAsync(new DateOnly(m.Year, m.Month, d))).Entries);

            var actual = WeeklyHoursCalculator.ActualHoursByUser(entries).GetValueOrDefault(user.Id);
            var target = WeeklyHoursCalculator.MonthlyTarget(user.WeeklyHoursQuota, daysInMonth);
            months.Add((m, actual, target));
        }

        var diffs = months.Select(x => x.Actual - x.Target).ToList();
        var balances = HoursAccount.RunningBalance(user.OpeningBalanceHours, diffs);

        for (int i = 0; i < months.Count; i++)
        {
            var (month, actual, target) = months[i];
            var label = month.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
            Rows.Add(new MonthBalanceRow(label, actual, target, balances[i]));
        }

        var current = balances.Count > 0 ? balances[^1] : user.OpeningBalanceHours;
        CurrentBalanceText = (current >= 0 ? "+" : "−") + Math.Abs(current).ToString("0.#", CultureInfo.CurrentCulture) + " h";
        CurrentBalanceColor = current >= 0 ? "#27AE60" : "#C0392B";

        LogService.Info("Stundenkonto geladen: {0}", user.Username);
    }
}
