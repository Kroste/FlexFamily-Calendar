namespace FlexFamilyCalendar.Models;

/// <summary>Gut unterscheidbare Personenfarben für den Kalender (Auto-Vergabe + Editor-Auswahl).</summary>
public static class UserColorPalette
{
    public static readonly IReadOnlyList<string> Colors =
    [
        "#2E86C1", // Blau
        "#E67E22", // Orange
        "#27AE60", // Grün
        "#8E44AD", // Violett
        "#C0392B", // Rot
        "#16A085", // Türkis
        "#D4AC0D", // Gold
        "#2C3E50", // Anthrazit
        "#E84393", // Pink
        "#6C5CE7", // Indigo
    ];

    /// <summary>Farbe für den n-ten Benutzer (zykliert über die Palette).</summary>
    public static string ColorAt(int index)
        => Colors[((index % Colors.Count) + Colors.Count) % Colors.Count];
}
