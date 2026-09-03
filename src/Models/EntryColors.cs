using System.Globalization;

namespace FlexFamilyCalendar.Models;

/// <summary>
/// Farbe einer Plan-Kachel. Die Kachel färbt sich nach der <b>Art</b> des Eintrags, nicht nach
/// der Person: Die Plansicht ist eine Leitungssicht — gefragt ist, wer arbeitet, wer frei hat
/// und wer anderweitig unterwegs (und damit nicht verfügbar) ist. Wer die Zeile besitzt, steht
/// ohnehin links daneben.
///
/// Reihenfolge: die am Eintrag hinterlegte Farbe schlägt die Kategorie, die Kategorie schlägt
/// den Typ. So lässt sich beim Anlegen für den Einzelfall eine Farbe setzen, ohne die Kategorie
/// oder gar den Typ umzudefinieren.
/// </summary>
public static class EntryColors
{
    /// <summary>Fällt jede Auflösung aus, bleibt neutrales Grau.</summary>
    public const string Fallback = "#7F8C8D";

    /// <summary>Farbe für einen Eintragstyp.</summary>
    public static string ForType(EntryType type) => EntryTypeInfo.Color(type);

    /// <summary>
    /// Kachelfarbe eines Eintrags: <paramref name="entryColor"/> vor <paramref name="activityColor"/>
    /// vor Typ. Beide Sonderfarben greifen nur, wenn sie gesetzt UND lesbar sind —
    /// <paramref name="activityColor"/> darf nur übergeben werden, wenn der Eintrag wirklich eine
    /// Kategorie aufgelöst hat, sonst schlüge die Restfarbe eines früheren Laufs durch.
    /// </summary>
    public static string Tile(EntryType displayType, string? activityColor, string? entryColor = null)
    {
        if (IsValidHex(entryColor)) return entryColor!;
        if (IsValidHex(activityColor)) return activityColor!;
        var byType = ForType(displayType);
        return IsValidHex(byType) ? byType : Fallback;
    }

    /// <summary>
    /// Lesbare Schriftfarbe auf einer Kachel: Schwarz auf hellem, Weiß auf dunklem Grund.
    /// Entschieden wird über den WCAG-Kontrastwert gegen beide Kandidaten, nicht über eine
    /// feste Helligkeitsschwelle — sobald der Admin eigene Farben vergibt, ist jede feste
    /// Schwelle irgendwann die falsche, und eine Kachel mit unlesbarer Uhrzeit ist im Plan
    /// wertlos.
    /// </summary>
    public static string OnTile(string? tileColor)
    {
        if (!TryParseHex(tileColor, out var r, out var g, out var b)) return "#FFFFFF";

        var l = RelativeLuminance(r, g, b);
        var contrastToBlack = (l + 0.05) / 0.05;
        var contrastToWhite = 1.05 / (l + 0.05);
        return contrastToBlack >= contrastToWhite ? "#000000" : "#FFFFFF";
    }

    /// <summary>Relative Leuchtdichte nach WCAG 2.1 (sRGB linearisiert).</summary>
    private static double RelativeLuminance(byte r, byte g, byte b)
        => 0.2126 * Linearize(r) + 0.7152 * Linearize(g) + 0.0722 * Linearize(b);

    private static double Linearize(byte channel)
    {
        var c = channel / 255.0;
        return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    /// <summary>Akzeptiert <c>#RGB</c>, <c>#RRGGBB</c> und <c>#AARRGGBB</c> — genau das, was auch
    /// <c>Color.Parse</c> aus den gespeicherten Werten liest.</summary>
    public static bool IsValidHex(string? value) => TryParseHex(value, out _, out _, out _);

    private static bool TryParseHex(string? value, out byte r, out byte g, out byte b)
    {
        r = g = b = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var s = value.Trim();
        if (s[0] != '#') return false;
        s = s[1..];

        switch (s.Length)
        {
            case 3:
                return TryHex($"{s[0]}{s[0]}", out r) && TryHex($"{s[1]}{s[1]}", out g) && TryHex($"{s[2]}{s[2]}", out b);
            case 6:
                return TryHex(s[..2], out r) && TryHex(s[2..4], out g) && TryHex(s[4..], out b);
            case 8:   // AARRGGBB — der Alpha-Anteil spielt für den Kontrast keine Rolle
                return TryHex(s[2..4], out r) && TryHex(s[4..6], out g) && TryHex(s[6..], out b);
            default:
                return false;
        }
    }

    private static bool TryHex(string pair, out byte value)
        => byte.TryParse(pair, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
}
