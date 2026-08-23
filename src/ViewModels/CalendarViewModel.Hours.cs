using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using System.Globalization;

namespace FlexFamilyCalendar.ViewModels;

/// <summary>
/// Wochenstunden-Panel und die arbeitszeitrechtlichen Warnungen (Tageshöchstzeit,
/// Ruhezeit zwischen zwei Schichten).
/// </summary>
public partial class CalendarViewModel
{
    [RelayCommand]
    private void ToggleHoursPanel()
    {
        IsHoursPanelVisible = !IsHoursPanelVisible;
        if (IsHoursPanelVisible) RecomputeWeeklyHours();
    }

    /// <summary>Ist-Stunden je Person (Work+Au-Pair) der Woche; nur Personen mit Soll&gt;0.</summary>
    private void RecomputeWeeklyHours()
    {
        var entries = Days.SelectMany(d => d.Entries).Where(e => !e.IsRecurring).ToList();
        var actualByUser = WeeklyHoursCalculator.ActualHoursByUser(entries, _overnightHoursPerDay);
        var workedByUser = WeeklyHoursCalculator.WorkedHoursByUser(entries);
        var daysOrdered = Days.OrderBy(d => d.Date).ToList();

        WeeklyHours.Clear();
        var people = WeeklyHoursCalculator.RelevantUsers(_allUsers, CurrentUser, IsPersonalView);
        foreach (var u in people.OrderBy(u => u.DisplayName))
        {
            var actual = actualByUser.GetValueOrDefault(u.Id);
            var worked = workedByUser.GetValueOrDefault(u.Id);
            var name = string.IsNullOrEmpty(u.DisplayName) ? u.Username : u.DisplayName;
            var warnings = DailyAndRestWarnings(u, daysOrdered);
            WeeklyHours.Add(new WeeklyHoursViewModel(name, actual, u.WeeklyHoursQuota, worked, u.MaxWeeklyHours, warnings));
        }
    }

    /// <summary>Tages-Höchstarbeitszeit- und Ruhezeit-Warnungen für einen Benutzer über die sichtbare Woche.</summary>
    private static IReadOnlyList<string> DailyAndRestWarnings(User u, IReadOnlyList<CalendarDayViewModel> daysOrdered)
    {
        var summaries = daysOrdered
            .Select(d => WorkTimeRules.Summarize(d.Date, d.Entries.Where(e => e.UserId == u.Id && !e.IsRecurring)))
            .ToList();

        var warnings = new List<string>();
        var dayFmt = "ddd dd.MM.";

        foreach (var day in WorkTimeRules.OverDailyLimit(summaries, u.MaxDailyHours))
            warnings.Add($"⚠ {Localizer.Instance["Cal_OverDailyLimit"]} ({day.Date.ToString(dayFmt, CultureInfo.CurrentCulture)}): " +
                         $"{H(day.WorkedHours)} / {H(u.MaxDailyHours)} h");

        foreach (var (prev, next, restHours) in WorkTimeRules.ShortRests(summaries, u.MinRestHours))
            warnings.Add($"⚠ {Localizer.Instance["Cal_ShortRest"]} ({prev.Date.ToString(dayFmt, CultureInfo.CurrentCulture)}→" +
                         $"{next.Date.ToString(dayFmt, CultureInfo.CurrentCulture)}): {H(restHours)} / {H(u.MinRestHours)} h");

        foreach (var day in daysOrdered)
            foreach (var (first, second) in WorkTimeRules.WorkOverlaps(day.Entries.Where(e => e.UserId == u.Id && !e.IsRecurring)))
                warnings.Add($"⚠ {Localizer.Instance["Cal_Overlap"]} ({day.Date.ToString(dayFmt, CultureInfo.CurrentCulture)}): " +
                             $"{first.TimeRange} ↔ {second.TimeRange}");

        return warnings;
    }

    private static string H(double v) => v.ToString("0.#", CultureInfo.CurrentCulture);
}
