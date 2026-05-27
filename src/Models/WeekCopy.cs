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

    /// <summary>Klont die Vorlage-Einträge (neue Id, sonst identisch).</summary>
    public static List<CalendarEntry> TemplateEntries(IEnumerable<CalendarEntry> source)
        => source.Where(e => IsTemplate(e.Type)).Select(Clone).ToList();

    private static CalendarEntry Clone(CalendarEntry e) => new()
    {
        UserId = e.UserId,
        UserDisplayName = e.UserDisplayName,
        Type = e.Type,
        StartTime = e.StartTime,
        EndTime = e.EndTime,
        Title = e.Title,
        Notes = e.Notes
    };
}
