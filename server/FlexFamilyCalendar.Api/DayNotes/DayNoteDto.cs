namespace FlexFamilyCalendar.Api.DayNotes;

/// <summary>Tagesnotiz für Lesen und Setzen (Datum steckt in der Route). NoteUserId = null heißt
/// „für alle sichtbar"; ein UserId gesetzt heißt „nur Admin und diese Person".</summary>
public record DayNoteDto(string Note, bool IsFinalized, string? NoteUserId = null);

/// <summary>Eine Tagesnotiz mit ihrem Datum — für den Bereichs-Abruf einer ganzen Woche.</summary>
public record DayNoteRangeDto(DateOnly Date, string Note, bool IsFinalized, string? NoteUserId = null);
