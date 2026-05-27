using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace FlexFamilyCalendar.Theming;

public record ThemeVariantOption(string Id, string LabelKey);
public record AccentOption(string NameKey, string Hex);

/// <summary>Wendet Theme-Variante (System/Hell/Dunkel) + Akzentfarbe pro Benutzer live an.</summary>
public sealed class ThemeManager
{
    public static ThemeManager Instance { get; } = new();
    private ThemeManager() { }

    public IReadOnlyList<ThemeVariantOption> AvailableVariants { get; } =
    [
        new("System", "ThemeVariant_System"),
        new("Light", "ThemeVariant_Light"),
        new("Dark", "ThemeVariant_Dark"),
    ];

    public IReadOnlyList<AccentOption> AvailableAccents { get; } =
    [
        new("Accent_Blue", "#2E86C1"),
        new("Accent_Green", "#27AE60"),
        new("Accent_Violet", "#8E44AD"),
        new("Accent_Orange", "#E67E22"),
        new("Accent_Red", "#C0392B"),
        new("Accent_Teal", "#16A085"),
    ];

    public static ThemeVariant ParseVariant(string? id) => id switch
    {
        "Light" => ThemeVariant.Light,
        "Dark" => ThemeVariant.Dark,
        _ => ThemeVariant.Default,
    };

    public void Apply(string? variant, string? accentHex)
    {
        var app = Application.Current;
        if (app == null) return;

        app.RequestedThemeVariant = ParseVariant(variant);

        if (!string.IsNullOrWhiteSpace(accentHex) && Color.TryParse(accentHex, out var accent))
        {
            app.Resources["SystemAccentColor"] = accent;
            app.Resources["SystemAccentColorLight1"] = Lighten(accent, 0.25);
            app.Resources["SystemAccentColorLight2"] = Lighten(accent, 0.45);
            app.Resources["SystemAccentColorLight3"] = Lighten(accent, 0.65);
            app.Resources["SystemAccentColorDark1"] = Darken(accent, 0.20);
            app.Resources["SystemAccentColorDark2"] = Darken(accent, 0.40);
            app.Resources["SystemAccentColorDark3"] = Darken(accent, 0.55);
            app.Resources["AppAccentBrush"] = new SolidColorBrush(accent);
        }
    }

    private static Color Lighten(Color c, double f) => Color.FromArgb(
        c.A, Channel(c.R, 255, f), Channel(c.G, 255, f), Channel(c.B, 255, f));

    private static Color Darken(Color c, double f) => Color.FromArgb(
        c.A, Channel(c.R, 0, f), Channel(c.G, 0, f), Channel(c.B, 0, f));

    private static byte Channel(byte from, int towards, double f)
        => (byte)Math.Clamp(from + (towards - from) * f, 0, 255);
}
