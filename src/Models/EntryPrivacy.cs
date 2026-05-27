namespace FlexFamilyCalendar.Models;

/// <summary>
/// Datenschutz-Anzeige: Krank/Urlaub sind privat. Andere (Nicht-Admin, nicht der Eigentümer)
/// sehen nur „Abwesend" ohne Grund/Titel. Reine, testbare Logik (nur Anzeige, keine Datenänderung).
/// </summary>
public static class EntryPrivacy
{
    public static bool IsPrivate(EntryType type)
        => type is EntryType.SickLeave or EntryType.Vacation;

    /// <summary>Anzuzeigender Typ: privat &amp; nicht berechtigt → Absence; sonst der echte Typ.</summary>
    public static EntryType DisplayType(EntryType type, bool canSeeReason)
        => IsPrivate(type) && !canSeeReason ? EntryType.Absence : type;

    /// <summary>Darf der konkrete Grund/Titel angezeigt werden?</summary>
    public static bool ShowReason(EntryType type, bool canSeeReason)
        => !IsPrivate(type) || canSeeReason;
}
