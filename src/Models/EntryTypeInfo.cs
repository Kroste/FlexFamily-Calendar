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

    public static string Color(EntryType type) => type switch
    {
        EntryType.Work => "#2E86C1",
        EntryType.Vacation => "#27AE60",
        EntryType.SickLeave => "#C0392B",
        EntryType.Activity => "#E67E22",
        EntryType.Absence => "#7F8C8D",
        EntryType.Overnight => "#5B4B8A",
        EntryType.Custom => "#16A085",
        _ => "#2E86C1"
    };
}
