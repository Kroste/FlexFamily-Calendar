using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace FlexFamilyCalendar.Localization;

public record LanguageOption(string Code, string DisplayName);

/// <summary>
/// Lädt UI-Sprachen aus eingebetteten Ressourcen (web-tauglich, ohne Dateisystem).
/// Fallback-Kette pro Schlüssel: aktuelle Sprache → Englisch → Schlüsselname.
///
/// Die Live-Umschaltung läuft über <see cref="LocalizedString"/>: beim Sprachwechsel feuert
/// jeder gecachte Wrapper ein reguläres PropertyChanged. Vorher lief das über ein
/// <c>PropertyChanged("Item[]")</c> auf dem Indexer — die WPF-Konvention, die Avalonia 12 nur
/// unzuverlässig verarbeitet: Fenster ohne Fokus blieben in der alten Sprache stehen.
/// </summary>
public sealed class Localizer
{
    public static Localizer Instance { get; } = new();

    public const string FallbackLanguage = "en";
    public const string BaseLanguage = "de";

    private readonly Dictionary<string, Dictionary<string, string>> _cache = new();
    private Dictionary<string, string> _current = new();
    private Dictionary<string, string> _fallback = new();

    public string CurrentLanguage { get; private set; } = BaseLanguage;

    /// <summary>
    /// Auswahlliste für die Sprach-ComboBox. Die Länderflagge als Emoji ist Kroste-Standard;
    /// Englisch bekommt die UK-Flagge als international neutrales Symbol.
    /// </summary>
    public IReadOnlyList<LanguageOption> AvailableLanguages { get; } =
    [
        new("de", "🇩🇪 Deutsch"),
        new("en", "🇬🇧 English"),
    ];

    /// <summary>Für ViewModels, die bei Sprachwechsel selbst gebaute Texte neu erzeugen müssen.</summary>
    public event EventHandler? LanguageChanged;

    private Localizer() => SetLanguage(BaseLanguage);

    /// <summary>Übersetzter Text zum Schlüssel; bei fehlendem Schlüssel: Fallback-Sprache, dann Key.</summary>
    public string this[string key] =>
        _current.TryGetValue(key, out var v) ? v
        : _fallback.TryGetValue(key, out var f) ? f
        : key;

    public void SetLanguage(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) code = BaseLanguage;
        if (AvailableLanguages.All(l => l.Code != code)) code = BaseLanguage;

        _current = Load(code);
        _fallback = code == FallbackLanguage ? _current : Load(FallbackLanguage);
        CurrentLanguage = code;

        var culture = CultureInfo.GetCultureInfo(code);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        // Alle gebundenen Wrapper aktualisieren — erreicht jedes Fenster, nicht nur das aktive.
        LocalizedString.NotifyAllChanged();
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private Dictionary<string, string> Load(string code)
    {
        if (_cache.TryGetValue(code, out var cached)) return cached;

        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        var asm = typeof(Localizer).Assembly;
        var resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith($"i18n.{code}.json", StringComparison.OrdinalIgnoreCase));

        if (resourceName != null)
        {
            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                       ?? new(StringComparer.Ordinal);
            }
        }

        _cache[code] = dict;
        return dict;
    }
}
