namespace FlexFamilyCalendar.Api.Models;

/// <summary>Peer-to-Peer Schichttausch-Vorschlag. Zeitstempel als ISO-Strings (kein DateTime/tz-Risiko).</summary>
public class ShiftSwapRequestEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CreatedAt { get; set; } = "";       // ISO 8601
    public string? RespondedAt { get; set; }
    public int Status { get; set; }                    // SwapStatus (0..3)
    public int Mode { get; set; }                      // SwapMode (0..1)

    public string FromUserId { get; set; } = "";
    public string FromUserName { get; set; } = "";
    public string FromDate { get; set; } = "";         // yyyy-MM-dd
    public string FromEntryId { get; set; } = "";

    public string ToUserId { get; set; } = "";
    public string ToUserName { get; set; } = "";
    public string? ToDate { get; set; }                // yyyy-MM-dd (nur Exchange)
    public string? ToEntryId { get; set; }             // nur Exchange

    public string Message { get; set; } = "";
}
