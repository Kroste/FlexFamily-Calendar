namespace FlexFamilyCalendar.Api.DayNotes;

/// <summary>Tagesnotiz für Lesen und Setzen (Datum steckt in der Route).</summary>
public record DayNoteDto(string Note, bool IsFinalized);
