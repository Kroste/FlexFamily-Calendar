namespace FlexFamilyCalendar.Models;

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
}
