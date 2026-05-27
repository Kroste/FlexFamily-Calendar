namespace FlexFamilyCalendar.Models;

/// <summary>Zentrale Label- und Farbzuordnung pro EntryType (instanzunabhängig).</summary>
public static class EntryTypeInfo
{
    /// <summary>Lokalisierungs-Schlüssel, z.B. "EntryType_Work" → Localizer.</summary>
    public static string Key(EntryType type) => $"EntryType_{type}";

    /// <summary>Zählt der Eintragstyp als geleistete Arbeitszeit (fürs Wochenstunden-Konto)?</summary>
    public static bool CountsAsWork(EntryType type) => type is EntryType.Work;

    /// <summary>Deutsche Fallback-Beschriftung (UI nutzt bevorzugt den Localizer via Key).</summary>
    public static string Label(EntryType type) => type switch
    {
        EntryType.Work => "Arbeit",
        EntryType.Vacation => "Urlaub",
        EntryType.SickLeave => "Krank",
        EntryType.Activity => "Aktivität",
        EntryType.Absence => "Abwesend",
        _ => type.ToString()
    };

    public static string Color(EntryType type) => type switch
    {
        EntryType.Work => "#2E86C1",
        EntryType.Vacation => "#27AE60",
        EntryType.SickLeave => "#C0392B",
        EntryType.Activity => "#E67E22",
        EntryType.Absence => "#7F8C8D",
        _ => "#2E86C1"
    };
}
