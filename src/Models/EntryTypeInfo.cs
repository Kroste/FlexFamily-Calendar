namespace FlexFamilyCalendar.Models;

/// <summary>Zentrale Label- und Farbzuordnung pro EntryType (instanzunabhängig).</summary>
public static class EntryTypeInfo
{
    /// <summary>Lokalisierungs-Schlüssel, z.B. "EntryType_Work" → Localizer.</summary>
    public static string Key(EntryType type) => $"EntryType_{type}";

    /// <summary>Ist es tatsächliche Arbeit (für die Anzeige/Hervorhebung)?</summary>
    public static bool CountsAsWork(EntryType type) => type is EntryType.Work;

    /// <summary>Abwesenheits-Typ (Urlaub/Krank/Abwesend) → wird als Hinweis unter dem Datum gezeigt, nicht im Raster.</summary>
    public static bool IsAbsence(EntryType type)
        => type is EntryType.Vacation or EntryType.SickLeave or EntryType.Absence;

    /// <summary>Zählt der Typ aufs Stundenkonto? Arbeit + angerechnete Abwesenheit (Krank/Urlaub) + Übernachtung (pauschal).</summary>
    public static bool CountsTowardHours(EntryType type)
        => type is EntryType.Work or EntryType.SickLeave or EntryType.Vacation or EntryType.Overnight;

    /// <summary>Deutsche Fallback-Beschriftung (UI nutzt bevorzugt den Localizer via Key).</summary>
    public static string Label(EntryType type) => type switch
    {
        EntryType.Work => "Arbeit",
        EntryType.Vacation => "Urlaub",
        EntryType.SickLeave => "Krank",
        EntryType.Activity => "Aktivität",
        EntryType.Absence => "Abwesend",
        EntryType.Overnight => "Übernachtung",
        EntryType.Custom => "Termin",
        _ => type.ToString()
    };

    /// <summary>
    /// Standard-Kachelfarbe je Typ — warm abgestimmt (v0.23): Ocker für Arbeit statt Blau, dazu
    /// Oliv, Ziegelrot, Altrosa, Taupe, Pflaume, Petrol. Die Töne bleiben untereinander klar
    /// unterscheidbar; die Schrift darauf rechnet <see cref="EntryColors.OnTile"/>.
    /// </summary>
    public static string Color(EntryType type) => type switch
    {
        EntryType.Work => "#D4933A",
        EntryType.Vacation => "#6B9A4B",
        EntryType.SickLeave => "#B23A2E",
        EntryType.Activity => "#B8698F",
        EntryType.Absence => "#8D7F72",
        EntryType.Overnight => "#6D4C73",
        EntryType.Custom => "#3E8A80",
        _ => "#D4933A"
    };
}
