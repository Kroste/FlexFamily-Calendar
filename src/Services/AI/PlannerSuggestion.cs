using FlexFamilyCalendar.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FlexFamilyCalendar.Services.AI;

public enum SuggestionAction { Add, Update, Delete, Pause }

/// <summary>
/// Strukturierter Vorschlag, den die KI in einem JSON-Codeblock liefert. Vier Aktionen:
/// <list type="bullet">
///   <item><b>Add</b>: neuen Eintrag anlegen (UserId, Type, Start, End, optional Title).</item>
///   <item><b>Update</b>: bestehenden Eintrag ändern (EntryId, optional Start/End/Title/UserId/Type).</item>
///   <item><b>Delete</b>: bestehenden Eintrag entfernen (EntryId).</item>
///   <item><b>Pause</b>: tagesgenaue Pause für eine wiederkehrende Aktivität anlegen
///     (RecurringActivityId, From, To, optional Reason). Date wird auf From gespiegelt.</item>
/// </list>
/// Felder, die für eine Aktion irrelevant sind, dürfen null sein — der Parser akzeptiert das
/// nur dort, wo die Aktion das zulässt.
/// </summary>
public record PlannerSuggestion(
    SuggestionAction Action,
    DateOnly Date,
    string? EntryId = null,
    string? UserId = null,
    EntryType? Type = null,
    TimeSpan? Start = null,
    TimeSpan? End = null,
    string? Title = null,
    string? RecurringActivityId = null,
    DateOnly? From = null,
    DateOnly? To = null,
    string? Reason = null);

public static class PlannerSuggestionParser
{
    private static readonly Regex JsonBlock = new(
        @"```(?:json)?\s*(\{[\s\S]*?\})\s*```",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static List<PlannerSuggestion> Extract(string? text)
    {
        var result = new List<PlannerSuggestion>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        foreach (Match m in JsonBlock.Matches(text))
            if (TryParse(m.Groups[1].Value, out var s) && s is not null)
                result.Add(s);
        return result;
    }

    public static bool TryParse(string json, out PlannerSuggestion? suggestion)
    {
        suggestion = null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("action", out var actionEl)) return false;
            var actionStr = actionEl.GetString();
            if (!Enum.TryParse<SuggestionAction>(actionStr, ignoreCase: true, out var action)) return false;

            string? entryId = root.TryGetProperty("entryId", out var idEl) ? idEl.GetString() : null;
            string? userId = root.TryGetProperty("userId", out var uidEl) ? uidEl.GetString() : null;
            EntryType? type = null;
            if (root.TryGetProperty("type", out var tEl) && Enum.TryParse<EntryType>(tEl.GetString(), true, out var t))
                type = t;
            TimeSpan? start = null, end = null;
            if (root.TryGetProperty("start", out var sEl) && TryParseTime(sEl.GetString(), out var ts)) start = ts;
            if (root.TryGetProperty("end", out var eEl) && TryParseTime(eEl.GetString(), out var te)) end = te;
            string? title = root.TryGetProperty("title", out var titEl) && titEl.ValueKind == System.Text.Json.JsonValueKind.String
                ? titEl.GetString() : null;

            // Pause-spezifische Felder
            string? recurringId = root.TryGetProperty("recurringActivityId", out var ridEl) ? ridEl.GetString() : null;
            DateOnly? from = TryGetDate(root, "from");
            DateOnly? to = TryGetDate(root, "to");
            string? reason = root.TryGetProperty("reason", out var rEl) && rEl.ValueKind == System.Text.Json.JsonValueKind.String
                ? rEl.GetString() : null;

            // Pause hat from/to als Datums-Quelle — andere Aktionen brauchen das primäre "date".
            DateOnly date;
            if (action == SuggestionAction.Pause)
            {
                if (from is null) return false;
                date = from.Value;
            }
            else
            {
                if (!root.TryGetProperty("date", out var dateEl)) return false;
                if (!DateOnly.TryParseExact(dateEl.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out date)) return false;
            }

            // Aktions-spezifische Pflichtfelder
            switch (action)
            {
                case SuggestionAction.Add:
                    if (string.IsNullOrWhiteSpace(userId) || type is null || start is null || end is null) return false;
                    break;
                case SuggestionAction.Update:
                    if (string.IsNullOrWhiteSpace(entryId)) return false;
                    if (start is null && end is null && title is null
                        && string.IsNullOrWhiteSpace(userId) && type is null) return false;
                    break;
                case SuggestionAction.Delete:
                    if (string.IsNullOrWhiteSpace(entryId)) return false;
                    break;
                case SuggestionAction.Pause:
                    if (string.IsNullOrWhiteSpace(recurringId) || from is null || to is null) return false;
                    if (to < from) return false;   // To >= From; sonst sinnlos
                    break;
            }

            suggestion = new PlannerSuggestion(action, date, entryId, userId, type, start, end, title,
                recurringId, from, to, reason);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseTime(string? s, out TimeSpan t)
    {
        t = default;
        if (string.IsNullOrWhiteSpace(s)) return false;
        if (TimeSpan.TryParseExact(s, @"h\:mm", CultureInfo.InvariantCulture, out t)) return true;
        if (TimeSpan.TryParseExact(s, @"hh\:mm", CultureInfo.InvariantCulture, out t)) return true;
        return false;
    }

    private static DateOnly? TryGetDate(System.Text.Json.JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el)) return null;
        if (el.ValueKind != System.Text.Json.JsonValueKind.String) return null;
        return DateOnly.TryParseExact(el.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var d) ? d : null;
    }
}
