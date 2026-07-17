namespace FlexFamilyCalendar.Models;

public class CalendarDay
{
    public string DateString { get; set; } = "";  // "2026-05-25"
    public bool IsFinalized { get; set; }
    public string Note { get; set; } = "";        // Tages-Hinweis. Sichtbar abhängig von NoteUserId:
                                                  //   null = für alle; gesetzt = nur Admin + die Person.
    public string? NoteUserId { get; set; }       // Adressat des Hinweises (optional)
    public List<CalendarEntry> Entries { get; set; } = new();

    [System.Text.Json.Serialization.JsonIgnore]
    public DateOnly Date => DateOnly.Parse(DateString);
}
