namespace FlexFamilyCalendar.Api.Models;

/// <summary>Persistierte KI-Chat-Nachricht eines Benutzers. UserId trennt die Verläufe pro Konto.</summary>
public class ChatHistoryEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Role { get; set; } = "User";     // "User" | "Assistant"
    public string Text { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
