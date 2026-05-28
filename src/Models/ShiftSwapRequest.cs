namespace FlexFamilyCalendar.Models;

// Werte gepinnt — werden als Integer in swap-requests.json serialisiert.
public enum SwapStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2,
    Cancelled = 3
}

public enum SwapMode
{
    GiveAway = 0,  // Kollege übernimmt meine Schicht; ich werde frei
    Exchange = 1   // meine Schicht ↔ eine konkrete Schicht des Kollegen
}

/// <summary>
/// Ein Schichttausch-Vorschlag eines Mitarbeiters an einen Kollegen (Peer-to-Peer).
/// Bei GiveAway ist nur die From-Schicht relevant; bei Exchange auch die To-Schicht des Kollegen.
/// </summary>
public class ShiftSwapRequest
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? RespondedAt { get; set; }
    public SwapStatus Status { get; set; } = SwapStatus.Pending;
    public SwapMode Mode { get; set; } = SwapMode.GiveAway;

    // Initiator + angebotene Schicht
    public string FromUserId { get; set; } = "";
    public string FromUserName { get; set; } = "";
    public string FromDate { get; set; } = "";   // yyyy-MM-dd
    public string FromEntryId { get; set; } = "";

    // Angefragter Kollege; bei Exchange zusätzlich dessen Gegen-Schicht
    public string ToUserId { get; set; } = "";
    public string ToUserName { get; set; } = "";
    public string? ToDate { get; set; }           // yyyy-MM-dd (nur Exchange)
    public string? ToEntryId { get; set; }        // nur Exchange

    public string Message { get; set; } = "";
}
