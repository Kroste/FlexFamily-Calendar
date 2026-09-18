namespace FlexFamilyCalendar.Api.Models;

/// <summary>Wiederkehrende Aktivität (Wochen-Regel), als Overlay über die Tage projiziert (Typ Activity).</summary>
public class RecurringActivityEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = "";
    public string UserDisplayName { get; set; } = "";
    public string Title { get; set; } = "";          // Freitext-Bezeichnung (früher kam der Name aus einer Kategorie)
    public string? Color { get; set; }               // Kachelfarbe (#RRGGBB), null = Standardfarbe für Aktivitäten
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public List<int> Weekdays { get; set; } = new();  // DayOfWeek-Werte 0..6 → integer[]
    public bool SkipOnHolidays { get; set; }

    /// <summary>Tagesgenaue Aussetzungen (Urlaub/Krank/…). Cascade delete via EF (1:n).</summary>
    public List<RecurrenceSkipEntity> Skips { get; set; } = new();
}
