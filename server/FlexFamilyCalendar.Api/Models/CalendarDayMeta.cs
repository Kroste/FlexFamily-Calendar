namespace FlexFamilyCalendar.Api.Models;

/// <summary>Pro-Tag-Metadaten: allgemeine Tagesnotiz (Admin, für alle sichtbar) und Finalisiert-Flag.</summary>
public class CalendarDayMeta
{
    public DateOnly Date { get; set; }      // Primärschlüssel
    public string Note { get; set; } = "";
    public bool IsFinalized { get; set; }
}
