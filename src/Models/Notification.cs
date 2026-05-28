namespace FlexFamilyCalendar.Models;

/// <summary>
/// Eine an einen Benutzer gerichtete Benachrichtigung. Der Text wird sprach-neutral als i18n-Schlüssel
/// (<see cref="MessageKey"/>) + Argumente (<see cref="Args"/>) gespeichert und erst bei der Anzeige in
/// der Sprache des Empfängers formatiert.
/// </summary>
public class Notification
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = "";   // Empfänger
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsRead { get; set; }
    public string MessageKey { get; set; } = "";
    public List<string> Args { get; set; } = new();
    public string? RelatedDate { get; set; }    // yyyy-MM-dd → „zur Woche springen"
    public string? Action { get; set; }          // z.B. "ReplanSick" → Klick öffnet den Umplanungs-Dialog
    public string? RelatedUserId { get; set; }   // Bezugsperson der Aktion (z.B. die krankgemeldete Person)
}
