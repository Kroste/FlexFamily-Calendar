using System.Text;
using System.Text.RegularExpressions;
using NLog;
using NLog.Config;
using NLog.LayoutRenderers;
using NLog.LayoutRenderers.Wrappers;

namespace FlexFamilyCalendar.Services;

/// <summary>
/// NLog-Layout-Renderer <c>${masked:...}</c>: rendert den inneren Layout-Text und ersetzt
/// darin alles, was nach Passwort, Token oder Connection-String-Credential aussieht.
///
/// Zweite Verteidigungslinie, keine Ausrede: Secrets gehören gar nicht erst in eine
/// Log-Message. Wenn aber jemand versehentlich ein DTO oder einen Connection-String
/// durchreicht, landet er nicht im Klartext auf der Platte.
/// </summary>
[LayoutRenderer("masked")]
[ThreadAgnostic]
public sealed class MaskingLayoutRenderer : WrapperLayoutRendererBase
{
    private const string Replacement = "***";

    // Reihenfolge egal, die Muster überschneiden sich nicht. Alle case-insensitiv.
    private static readonly Regex[] Patterns =
    [
        // JSON: "password": "geheim"  /  "token": "ey..."
        new(@"(""(?:password|passwort|pass|pwd|token|accessToken|refreshToken|apiKey|api_key|secret|jwt)""\s*:\s*"")[^""]*("")",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
        // Connection-String / Query: Password=geheim;  User Id=x;Password=y
        new(@"\b(password|pwd)\s*=\s*[^;&\s]+", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        // Authorization: Bearer ey...
        new(@"\b(Bearer|Basic)\s+[A-Za-z0-9\-._~+/=]+", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ];

    protected override string Transform(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var s = Patterns[0].Replace(text, $"$1{Replacement}$2");
        s = Patterns[1].Replace(s, m => $"{m.Groups[1].Value}={Replacement}");
        s = Patterns[2].Replace(s, m => $"{m.Groups[1].Value} {Replacement}");
        return s;
    }

    /// <summary>
    /// Registrierung über einen Modul-Initializer statt in Program.Main: der Initializer läuft
    /// beim Laden des Assemblys und damit garantiert vor dem ersten Logger. Ein Aufruf in Main
    /// deckt nur den App-Prozess ab — der Testprozess hat kein Main, und dort würde NLog das
    /// unbekannte ${masked:…} samt Message-Ende verschlucken (leere Log-Texte).
    /// </summary>
    /// <remarks>
    /// CA2255 warnt vor ModuleInitializern in Bibliotheken — hier bewusst unterdrückt: genau
    /// das ist der Punkt. Eine Registrierung im Einstiegspunkt der App würde den Testprozess
    /// nicht erreichen, und ohne registrierten Renderer verschluckt NLog das ${masked:…}
    /// samt Rest der Message.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255",
        Justification = "Muss vor dem ersten Logger laufen, auch im Testprozess ohne Main.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Register()
        => LogManager.Setup().SetupExtensions(s => s.RegisterLayoutRenderer<MaskingLayoutRenderer>("masked"));
}
