namespace FlexFamilyCalendar.Models;

/// <summary>Rolle einer Chat-Nachricht — User stellt die Frage, Assistant antwortet.</summary>
public enum ChatHistoryRole { User, Assistant }

/// <summary>
/// Persistierte Chat-Nachricht aus dem KI-Plan-Verlauf. Wird pro Benutzer gespeichert
/// (UserId in der Server-DB; lokal global, weil Local-Modus eh ein Konto pro Installation hat).
/// </summary>
public class ChatHistoryEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public ChatHistoryRole Role { get; set; }
    public string Text { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
