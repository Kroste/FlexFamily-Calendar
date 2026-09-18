namespace FlexFamilyCalendar.Models;

/// <summary>
/// Reine Logik fürs Kopieren einer Wochen-Vorlage: nur wiederkehrende Einträge (Arbeit/Aktivität),
/// nicht einmalige Ausnahmen (Krank/Urlaub/Abwesend). Jeder kopierte Eintrag bekommt eine neue Id.
/// </summary>
public static class WeekCopy
{
    /// <summary>Gehört der Typ zur wiederkehrenden Wochen-Vorlage?</summary>
    public static bool IsTemplate(EntryType type)
        => type is EntryType.Work or EntryType.Activity;

    /// <summary>
    /// Klont die Vorlage-Einträge (neue Id, sonst identisch). Mehrtägige Einträge bleiben außen
    /// vor: sie sind einmalige Einsätze, und ihre Tagesanteile („ab 14:00", „ganztägig") ergäben
    /// ohne den Zeitraum dahinter lauter Einzeleinträge mit 24 Stunden.
    /// </summary>
    public static List<CalendarEntry> TemplateEntries(IEnumerable<CalendarEntry> source)
        => source.Where(e => IsTemplate(e.Type) && !e.IsMultiDay).Select(Clone).ToList();

    private static CalendarEntry Clone(CalendarEntry e) => new()
    {
        UserId = e.UserId,
        UserDisplayName = e.UserDisplayName,
        Type = e.Type,
        AllDay = e.AllDay,   // sonst würde aus „Frei, ganztägig" eine Schicht 00:00–00:00 mit 24 Stunden
        StartTime = e.StartTime,
        EndTime = e.EndTime,
        Title = e.Title,
        Color = e.Color,   // sonst verlor die kopierte Woche alle eigenen Kachelfarben
        Notes = e.Notes
    };
}
