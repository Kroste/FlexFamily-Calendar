namespace FlexFamilyCalendar.Api.Models;

public class PlannerNoteEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
