namespace FlexFamilyCalendar.Models;

/// <summary>
/// Tagesgenauer Aussetzungs-Zeitraum für eine <see cref="RecurringActivity"/>.
/// From und To sind inklusiv — der Tag To ist selbst noch pausiert.
/// </summary>
public class RecurrenceSkip
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }

    /// <summary>Optionaler Grund (z.B. „Urlaub", „krank") — nur fürs UI, nicht logisch ausgewertet.</summary>
    public string? Reason { get; set; }

    public bool Contains(DateOnly date) => date >= From && date <= To;
}
