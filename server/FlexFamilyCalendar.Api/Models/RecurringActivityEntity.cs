namespace FlexFamilyCalendar.Api.Models;

/// <summary>Wiederkehrende Aktivität (Wochen-Regel), als Overlay über die Tage projiziert (Typ Activity).</summary>
public class RecurringActivityEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = "";
    public string UserDisplayName { get; set; } = "";
    public string Title { get; set; } = "";
    public string? ActivityTypeId { get; set; }      // weiche Referenz auf eine Kategorie (Client-String-Id)
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public List<int> Weekdays { get; set; } = new();  // DayOfWeek-Werte 0..6 → integer[]
    public bool SkipOnHolidays { get; set; }
}
