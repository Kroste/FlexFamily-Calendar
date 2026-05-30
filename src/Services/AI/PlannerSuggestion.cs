using FlexFamilyCalendar.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FlexFamilyCalendar.Services.AI;

public enum SuggestionAction { Add, Update, Delete }

/// <summary>
/// Strukturierter Vorschlag, den die KI in einem JSON-Codeblock liefert. Drei Aktionen:
/// <list type="bullet">
///   <item><b>Add</b>: neuen Eintrag anlegen (UserId, Type, Start, End, optional Title)</item>
///   <item><b>Update</b>: bestehenden Eintrag ändern (EntryId, Start, End, optional Title)</item>
///   <item><b>Delete</b>: bestehenden Eintrag entfernen (EntryId)</item>
/// </list>
/// Felder, die für eine Aktion irrelevant sind, dürfen null sein — der Parser akzeptiert das
/// nur dort, wo die Aktion das zulässt.
/// </summary>
public record PlannerSuggestion(
    SuggestionAction Action,
    DateOnly Date,
    string? EntryId,
    string? UserId,
    EntryType? Type,
    TimeSpan? Start,
    TimeSpan? End,
    string? Title);

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

            if (!root.TryGetProperty("date", out var dateEl)) return false;
            if (!DateOnly.TryParseExact(dateEl.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date)) return false;

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

            // Aktions-spezifische Pflichtfelder
            switch (action)
            {
                case SuggestionAction.Add:
                    if (string.IsNullOrWhiteSpace(userId) || type is null || start is null || end is null) return false;
                    break;
                case SuggestionAction.Update:
                    if (string.IsNullOrWhiteSpace(entryId)) return false;
                    if (start is null && end is null && title is null) return false;   // mind. ein Feld ändern
                    break;
                case SuggestionAction.Delete:
                    if (string.IsNullOrWhiteSpace(entryId)) return false;
                    break;
            }

            suggestion = new PlannerSuggestion(action, date, entryId, userId, type, start, end, title);
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
}
