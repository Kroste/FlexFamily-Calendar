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

    /// <summary>Monats-Soll als Proration des Wochen-Solls: Quota × Tage/7.</summary>
    public static double MonthlyTarget(double weeklyQuota, int daysInMonth)
        => weeklyQuota * daysInMonth / 7.0;

    /// <summary>
    /// Summe der angerechneten Stunden je UserId (Arbeit + Krank/Urlaub + Übernachtung).
    /// Übernachtungen zählen pauschal je Tag (Wert aus <paramref name="overnightHoursByUser"/>, sonst 0), nicht die volle Dauer.
    /// </summary>
    public static Dictionary<string, double> ActualHoursByUser(
        IEnumerable<CalendarEntry> entries, IReadOnlyDictionary<string, double>? overnightHoursByUser = null)
        => SumByUser(entries, EntryTypeInfo.CountsTowardHours, overnightHoursByUser);

    /// <summary>Summe der tatsächlich gearbeiteten Stunden je UserId (nur Arbeit) — fürs Arbeitszeit-Limit.</summary>
    public static Dictionary<string, double> WorkedHoursByUser(IEnumerable<CalendarEntry> entries)
        => SumByUser(entries, EntryTypeInfo.CountsAsWork, null);

    private static Dictionary<string, double> SumByUser(
        IEnumerable<CalendarEntry> entries, Func<EntryType, bool> include,
        IReadOnlyDictionary<string, double>? overnightHoursByUser)
    {
        var result = new Dictionary<string, double>();
        foreach (var e in entries)
        {
            if (!include(e.Type)) continue;
            if (string.IsNullOrEmpty(e.UserId)) continue;
            var hours = e.Type == EntryType.Overnight
                ? (overnightHoursByUser?.GetValueOrDefault(e.UserId) ?? 0)  // pauschale Tagesgutschrift
                : e.DurationHours;
            result[e.UserId] = result.GetValueOrDefault(e.UserId) + hours;
        }
        return result;
    }
}
