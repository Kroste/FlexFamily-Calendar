namespace FlexFamilyCalendar.Models;

/// <summary>
/// Persistenter Merker, der bei jedem KI-Planungs-Chat als Kontext mitgeschickt wird
/// (z.B. „Mia hat im Mai Klassenfahrt", „Pausch arbeitet montags von zu Hause").
/// </summary>
public class PlannerNote
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Text { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
