namespace FlexFamilyCalendar.Models;

/// <summary>Zentrale Label- und Farbzuordnung pro EntryType (instanzunabhängig).</summary>
public static class EntryTypeInfo
{
    public static string Label(EntryType type) => type switch
    {
        EntryType.Work => "Arbeit",
        EntryType.AuPairShift => "Au-Pair",
        EntryType.Vacation => "Urlaub",
        EntryType.SickLeave => "Krank",
        EntryType.Activity => "Aktivität",
        EntryType.Absence => "Abwesend",
        _ => type.ToString()
    };

    public static string Color(EntryType type) => type switch
    {
        EntryType.Work => "#2E86C1",
        EntryType.AuPairShift => "#8E44AD",
        EntryType.Vacation => "#27AE60",
        EntryType.SickLeave => "#C0392B",
        EntryType.Activity => "#E67E22",
        EntryType.Absence => "#7F8C8D",
        _ => "#2E86C1"
    };
}
