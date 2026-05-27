namespace FlexFamilyCalendar.Services.Translation;

/// <summary>Übersetzt freie Benutzereingaben (Titel/Notizen) — Backend ist die KI-Schicht.</summary>
public interface ITranslationService
{
    Task<string> TranslateAsync(string text, string targetLanguage, CancellationToken ct = default);
}
