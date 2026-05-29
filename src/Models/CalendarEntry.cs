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

    // Abwesenheiten (Urlaub/Krank/Abwesend) als Zeitraum: je Tag ein Eintrag, über GroupId verbunden.
    public string? AbsenceGroupId { get; set; }    // verbindet die Tage einer Abwesenheit (null = keine)
    public DateOnly? AbsenceStart { get; set; }    // erster Tag des Zeitraums
    public DateOnly? AbsenceEnd { get; set; }      // letzter Tag des Zeitraums

    /// <summary>Stunden der Schicht; Schichten über Mitternacht (EndTime ≤ StartTime) zählen den Folgetag-Anteil mit.</summary>
    [Newtonsoft.Json.JsonIgnore]
    public double DurationHours
    {
        get
        {
            var d = (EndTime - StartTime).TotalHours;
            return d > 0 ? d : d + 24;   // EndTime ≤ StartTime ⇒ über Mitternacht
        }
    }

    /// <summary>Schicht überschreitet die Tagesgrenze (z.B. 20:00–06:00, auch 20:00–00:00).</summary>
    [Newtonsoft.Json.JsonIgnore]
    public bool CrossesMidnight => EndTime <= StartTime;

    [Newtonsoft.Json.JsonIgnore]
    public string TimeRange => $"{StartTime:hh\\:mm}–{EndTime:hh\\:mm}";

    /// <summary>Kompakter Zeitraum einer mehrtägigen Abwesenheit (leer, wenn nur ein Tag).</summary>
    [Newtonsoft.Json.JsonIgnore]
    public string AbsenceSpanLabel =>
        AbsenceStart is { } s && AbsenceEnd is { } e && e > s ? $"{s:dd.MM.}–{e:dd.MM.}" : "";

    /// <summary>Anzeige als Abwesenheit (Urlaub/Krank/Abwesend) — in der Tabellenzelle ohne Uhrzeit.</summary>
    [Newtonsoft.Json.JsonIgnore]
    public bool IsAbsenceDisplay => EntryTypeInfo.IsAbsence(DisplayType);

    /// <summary>Uhrzeit anzeigen (Schichten/Aktivitäten) — nicht bei Abwesenheiten.</summary>
    [Newtonsoft.Json.JsonIgnore]
    public bool ShowsTime => !IsAbsenceDisplay;

    /// <summary>Zeitraum einer Abwesenheit in der Zelle anzeigen (nur wenn mehrtägig).</summary>
    [Newtonsoft.Json.JsonIgnore]
    public bool ShowAbsenceSpan => IsAbsenceDisplay && !string.IsNullOrEmpty(AbsenceSpanLabel);

    [Newtonsoft.Json.JsonIgnore]
    public string EntryColor => EntryTypeInfo.Color(Type);

    [Newtonsoft.Json.JsonIgnore]
    public string TypeLabel => EntryTypeInfo.Label(Type);

    /// <summary>Personenfarbe (zur Laufzeit aus dem Benutzer aufgelöst, nicht persistiert).</summary>
    [Newtonsoft.Json.JsonIgnore]
    public string OwnerColor { get; set; } = "#7F8C8D";

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

    /// <summary>Laufzeit: aus einer wiederkehrenden Regel projiziert (nicht persistiert, nicht editierbar).</summary>
    [Newtonsoft.Json.JsonIgnore]
    public bool IsRecurring { get; set; }

    /// <summary>Laufzeit: fällt auf einen Feiertag → Hinweis „könnte ausfallen".</summary>
    [Newtonsoft.Json.JsonIgnore]
    public bool HolidayConflict { get; set; }
}
