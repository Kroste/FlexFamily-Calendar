using System.Globalization;
using System.Text;
using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services;

/// <summary>
/// Reine, testbare Logik der Krankmeldungs-Umplanung: ermittelt gültige Ersatzkandidaten für eine
/// ausgefallene Arbeitsschicht und baut einen anonymisierten KI-Prompt. UI-/Persistenz-unabhängig.
/// </summary>
public static class ReplanEngine
{
    public record ReplanCandidate(User User, double WeekWorkedHours, double WeeklyTarget);

    /// <summary>
    /// Kandidaten (Employee/AuPair, ≠ kranke Person), die die <paramref name="absentShift"/> übernehmen
    /// könnten: kein Zeitkonflikt am Tag, unter Tages-/Wochenlimit. Sortiert nach geringster Wochenauslastung.
    /// </summary>
    public static IReadOnlyList<ReplanCandidate> FindCandidates(
        CalendarEntry absentShift, DateOnly date,
        IReadOnlyList<User> users, string absentUserId,
        IReadOnlyList<(DateOnly Date, IReadOnlyList<CalendarEntry> Entries)> week)
    {
        var allWeekEntries = week.SelectMany(d => d.Entries).ToList();
        var workedByUser = WeeklyHoursCalculator.WorkedHoursByUser(allWeekEntries);
        var dayEntries = week.FirstOrDefault(d => d.Date == date).Entries ?? Array.Empty<CalendarEntry>();
        var shiftHours = absentShift.DurationHours;

        var result = new List<ReplanCandidate>();
        foreach (var u in users)
        {
            if (u.Id == absentUserId) continue;
            if (u.Category is not (PersonCategory.Employee or PersonCategory.AuPair)) continue;

            var candidateDay = dayEntries.Where(e => e.UserId == u.Id).ToList();
            // Zeitkonflikt: würde der Kandidat mit der übernommenen Schicht überlappen?
            if (WorkTimeRules.WorkOverlaps(candidateDay.Append(absentShift)).Count > 0) continue;

            var dayWorked = candidateDay.Where(e => EntryTypeInfo.CountsAsWork(e.Type)).Sum(e => e.DurationHours);
            if (u.MaxDailyHours > 0 && dayWorked + shiftHours > u.MaxDailyHours) continue;

            var weekWorked = workedByUser.GetValueOrDefault(u.Id);
            if (u.MaxWeeklyHours > 0 && weekWorked + shiftHours > u.MaxWeeklyHours) continue;

            result.Add(new ReplanCandidate(u, weekWorked, u.WeeklyHoursQuota));
        }

        return result.OrderBy(c => c.WeekWorkedHours).ToList();
    }

    /// <summary>Anonymisierter Prompt (Kandidaten als A/B/C mit Auslastung, keine Klarnamen).</summary>
    public static string BuildPrompt(CalendarEntry absentShift, DateOnly date, IReadOnlyList<ReplanCandidate> candidates)
    {
        var c = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine($"Eine Arbeitsschicht am {date:dd.MM.yyyy} von {absentShift.StartTime:hh\\:mm} bis " +
                      $"{absentShift.EndTime:hh\\:mm} ({absentShift.DurationHours.ToString("0.#", c)} h) ist " +
                      "wegen einer Krankmeldung ausgefallen und muss neu besetzt werden.");
        sb.AppendLine("Verfügbare Kandidaten (anonymisiert):");
        for (var i = 0; i < candidates.Count; i++)
        {
            var letter = (char)('A' + i);
            var cand = candidates[i];
            var soll = cand.WeeklyTarget > 0 ? $"{cand.WeeklyTarget.ToString("0.#", c)} h Wochen-Soll" : "kein Wochen-Soll";
            sb.AppendLine($"{letter}: diese Woche bereits {cand.WeekWorkedHours.ToString("0.#", c)} h gearbeitet, {soll}.");
        }
        sb.AppendLine("Welcher Kandidat sollte die Schicht übernehmen? Nenne den Buchstaben und begründe kurz (höchstens zwei Sätze).");
        return sb.ToString();
    }
}
