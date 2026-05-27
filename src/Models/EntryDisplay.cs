namespace FlexFamilyCalendar.Models;

/// <summary>
/// Reine Anzeige-Logik für Kalendereinträge: bestimmt Deckkraft und Hervorhebung
/// abhängig von Sichtmodus und Eigentümerschaft (UI-unabhängig, testbar).
/// </summary>
public static class EntryDisplay
{
    public const double OtherOpacity = 0.28;   // fremde Einträge in der Normalsicht
    public const double NonWorkOpacity = 0.55; // Nicht-Arbeit in der Planungssicht
    public const double OwnNonWorkOpacity = 0.7;

    public static (double Opacity, bool Highlighted) Resolve(EntryType type, bool isOwn, bool personalView)
    {
        var isWork = EntryTypeInfo.CountsAsWork(type);

        if (!personalView)                       // Planungssicht: alle gleich
            return (isWork ? 1.0 : NonWorkOpacity, false);

        if (isOwn)                               // Normalsicht: eigene hervorgehoben
            return (isWork ? 1.0 : OwnNonWorkOpacity, true);

        return (OtherOpacity, false);            // Normalsicht: fremde gedämpft
    }
}
