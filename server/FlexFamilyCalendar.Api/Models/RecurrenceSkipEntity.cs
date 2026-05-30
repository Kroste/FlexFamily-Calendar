namespace FlexFamilyCalendar.Api.Models;

/// <summary>
/// Tagesgenaue Aussetzung einer <see cref="RecurringActivityEntity"/>. From und To inklusiv.
/// </summary>
public class RecurrenceSkipEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RecurringActivityId { get; set; }
    public RecurringActivityEntity? RecurringActivity { get; set; }

    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public string? Reason { get; set; }
}
