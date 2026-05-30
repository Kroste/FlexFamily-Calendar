using FlexFamilyCalendar.Models;
using System.Globalization;
using System.Text;

namespace FlexFamilyCalendar.Services.AI;

/// <summary>
/// Eine schlanke Momentaufnahme der für Planungs-Chats relevanten Daten. Hält nur,
/// was die KI fürs Verständnis braucht — keine ViewModels, keine UI-Referenzen.
/// </summary>
public record PlannerContext(
    DateOnly Today,
    DateOnly WeekStart,
    IReadOnlyList<User> Users,
    IReadOnlyList<ActivityType> ActivityTypes,
    IReadOnlyList<RecurringActivity> RecurringActivities,
    IReadOnlyList<(DateOnly Date, IReadOnlyList<CalendarEntry> Entries)> Week,
    IReadOnlyList<PlannerNote> Notes);

/// <summary>
/// Rendert den <see cref="PlannerContext"/> als deutschen Klartext-Block. Das ist der
/// „Hintergrund-Prompt", den der Chat bei jeder Anfrage als System-Teil mitgibt — damit
/// das LLM Personen, Regeln und aktuellen Plan kennt.
/// </summary>
public static class PlannerContextBuilder
{
    /// <summary>Maximalanzahl an Wochen-Detail-Tagen, die der Snapshot enthält (Standard 7).</summary>
    public const int DefaultWeekDays = 7;

    public static string Render(PlannerContext ctx)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# FlexFamily Calendar — Planungs-Kontext");
        sb.AppendLine();
        sb.AppendLine("Du bist eine Planungs-Assistenz für eine Familie/Wohngruppe. Du kennst die Personen,");
        sb.AppendLine("Aktivitätskategorien, wiederkehrenden Termine und die aktuelle Wochenplanung. Vorschläge");
        sb.AppendLine("müssen Personen-Wochensoll, Mindest-Ruhezeit und Aktivitäts-Pausen respektieren.");
        sb.AppendLine();
        sb.AppendLine("## Verhalten");
        sb.AppendLine("- Antworte auf Deutsch, in vollständigen Sätzen.");
        sb.AppendLine("- Wenn dir Informationen für eine gute Empfehlung fehlen, frag konkret nach statt zu raten");
        sb.AppendLine("  (z.B. 'Soll Lars die Schicht oder Sneha?', 'Welche Tage genau?').");
        sb.AppendLine("- Wenn du einen konkreten Eintrag für den Kalender vorschlägst, schreibe ihn — zusätzlich");
        sb.AppendLine("  zum erklärenden Text — als JSON-Codeblock im exakten Schema unten. Maximal eine Aktion");
        sb.AppendLine("  pro Codeblock. Der Admin kann sie dann per Klick übernehmen.");
        sb.AppendLine();
        sb.AppendLine("## Vorschlags-Schemata");
        sb.AppendLine("Drei Aktionen sind möglich. Wähle exakt die passende — der Admin übernimmt sie per Klick.");
        sb.AppendLine();
        sb.AppendLine("Neue Schicht/Aktivität anlegen:");
        sb.AppendLine("```json");
        sb.AppendLine("{");
        sb.AppendLine("  \"action\": \"add\",");
        sb.AppendLine("  \"date\": \"YYYY-MM-DD\",");
        sb.AppendLine("  \"userId\": \"<UserId aus der Personenliste unten>\",");
        sb.AppendLine("  \"type\": \"Work\" | \"Activity\" | \"Vacation\" | \"SickLeave\" | \"Absence\" | \"Overnight\",");
        sb.AppendLine("  \"start\": \"HH:mm\",");
        sb.AppendLine("  \"end\": \"HH:mm\",");
        sb.AppendLine("  \"title\": \"optional, frei\"");
        sb.AppendLine("}");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("Bestehenden Eintrag ändern (Zeit, Person, Typ und/oder Titel):");
        sb.AppendLine("```json");
        sb.AppendLine("{");
        sb.AppendLine("  \"action\": \"update\",");
        sb.AppendLine("  \"date\": \"YYYY-MM-DD\",");
        sb.AppendLine("  \"entryId\": \"<entryId aus der Wochenliste unten>\",");
        sb.AppendLine("  \"start\": \"HH:mm\",      // optional");
        sb.AppendLine("  \"end\": \"HH:mm\",        // optional");
        sb.AppendLine("  \"userId\": \"<UserId>\",   // optional, um Schicht umzumelden");
        sb.AppendLine("  \"type\": \"Work | Activity | …\",  // optional");
        sb.AppendLine("  \"title\": \"neu, optional\"");
        sb.AppendLine("}");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("Eintrag entfernen:");
        sb.AppendLine("```json");
        sb.AppendLine("{");
        sb.AppendLine("  \"action\": \"delete\",");
        sb.AppendLine("  \"date\": \"YYYY-MM-DD\",");
        sb.AppendLine("  \"entryId\": \"<entryId aus der Wochenliste unten>\"");
        sb.AppendLine("}");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("Hinweis: Mehrere Personen dürfen sich zeitlich überlappen — Überlappung ist nur bei");
        sb.AppendLine("derselben Person ein Problem. Achte auf die persönliche Mindest-Ruhezeit. Vermeide");
        sb.AppendLine("Vorschläge, die das Wochensoll bzw. die gesetzliche Höchstarbeitszeit der Person");
        sb.AppendLine("überschreiten — die UI warnt davor, aber bitte gleich gar nicht erst dort hin planen.");
        sb.AppendLine();
        sb.AppendLine($"Heute: {ctx.Today:dd.MM.yyyy} ({ctx.Today.ToString("dddd", CultureInfo.GetCultureInfo("de-DE"))})");
        sb.AppendLine($"Aktuelle Woche beginnt am: {ctx.WeekStart:dd.MM.yyyy}");
        sb.AppendLine();

        AppendPeople(sb, ctx.Users);
        AppendActivityTypes(sb, ctx.ActivityTypes);
        AppendRecurring(sb, ctx.RecurringActivities, ctx.Users);
        AppendWeek(sb, ctx.Week, ctx.Users);
        AppendNotes(sb, ctx.Notes);

        return sb.ToString().TrimEnd();
    }

    private static void AppendPeople(StringBuilder sb, IReadOnlyList<User> users)
    {
        sb.AppendLine("## Personen");
        foreach (var u in users)
        {
            var name = string.IsNullOrEmpty(u.DisplayName) ? u.Username : u.DisplayName;
            var soll = u.WeeklyHoursQuota > 0 ? $"{u.WeeklyHoursQuota:0.#} Std./Woche" : "kein Soll";
            var ruhe = u.MinRestHours > 0 ? $"{u.MinRestHours:0.#} Std. Mindest-Ruhezeit" : "keine Ruhezeit-Prüfung";
            var max = u.MaxWeeklyHours > 0 ? $", max. {u.MaxWeeklyHours:0.#} Std./Woche" : "";
            sb.AppendLine($"- {name} (userId: {u.Id}, {u.Category}, Rolle: {u.Role}) — Soll: {soll}{max}, {ruhe}");
        }
        sb.AppendLine();
    }

    private static void AppendActivityTypes(StringBuilder sb, IReadOnlyList<ActivityType> types)
    {
        if (types.Count == 0) return;
        sb.AppendLine("## Aktivitäts-Kategorien");
        foreach (var t in types)
            sb.AppendLine($"- {t.Name}");
        sb.AppendLine();
    }

    private static void AppendRecurring(StringBuilder sb, IReadOnlyList<RecurringActivity> rules, IReadOnlyList<User> users)
    {
        if (rules.Count == 0) return;
        sb.AppendLine("## Wiederkehrende Aktivitäten");
        foreach (var r in rules)
        {
            var name = NameFor(r.UserId, users) ?? r.UserDisplayName;
            var days = r.Weekdays.OrderBy(WeekOrder)
                .Select(d => CultureInfo.GetCultureInfo("de-DE").DateTimeFormat.GetAbbreviatedDayName(d));
            var title = string.IsNullOrEmpty(r.Title) ? "(ohne Titel)" : r.Title;
            sb.AppendLine($"- {name} · {string.Join(", ", days)} {r.StartTime:hh\\:mm}–{r.EndTime:hh\\:mm} · {title}");
            if (r.SkipOnHolidays) sb.AppendLine("    (an Feiertagen entfällt)");
            foreach (var s in r.Skips.OrderBy(x => x.From))
            {
                var span = s.From == s.To ? $"{s.From:dd.MM.yyyy}" : $"{s.From:dd.MM.yyyy}–{s.To:dd.MM.yyyy}";
                var reason = string.IsNullOrWhiteSpace(s.Reason) ? "" : $" ({s.Reason})";
                sb.AppendLine($"    Pause: {span}{reason}");
            }
        }
        sb.AppendLine();
    }

    private static void AppendWeek(StringBuilder sb,
        IReadOnlyList<(DateOnly Date, IReadOnlyList<CalendarEntry> Entries)> week,
        IReadOnlyList<User> users)
    {
        if (week.Count == 0) return;
        sb.AppendLine("## Aktuelle Woche");
        foreach (var (date, entries) in week)
        {
            var day = date.ToString("dddd, dd.MM.", CultureInfo.GetCultureInfo("de-DE"));
            sb.AppendLine($"### {day}");
            if (entries.Count == 0)
            {
                sb.AppendLine("- (keine Einträge)");
                continue;
            }
            foreach (var e in entries.OrderBy(x => x.StartTime))
            {
                var name = NameFor(e.UserId, users) ?? e.UserDisplayName;
                var time = $"{e.StartTime:hh\\:mm}–{e.EndTime:hh\\:mm}";
                var label = e.Type.ToString();
                var extra = string.IsNullOrEmpty(e.Title) ? "" : $" · {e.Title}";
                sb.AppendLine($"- {name} · {time} · {label}{extra} · entryId={e.Id}");
            }
        }
        sb.AppendLine();
    }

    private static void AppendNotes(StringBuilder sb, IReadOnlyList<PlannerNote> notes)
    {
        if (notes.Count == 0) return;
        sb.AppendLine("## Vom Admin hinterlegte Hinweise");
        foreach (var n in notes)
            sb.AppendLine($"- {n.Text}");
        sb.AppendLine();
    }

    private static string? NameFor(string userId, IReadOnlyList<User> users)
    {
        var u = users.FirstOrDefault(x => x.Id == userId);
        return u is null ? null : string.IsNullOrEmpty(u.DisplayName) ? u.Username : u.DisplayName;
    }

    private static int WeekOrder(DayOfWeek d) => ((int)d + 6) % 7;
}
