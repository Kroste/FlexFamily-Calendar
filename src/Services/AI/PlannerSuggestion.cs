using FlexFamilyCalendar.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FlexFamilyCalendar.Services.AI;

/// <summary>
/// Strukturierter Vorschlag, den die KI in einem JSON-Codeblock liefert.
/// Aktuell unterstützt: action="add" — neuen Eintrag anlegen.
/// </summary>
public record PlannerSuggestion(
    string Action,
    DateOnly Date,
    string UserId,
    EntryType Type,
    TimeSpan Start,
    TimeSpan End,
    string? Title);

/// <summary>
/// Findet und validiert JSON-Codeblöcke in KI-Antworten. Ungültige Blöcke werden
/// stillschweigend ignoriert — der Fließtext der Antwort bleibt davon unberührt.
/// </summary>
public static class PlannerSuggestionParser
{
    private static readonly Regex JsonBlock = new(
        @"```(?:json)?\s*(\{[\s\S]*?\})\s*```",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Liest alle gültigen Vorschläge aus einer Assistant-Antwort. Reihenfolge bleibt erhalten.</summary>
    public static List<PlannerSuggestion> Extract(string? text)
    {
        var result = new List<PlannerSuggestion>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        foreach (Match m in JsonBlock.Matches(text))
        {
            if (TryParse(m.Groups[1].Value, out var s) && s is not null)
                result.Add(s);
        }
        return result;
    }

    /// <summary>Pure Validator über einen JSON-String. Public für direkte Tests.</summary>
    public static bool TryParse(string json, out PlannerSuggestion? suggestion)
    {
        suggestion = null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("action", out var actionEl)) return false;
            var action = actionEl.GetString();
            if (!string.Equals(action, "add", StringComparison.OrdinalIgnoreCase)) return false;

            if (!root.TryGetProperty("date", out var dateEl)) return false;
            if (!DateOnly.TryParseExact(dateEl.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date)) return false;

            if (!root.TryGetProperty("userId", out var userIdEl)) return false;
            var userId = userIdEl.GetString();
            if (string.IsNullOrWhiteSpace(userId)) return false;

            if (!root.TryGetProperty("type", out var typeEl)) return false;
            if (!Enum.TryParse<EntryType>(typeEl.GetString(), ignoreCase: true, out var type)) return false;

            if (!root.TryGetProperty("start", out var startEl) || !TryParseTime(startEl.GetString(), out var start)) return false;
            if (!root.TryGetProperty("end", out var endEl) || !TryParseTime(endEl.GetString(), out var end)) return false;

            string? title = null;
            if (root.TryGetProperty("title", out var titleEl) && titleEl.ValueKind == System.Text.Json.JsonValueKind.String)
                title = titleEl.GetString();

            suggestion = new PlannerSuggestion("add", date, userId!, type, start, end, title);
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
