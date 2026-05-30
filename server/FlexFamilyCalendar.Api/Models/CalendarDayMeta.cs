namespace FlexFamilyCalendar.Api.Models;

/// <summary>Pro-Tag-Metadaten: Tagesnotiz + Adressat (Admin schreibt; sichtbar je nach NoteUserId
/// für alle oder nur Admin + adressierte Person) und Finalisiert-Flag.</summary>
public class CalendarDayMeta
{
    public DateOnly Date { get; set; }      // Primärschlüssel
    public string Note { get; set; } = "";
    public string? NoteUserId { get; set; } // null = für alle; gesetzt = nur Admin + diese Person
    public bool IsFinalized { get; set; }
}
