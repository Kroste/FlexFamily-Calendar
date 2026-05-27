using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services;

/// <summary>
/// Reine Prüfung der Arbeitszeit-Grenzen je Tag (Tages-Höchstarbeitszeit, Ruhezeit zwischen Tagen).
/// UI-unabhängig und testbar; nur Arbeitseinträge zählen (Krank/Urlaub sind keine Arbeitszeit).
/// </summary>
public static class WorkTimeRules
{
    /// <summary>Verdichtete Arbeitszeit eines Tages: Summe sowie erster/letzter Schicht-Rand.</summary>
    public record DaySummary(DateOnly Date, double WorkedHours, TimeSpan? FirstWorkStart, TimeSpan? LastWorkEnd);

    /// <summary>Fasst die Arbeitseinträge eines Tages zusammen.</summary>
    public static DaySummary Summarize(DateOnly date, IEnumerable<CalendarEntry> entries)
    {
        var work = entries.Where(e => EntryTypeInfo.CountsAsWork(e.Type)).ToList();
        if (work.Count == 0)
            return new DaySummary(date, 0, null, null);
        return new DaySummary(
            date,
            work.Sum(e => e.DurationHours),
            work.Min(e => e.StartTime),
            work.Max(e => e.EndTime));
    }

    /// <summary>Ruhezeit (Stunden) zwischen dem Arbeitsende eines Tages und dem Arbeitsbeginn des Folgetags.</summary>
    public static double? RestHoursBetween(DaySummary prev, DaySummary next)
    {
        if (prev.LastWorkEnd is not { } end || next.FirstWorkStart is not { } start)
            return null;
        return (24 - end.TotalHours) + start.TotalHours;
    }

    /// <summary>Tage, deren gearbeitete Zeit die Tages-Höchstarbeitszeit überschreitet (leer, wenn kein Limit).</summary>
    public static IEnumerable<DaySummary> OverDailyLimit(IEnumerable<DaySummary> days, double maxDailyHours)
        => maxDailyHours <= 0 ? [] : days.Where(d => d.WorkedHours > maxDailyHours);

    /// <summary>Aufeinanderfolgende Tage, zwischen denen die Mindest-Ruhezeit unterschritten wird.</summary>
    public static IEnumerable<(DaySummary Prev, DaySummary Next, double RestHours)> ShortRests(
        IReadOnlyList<DaySummary> orderedDays, double minRestHours)
    {
        if (minRestHours <= 0) yield break;
        for (var i = 0; i < orderedDays.Count - 1; i++)
        {
            if (RestHoursBetween(orderedDays[i], orderedDays[i + 1]) is { } rest && rest < minRestHours)
                yield return (orderedDays[i], orderedDays[i + 1], rest);
        }
    }
}
