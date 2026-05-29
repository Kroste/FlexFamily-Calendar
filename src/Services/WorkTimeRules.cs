using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services;

/// <summary>
/// Reine Prüfung der Arbeitszeit-Grenzen je Tag (Tages-Höchstarbeitszeit, Ruhezeit zwischen Tagen).
/// UI-unabhängig und testbar; nur Arbeitseinträge zählen (Krank/Urlaub sind keine Arbeitszeit).
/// </summary>
public static class WorkTimeRules
{
    /// <summary>Verdichtete Arbeitszeit eines Tages: Summe sowie erster/letzter Schicht-Rand.
    /// <see cref="LastWorkEnd"/> ist relativ zum <see cref="Date"/>-Start gemessen und kann
    /// &gt; 24h sein, wenn die letzte Schicht über Mitternacht ging (z.B. 20:00→06:00 → 30:00).
    /// So bleibt die Ruhezeit-Berechnung zwischen Tagen einfach.</summary>
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
            // Nacht-Schicht (EndTime ≤ StartTime, z.B. 20:00→06:00) endet erst am Folgetag —
            // EndTime + 24h, damit die Ruhezeit-Differenz korrekt bleibt.
            work.Max(e => e.CrossesMidnight ? e.EndTime + TimeSpan.FromHours(24) : e.EndTime));
    }

    /// <summary>Ruhezeit (Stunden) zwischen dem Arbeitsende eines Tages und dem Arbeitsbeginn des Folgetags.
    /// Wenn das prev-Schichtende über Mitternacht ging, ist <see cref="DaySummary.LastWorkEnd"/>
    /// &gt; 24h (siehe <see cref="Summarize"/>) — die Formel funktioniert dadurch ohne Sonderfall.</summary>
    public static double? RestHoursBetween(DaySummary prev, DaySummary next)
    {
        if (prev.LastWorkEnd is not { } end || next.FirstWorkStart is not { } start)
            return null;
        // next.FirstWorkStart liegt 24h nach Beginn von prev.Date; rest = nextStart+24 − end.
        var rest = (24 + start.TotalHours) - end.TotalHours;
        return rest >= 0 ? rest : (double?)null;   // negativ = Überlappung
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

    /// <summary>
    /// Paare sich zeitlich überschneidender Arbeitseinträge (Doppelbelegung an einem Tag).
    /// Nur Arbeit zählt — Krank/Urlaub überspannen oft den ganzen Tag und sind keine echte Kollision.
    /// </summary>
    public static IReadOnlyList<(CalendarEntry First, CalendarEntry Second)> WorkOverlaps(IEnumerable<CalendarEntry> entries)
    {
        var work = entries.Where(e => EntryTypeInfo.CountsAsWork(e.Type))
                          .OrderBy(e => e.StartTime).ToList();
        var pairs = new List<(CalendarEntry, CalendarEntry)>();
        for (var i = 0; i < work.Count; i++)
            for (var j = i + 1; j < work.Count; j++)
                if (work[i].StartTime < work[j].EndTime && work[j].StartTime < work[i].EndTime)
                    pairs.Add((work[i], work[j]));
        return pairs;
    }
}
