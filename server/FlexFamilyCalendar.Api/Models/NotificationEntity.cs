namespace FlexFamilyCalendar.Api.Models;

/// <summary>Benachrichtigung an einen Benutzer (Text als i18n-Schlüssel + Argumente). CreatedAt als ISO-String.</summary>
public class NotificationEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = "";        // Empfänger
    public string CreatedAt { get; set; } = "";      // ISO 8601
    public bool IsRead { get; set; }
    public string MessageKey { get; set; } = "";
    public List<string> Args { get; set; } = new();  // → text[]
    public string? RelatedDate { get; set; }         // yyyy-MM-dd
    public string? Action { get; set; }
    public string? RelatedUserId { get; set; }
}
