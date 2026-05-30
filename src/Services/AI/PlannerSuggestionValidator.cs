using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services.AI;

public enum SuggestionWarningKind { SelfOverlap, RestHoursViolation, PersonAbsent, WeeklyHoursExceeded }

public record SuggestionWarning(SuggestionWarningKind Kind, string Message);

/// <summary>
/// Prüft KI-Vorschläge gegen die aktuelle Wochenlage. Mehrere Personen dürfen gleichzeitig
/// arbeiten — die Überlappung wird nur für dieselbe Person als Konflikt gewertet. Drei Klassen:
/// <list type="bullet">
///   <item><b>SelfOverlap</b>: Person hat schon eine Schicht/Aktivität, die sich zeitlich überschneidet.</item>
///   <item><b>RestHoursViolation</b>: zwischen einer benachbarten Schicht und dieser unterschreitet die Lücke
///     <see cref="User.MinRestHours"/>.</item>
///   <item><b>PersonAbsent</b>: Person ist an dem Tag im Urlaub/krank/abwesend.</item>
/// </list>
/// Reine Funktion — keine Storage-Abhängigkeiten, voll testbar.
/// </summary>
public static class PlannerSuggestionValidator
{
    public static List<SuggestionWarning> Validate(
        PlannerSuggestion s,
        IReadOnlyList<User> users,
        IReadOnlyList<(DateOnly Date, IReadOnlyList<CalendarEntry> Entries)> week)
    {
        var warnings = new List<SuggestionWarning>();

        // Welche Person? Bei Add aus dem Vorschlag, bei Update aus dem referenzierten Eintrag
        // (oder umgemeldete neue UserId), bei Delete aus dem Bestand.
        string? userId = s.UserId;
        TimeSpan? start = s.Start;
        TimeSpan? end = s.End;
        EntryType? type = s.Type;
        CalendarEntry? existing = null;

        if (s.Action == SuggestionAction.Update || s.Action == SuggestionAction.Delete)
        {
            existing = FindEntry(week, s.EntryId);
            if (existing is null) return warnings;   // kann nicht prüfen, was nicht da ist
            userId ??= existing.UserId;
            start ??= existing.StartTime;
            end ??= existing.EndTime;
            type ??= existing.Type;
        }

        // Wochensoll-/Max-Hinweis: immer prüfen (auch Delete reduziert nur, was kein Konflikt ist).
        CheckWeeklyHours(warnings, s, existing, userId, start, end, type, users, week);

        if (s.Action == SuggestionAction.Delete) return warnings;
        if (userId is null || start is null || end is null) return warnings;

        var dayEntries = week.FirstOrDefault(d => d.Date == s.Date).Entries ?? Array.Empty<CalendarEntry>();
        var personEntries = dayEntries.Where(e => e.UserId == userId && (existing is null || e.Id != existing.Id)).ToList();

        // Abwesenheit am Tag?
        var absence = personEntries.FirstOrDefault(e => EntryTypeInfo.IsAbsence(e.Type));
        if (absence is not null)
            warnings.Add(new SuggestionWarning(SuggestionWarningKind.PersonAbsent,
                $"{NameFor(userId, users)} ist am {s.Date:dd.MM.} laut Kalender abwesend ({absence.Type})."));

        // Selbst-Überlappung am gleichen Tag
        foreach (var e in personEntries)
        {
            if (EntryTypeInfo.IsAbsence(e.Type)) continue;
            if (Overlaps(e.StartTime, e.EndTime, start.Value, end.Value))
            {
                warnings.Add(new SuggestionWarning(SuggestionWarningKind.SelfOverlap,
                    $"{NameFor(userId, users)} hat am {s.Date:dd.MM.} bereits {e.StartTime:hh\\:mm}–{e.EndTime:hh\\:mm} ({e.Type})."));
            }
        }

        // Mindest-Ruhezeit gegen Vortag/Folgetag
        var user = users.FirstOrDefault(u => u.Id == userId);
        if (user?.MinRestHours > 0)
        {
            var prev = week.FirstOrDefault(d => d.Date == s.Date.AddDays(-1)).Entries ?? Array.Empty<CalendarEntry>();
            foreach (var e in prev.Where(x => x.UserId == userId && !EntryTypeInfo.IsAbsence(x.Type)))
            {
                var endLocal = e.EndTime <= e.StartTime ? e.EndTime + TimeSpan.FromHours(24) : e.EndTime;
                var prevEndTotal = endLocal.TotalHours;
                var newStartTotal = 24 + start.Value.TotalHours;
                if (newStartTotal - prevEndTotal < user.MinRestHours)
                    warnings.Add(new SuggestionWarning(SuggestionWarningKind.RestHoursViolation,
                        $"Mindest-Ruhezeit ({user.MinRestHours:0.#} h) seit der Schicht vom Vortag unterschritten."));
            }

            var next = week.FirstOrDefault(d => d.Date == s.Date.AddDays(1)).Entries ?? Array.Empty<CalendarEntry>();
            foreach (var e in next.Where(x => x.UserId == userId && !EntryTypeInfo.IsAbsence(x.Type)))
            {
                var thisEndLocal = end.Value <= start.Value ? end.Value + TimeSpan.FromHours(24) : end.Value;
                var nextStartTotal = 24 + e.StartTime.TotalHours;
                if (nextStartTotal - thisEndLocal.TotalHours < user.MinRestHours)
                    warnings.Add(new SuggestionWarning(SuggestionWarningKind.RestHoursViolation,
                        $"Mindest-Ruhezeit ({user.MinRestHours:0.#} h) bis zur Schicht am Folgetag unterschritten."));
            }
        }

        return warnings;
    }

    private static void CheckWeeklyHours(
        List<SuggestionWarning> warnings, PlannerSuggestion s, CalendarEntry? existing,
        string? userId, TimeSpan? start, TimeSpan? end, EntryType? type,
        IReadOnlyList<User> users,
        IReadOnlyList<(DateOnly Date, IReadOnlyList<CalendarEntry> Entries)> week)
    {
        if (string.IsNullOrEmpty(userId)) return;
        var user = users.FirstOrDefault(u => u.Id == userId);
        if (user is null) return;
        if (user.WeeklyHoursQuota <= 0 && user.MaxWeeklyHours <= 0) return;

        // Aktuell geleistete Arbeitsstunden der Person in der Woche (ohne den evtl. zu ändernden/löschenden Eintrag).
        var allWeekEntries = week.SelectMany(d => d.Entries);
        var currentWeekly = allWeekEntries
            .Where(e => e.UserId == userId
                        && EntryTypeInfo.CountsAsWork(e.Type)
                        && (existing is null || e.Id != existing.Id))
            .Sum(e => e.DurationHours);

        double delta = 0;
        if (s.Action == SuggestionAction.Add || s.Action == SuggestionAction.Update)
        {
            if (type is null || !EntryTypeInfo.CountsAsWork(type.Value)) return;
            if (start is null || end is null) return;
            var duration = DurationOf(start.Value, end.Value);
            delta = duration;
        }
        // Delete: delta = 0 zu currentWeekly (existing ist bereits ausgeschlossen). Hier prüfen wir
        // nur, ob die Person unter Soll fällt — i.d.R. relevant, aber wir warnen nur bei Über-Soll.

        var projected = currentWeekly + delta;
        if (user.MaxWeeklyHours > 0 && projected > user.MaxWeeklyHours)
        {
            warnings.Add(new SuggestionWarning(SuggestionWarningKind.WeeklyHoursExceeded,
                $"{NameFor(userId, users)}: gesetzliches Wochen-Höchstmaß ({user.MaxWeeklyHours:0.#} h) wird überschritten ({projected:0.#} h)."));
        }
        else if (user.WeeklyHoursQuota > 0 && projected > user.WeeklyHoursQuota)
        {
            warnings.Add(new SuggestionWarning(SuggestionWarningKind.WeeklyHoursExceeded,
                $"{NameFor(userId, users)}: Wochensoll ({user.WeeklyHoursQuota:0.#} h) wird überschritten ({projected:0.#} h)."));
        }
    }

    private static double DurationOf(TimeSpan start, TimeSpan end)
    {
        var d = (end - start).TotalHours;
        return d > 0 ? d : d + 24;   // über Mitternacht
    }

    private static bool Overlaps(TimeSpan aStart, TimeSpan aEnd, TimeSpan bStart, TimeSpan bEnd)
    {
        var aEndN = aEnd <= aStart ? aEnd + TimeSpan.FromHours(24) : aEnd;
        var bEndN = bEnd <= bStart ? bEnd + TimeSpan.FromHours(24) : bEnd;
        return aStart < bEndN && bStart < aEndN;
    }

    private static CalendarEntry? FindEntry(IReadOnlyList<(DateOnly Date, IReadOnlyList<CalendarEntry> Entries)> week, string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var (_, entries) in week)
        {
            var e = entries.FirstOrDefault(x => x.Id == id);
            if (e is not null) return e;
        }
        return null;
    }

    private static string NameFor(string userId, IReadOnlyList<User> users)
    {
        var u = users.FirstOrDefault(x => x.Id == userId);
        if (u is null) return userId;
        return string.IsNullOrEmpty(u.DisplayName) ? u.Username : u.DisplayName;
    }
}
