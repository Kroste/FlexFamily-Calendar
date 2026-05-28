namespace FlexFamilyCalendar.Models;

/// <summary>Laufzeit-Markierung für eine offene Tausch-Anfrage an dieser Schicht (relativ zum aktuellen Benutzer).</summary>
public enum SwapMark
{
    None = 0,
    Incoming = 1,  // an mich gerichtet → ich kann annehmen/ablehnen
    Outgoing = 2   // von mir gestellt → ausstehend, ich kann zurückziehen
}

public class CalendarEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = "";
    public string UserDisplayName { get; set; } = "";
    public EntryType Type { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string Title { get; set; } = "";
    public string Notes { get; set; } = "";
    public string? ActivityTypeId { get; set; }   // optionale Kategorie bei Typ Activity

    [Newtonsoft.Json.JsonIgnore]
    public double DurationHours => (EndTime - StartTime).TotalHours;

    [Newtonsoft.Json.JsonIgnore]
    public string TimeRange => $"{StartTime:hh\\:mm}–{EndTime:hh\\:mm}";

    [Newtonsoft.Json.JsonIgnore]
    public string EntryColor => EntryTypeInfo.Color(Type);

    [Newtonsoft.Json.JsonIgnore]
    public string TypeLabel => EntryTypeInfo.Label(Type);

    /// <summary>Personenfarbe (zur Laufzeit aus dem Benutzer aufgelöst, nicht persistiert).</summary>
    [Newtonsoft.Json.JsonIgnore]
    public string OwnerColor { get; set; } = "#7F8C8D";

    /// <summary>Effektive Deckkraft je Sichtmodus/Eigentümer (zur Laufzeit gesetzt).</summary>
    [Newtonsoft.Json.JsonIgnore]
    public double EffectiveOpacity { get; set; } = 1.0;

    /// <summary>Eigene Schicht in der Normalsicht → Rahmen-Hervorhebung.</summary>
    [Newtonsoft.Json.JsonIgnore]
    public bool IsHighlighted { get; set; }

    /// <summary>Datenschutz-maskierter Anzeigetyp (Laufzeit; Default = echter Typ).</summary>
    [Newtonsoft.Json.JsonIgnore]
    public EntryType DisplayType { get; set; }

    /// <summary>Datenschutz-maskierter Titel (Laufzeit; leer = verborgen).</summary>
    [Newtonsoft.Json.JsonIgnore]
    public string DisplayTitle { get; set; } = "";

    /// <summary>Markierung einer offenen Tausch-Anfrage an dieser Schicht (Laufzeit, nicht persistiert).</summary>
    [Newtonsoft.Json.JsonIgnore]
    public SwapMark SwapMark { get; set; } = SwapMark.None;

    [Newtonsoft.Json.JsonIgnore]
    public bool HasSwap => SwapMark != SwapMark.None;

    /// <summary>Aufgelöster Aktivitäts-Kategoriename (Laufzeit; leer = keine Kategorie).</summary>
    [Newtonsoft.Json.JsonIgnore]
    public string ActivityName { get; set; } = "";

    /// <summary>Farbe der Aktivitäts-Kategorie (Laufzeit).</summary>
    [Newtonsoft.Json.JsonIgnore]
    public string ActivityColor { get; set; } = "#7F8C8D";

    [Newtonsoft.Json.JsonIgnore]
    public bool HasActivity => !string.IsNullOrEmpty(ActivityName);

    /// <summary>Karte zeigt das feste Typ-Label nur, wenn keine Aktivitäts-Kategorie aufgelöst ist.</summary>
    [Newtonsoft.Json.JsonIgnore]
    public bool ShowsTypeLabel => !HasActivity;
}
