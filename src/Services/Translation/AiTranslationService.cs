using FlexFamilyCalendar.Services.AI;

namespace FlexFamilyCalendar.Services.Translation;

/// <summary>
/// Übersetzt über die gemeinsame KI-Schicht (<see cref="AiService"/>). Solange kein KI-Provider
/// implementiert/konfiguriert ist, wird der Originaltext unverändert zurückgegeben (graceful).
/// </summary>
public class AiTranslationService : ITranslationService
{
    private readonly AiService _ai;

    public AiTranslationService(AiService ai) => _ai = ai;

    public async Task<string> TranslateAsync(string text, string targetLanguage, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        var prompt =
            $"Übersetze den folgenden Text nach Sprachcode '{targetLanguage}'. " +
            "Gib ausschließlich die Übersetzung zurück, ohne Anführungszeichen oder Erklärungen:\n\n" +
            text;

        var result = await _ai.SuggestAsync(prompt, ct);
        return string.IsNullOrWhiteSpace(result) ? text : result.Trim();
    }
}
