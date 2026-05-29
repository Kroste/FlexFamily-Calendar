namespace FlexFamilyCalendar.Api.Models;

/// <summary>Ein Kalender-Eintrag (Schicht, Übernachtung, Aktivität oder Abwesenheit) für einen Benutzer.</summary>
public class CalendarEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }                  // Betroffener Benutzer (wen der Eintrag betrifft)
    public string Type { get; set; } = EntryTypes.Work;
    public DateOnly Date { get; set; }                // Starttag
    public DateOnly? EndDate { get; set; }            // Endtag bei Bereichen (Urlaub/Krank/Abwesend)
    public TimeOnly? StartTime { get; set; }          // bei verplanten Schichten
    public TimeOnly? EndTime { get; set; }
    public bool EndsNextDay { get; set; }             // Übernachtung über Mitternacht
    public string? CategoryLabel { get; set; }        // Freitext-Titel (Desktop: Title)
    public string? ActivityTypeId { get; set; }       // Referenz auf eine Aktivitäts-Kategorie (Client-String-Id)
    public string? Note { get; set; }
    public string Status { get; set; } = EntryStatus.Approved;
    public Guid CreatedBy { get; set; }               // Wer den Eintrag angelegt hat (Audit)
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public static class EntryTypes
{
    public const string Work = "Work";
    public const string Overnight = "Overnight";
    public const string Activity = "Activity";
    public const string Vacation = "Vacation";
    public const string SickLeave = "SickLeave";
    public const string Absence = "Absence";

    public static readonly string[] All = { Work, Overnight, Activity, Vacation, SickLeave, Absence };

    public static bool IsKnown(string type) => Array.IndexOf(All, type) >= 0;

    /// <summary>Urlaub und Krankheit sind privat und werden Fremden maskiert.</summary>
    public static bool IsPrivate(string type) => type is Vacation or SickLeave;

    /// <summary>Verplante Schichten mit Uhrzeit.</summary>
    public static bool IsTimed(string type) => type is Work or Overnight;
}

public static class EntryStatus
{
    public const string Pending = "Pending";    // Urlaubswunsch, wartet auf Genehmigung
    public const string Approved = "Approved";  // gültig, Teil des Plans
    public const string Rejected = "Rejected";  // vom Admin abgelehnt
}
