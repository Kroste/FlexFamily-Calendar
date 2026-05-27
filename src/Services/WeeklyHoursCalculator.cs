using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services;

/// <summary>Reine Berechnung der geleisteten Wochenstunden je Benutzer (UI-unabhängig, testbar).</summary>
public static class WeeklyHoursCalculator
{
    /// <summary>
    /// Welche Personen im Stunden-Panel erscheinen: Personalsicht → nur der aktuelle Benutzer
    /// (auch ohne Soll); Planungssicht → alle mit Soll &gt; 0.
    /// </summary>
    public static IEnumerable<User> RelevantUsers(IReadOnlyList<User> all, User current, bool personalView)
    {
        if (personalView)
            return [all.FirstOrDefault(u => u.Id == current.Id) ?? current];
        return all.Where(u => u.WeeklyHoursQuota > 0);
    }

    /// <summary>Summe der Dauer aller als Arbeit zählenden Einträge je UserId.</summary>
    public static Dictionary<string, double> ActualHoursByUser(IEnumerable<CalendarEntry> entries)
    {
        var result = new Dictionary<string, double>();
        foreach (var e in entries)
        {
            if (!EntryTypeInfo.CountsAsWork(e.Type)) continue;
            if (string.IsNullOrEmpty(e.UserId)) continue;
            result[e.UserId] = result.GetValueOrDefault(e.UserId) + e.DurationHours;
        }
        return result;
    }
}
