using Avalonia;
using Avalonia.Styling;

namespace FlexFamilyCalendar.Theming;

public record ThemeVariantOption(string Id, string LabelKey);

/// <summary>Wendet die Theme-Variante (System/Hell/Dunkel) pro Benutzer an.</summary>
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

    public static ThemeVariant ParseVariant(string? id) => id switch
    {
        "Light" => ThemeVariant.Light,
        "Dark" => ThemeVariant.Dark,
        _ => ThemeVariant.Default,
    };

    public void Apply(string? variant)
    {
        if (Application.Current is { } app)
            app.RequestedThemeVariant = ParseVariant(variant);
    }
}
