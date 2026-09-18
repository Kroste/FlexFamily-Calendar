namespace FlexFamilyCalendar.Models;

/// <summary>Laufzeit-Markierung für eine offene Tausch-Anfrage an dieser Schicht (relativ zum aktuellen Benutzer).</summary>
public enum SwapMark
{
    None = 0,
    Incoming = 1,  // an mich gerichtet → ich kann annehmen/ablehnen
    Outgoing = 2   // von mir gestellt → ausstehend, ich kann zurückziehen
}

/// <summary>Genehmigungs-Zustand für einen Eintrag. Deckt den serverseitigen EntryStatus 1:1 ab.
/// Nicht-Admin-Urlaubswünsche entstehen als Pending und werden erst durch Admin-Approve auf
/// Approved gesetzt; Ablehnung setzt Rejected und macht den Eintrag im Kalender unsichtbar.</summary>
public static class EntryStatuses
{
    public const string Approved = "Approved";
    public const string Pending = "Pending";
    public const string Rejected = "Rejected";
}

public class CalendarEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = "";
    public string UserDisplayName { get; set; } = "";
    public EntryType Type { get; set; }
    /// <summary>Genehmigungs-Zustand (<see cref="EntryStatuses"/>). Default = Approved für Bestandsdaten.</summary>
    public string Status { get; set; } = EntryStatuses.Approved;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string Title { get; set; } = "";
    public string Notes { get; set; } = "";

    /// <summary>
    /// Nur für die einmalige Übernahme alter lokaler Dateien: dort stand die Kategorie, deren Name
    /// und Farbe der Eintrag trug. <see cref="Services.LegacyCategoryMigration"/> überträgt beides
    /// beim Laden an den Eintrag und setzt das Feld auf null — ab dann wird es nicht mehr
    /// geschrieben. Im Server-Modus erledigt das die EF-Migration DropCategories.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("ActivityTypeId")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyActivityTypeId { get; set; }

    /// <summary>
    /// Frei gewählte Kachelfarbe dieses einen Eintrags (leer = automatisch aus dem Typ).
    /// Persistiert, weil sie zur Planung gehört und nicht zur Ansicht des Betrachters.
    /// </summary>
    public string Color { get; set; } = "";

    // Zeitraum-Einträge: je Tag ein Eintrag, über die GroupId verbunden. Die Namen stammen aus der
    // Zeit, als nur Abwesenheiten mehrere Tage spannen konnten — heute kann das jeder Eintrag
    // (Start und Ende mit Datum und Uhrzeit). Umbenannt wird nicht: die lokalen JSON-Dateien
    // tragen genau diese Feldnamen.
    public string? AbsenceGroupId { get; set; }    // verbindet die Tage eines Zeitraums (null = keiner)
    public DateOnly? AbsenceStart { get; set; }    // erster Tag des Zeitraums
    public DateOnly? AbsenceEnd { get; set; }      // letzter Tag des Zeitraums

    /// <summary>
    /// Uhrzeit am ersten bzw. letzten Tag eines Zeitraums (null = ganztägig oder kein Zeitraum).
    /// <see cref="StartTime"/>/<see cref="EndTime"/> tragen am einzelnen Tag nur dessen Anteil —
    /// für die Bearbeitung und den Server braucht es die Eckwerte des ganzen Zeitraums.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public TimeSpan? SpanStartTime { get; set; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public TimeSpan? SpanEndTime { get; set; }

    /// <summary>
    /// Ganztägig gesetzt (true), mit Uhrzeit (false) oder Altbestand ohne Angabe (null). Nullable,
    /// weil ältere Dateien das Feld nicht kennen: dort galten Abwesenheiten immer als ganztägig und
    /// alles andere als Eintrag mit Uhrzeit — siehe <see cref="IsAllDay"/>.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public bool? AllDay { get; set; }

    /// <summary>Ganztägig — ohne Uhrzeit, zählt keine Stunden.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsAllDay => AllDay ?? EntryTypeInfo.IsAbsence(Type);

    /// <summary>Spannt mehr als einen Tag (Start- und Enddatum verschieden).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsMultiDay => AbsenceStart is { } s && AbsenceEnd is { } e && e > s;

    /// <summary>
    /// Schicht im engeren Sinn: an einem Tag, mit Uhrzeit. Nur solche Einträge prüfen
    /// Arbeitszeit-Regeln (Tageslimit, Ruhezeit, Überschneidung) und lassen sich tauschen — der
    /// Mittelteil eines mehrtägigen Einsatzes hätte sonst 24 Stunden und null Ruhezeit.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsShift => !IsAllDay && !IsMultiDay;

    /// <summary>
    /// Stunden an diesem Tag. Ganztägige Einträge zählen nichts; am Tag eines Zeitraums zählt
    /// dessen Anteil (erster Tag bis 24:00, Mitte ganz, letzter Tag ab 00:00). Schichten über
    /// Mitternacht (EndTime ≤ StartTime) zählen den Folgetag-Anteil mit.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public double DurationHours
    {
        get
        {
            if (IsAllDay) return 0;
            var d = (EndTime - StartTime).TotalHours;
            return d > 0 ? d : d + 24;   // EndTime ≤ StartTime ⇒ über Mitternacht
        }
    }

    /// <summary>Schicht überschreitet die Tagesgrenze (z.B. 20:00–06:00, auch 20:00–00:00).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool CrossesMidnight => !IsAllDay && EndTime <= StartTime;

    /// <summary>
    /// Uhrzeit, wie sie auf der Kachel steht: „08:00–16:00", am Tag eines Zeitraums nur dessen
    /// Anteil („ab 14:00", „ganztägig", „bis 10:00").
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string TimeRange
    {
        get
        {
            var loc = Localization.Localizer.Instance;
            if (IsAllDay) return loc["Entry_AllDayShort"];
            if (IsMultiDay)
            {
                var toMidnight = EndTime >= Services.EntrySpans.EndOfDay;
                var fromMidnight = StartTime == TimeSpan.Zero;
                if (toMidnight && fromMidnight) return loc["Entry_AllDayShort"];
                if (toMidnight) return string.Format(loc["Entry_FromTime"], Hm(StartTime));
                if (fromMidnight) return string.Format(loc["Entry_UntilTime"], Hm(EndTime));
            }
            return $"{Hm(StartTime)}–{Hm(EndTime)}";
        }
    }

    private static string Hm(TimeSpan t) => $"{t.Hours:D2}:{t.Minutes:D2}";

    /// <summary>Kompakter Zeitraum einer mehrtägigen Abwesenheit (leer, wenn nur ein Tag).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string AbsenceSpanLabel =>
        AbsenceStart is { } s && AbsenceEnd is { } e && e > s ? $"{s:dd.MM.}–{e:dd.MM.}" : "";

    /// <summary>Anzeige als Abwesenheit (Urlaub/Krank/Abwesend) — in der Tabellenzelle ohne Uhrzeit.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsAbsenceDisplay => EntryTypeInfo.IsAbsence(DisplayType);

    /// <summary>
    /// Uhrzeit anzeigen: bei Einträgen immer (auch „ganztägig"), bei Abwesenheiten nur, wenn sie
    /// eine Uhrzeit haben und nichts maskiert ist. Der Server räumt die Uhrzeit maskierter
    /// Einträge weg; im lokalen Modus hält diese Prüfung dieselbe Linie.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool ShowsTime => !IsAbsenceDisplay || (!IsAllDay && DisplayType == Type);

    /// <summary>Zeitraum einer Abwesenheit in der Zelle anzeigen (nur wenn mehrtägig).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool ShowAbsenceSpan => IsAbsenceDisplay && !string.IsNullOrEmpty(AbsenceSpanLabel);

    [System.Text.Json.Serialization.JsonIgnore]
    public string EntryColor => EntryTypeInfo.Color(Type);

    [System.Text.Json.Serialization.JsonIgnore]
    public string TypeLabel => EntryTypeInfo.Label(Type);

    /// <summary>Personenfarbe (zur Laufzeit aus dem Benutzer aufgelöst, nicht persistiert).
    /// Färbt nicht mehr die Plan-Kachel — siehe <see cref="TileColor"/> —, wird aber weiter
    /// für den Personen-Punkt in der Namensspalte und die Mobile-Tageskarten gebraucht.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string OwnerColor { get; set; } = "#7F8C8D";

    /// <summary>
    /// Farbe der Plan-Kachel: eigene Farbe schlägt Typ. Bewusst auf <see cref="DisplayType"/>
    /// gerechnet, nicht auf <see cref="Type"/> — sonst verriete das Rot einer Krankmeldung den
    /// Grund, den die Maskierung gerade als „Abwesend" verbirgt.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string TileColor => EntryColors.Tile(DisplayType, VisibleColor);

    /// <summary>
    /// Die eigene Farbe zählt nur, solange am Eintrag nichts maskiert wurde. Sonst wäre eine
    /// fremde Krankmeldung an ihrer Sonderfarbe erkennbar, obwohl sie als „Abwesend" erscheint —
    /// die Maskierung wäre über die Farbe unterlaufen. Im Server-Modus räumt <c>EntryDto.Mask</c>
    /// die Farbe ohnehin weg; diese Prüfung deckt den lokalen Modus ab, der ohne Server maskiert.
    /// </summary>
    private string? VisibleColor => DisplayType == Type ? Color : null;

    /// <summary>Schriftfarbe auf der Kachel — berechnet, damit die Uhrzeit auf jeder vom Admin
    /// vergebenen Farbe lesbar bleibt.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string TileForeground => EntryColors.OnTile(TileColor);

    /// <summary>
    /// Datenschutz-maskierter Anzeigetyp (Laufzeit). Ohne gesetzten Wert gilt der echte Typ:
    /// <see cref="EntryType.Work"/> ist der Enum-Wert 0 und wäre sonst die stille Vorgabe für
    /// jeden Eintrag, der noch nicht durch die Anzeige-Auflösung gelaufen ist — der bekäme
    /// damit Farbe und Label einer Schicht.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public EntryType DisplayType
    {
        get => _displayType ?? Type;
        set => _displayType = value;
    }

    private EntryType? _displayType;

    /// <summary>Datenschutz-maskierter Titel (Laufzeit; leer = verborgen).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string DisplayTitle { get; set; } = "";

    /// <summary>Markierung einer offenen Tausch-Anfrage an dieser Schicht (Laufzeit, nicht persistiert).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public SwapMark SwapMark { get; set; } = SwapMark.None;

    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasSwap => SwapMark != SwapMark.None;

    /// <summary>
    /// Die Bezeichnung IST der Name der Kachel — außer bei Abwesenheiten, deren Name der
    /// (maskierte) Typ ist. Seit es weder Kategorien noch eine Typ-Auswahl im Dialog gibt, ist
    /// die Freitext-Bezeichnung das, was im Plan oben steht: „Arbeit", „Frei", „Sprachschule".
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool ShowsTitleAsName => !IsAbsenceDisplay && !string.IsNullOrWhiteSpace(DisplayTitle);

    /// <summary>Typ-Label als Name: bei Abwesenheiten und bei Alt-Einträgen ohne Bezeichnung
    /// (die dann wie früher „Arbeit" o. ä. zeigen).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool ShowsTypeLabel => !ShowsTitleAsName;

    /// <summary>Zusatzzeile nur bei Abwesenheiten: dort ist die Bezeichnung ein Vermerk unter dem
    /// Typ — und ohnehin nur für den Betroffenen und Admins gesetzt (Maskierung).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool ShowsSubtitle => IsAbsenceDisplay && !string.IsNullOrEmpty(DisplayTitle);

    /// <summary>Laufzeit: aus einer wiederkehrenden Regel projiziert (nicht persistiert, nicht editierbar).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsRecurring { get; set; }

    /// <summary>Laufzeit: fällt auf einen Feiertag → Hinweis „könnte ausfallen".</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HolidayConflict { get; set; }

    /// <summary>Laufzeit: projizierter Eintrag liegt in einer aktiven Aussetzung (Urlaub/Krank/…). UI: grau + „(pausiert)".</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsPaused { get; set; }

    /// <summary>Anzeige-Opacity: pausierte oder noch nicht genehmigte Einträge wirken durchscheinend.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public double DisplayOpacity => IsPaused || IsPending ? 0.45 : 1.0;

    /// <summary>Wartet auf Admin-Genehmigung (typisch Urlaubswunsch eines Nicht-Admins).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsPending => Status == EntryStatuses.Pending;
}
