namespace FlexFamilyCalendar.Models;

public class CalendarDay
{
    public string DateString { get; set; } = "";  // "2026-05-25"
    public bool IsFinalized { get; set; }
    public List<CalendarEntry> Entries { get; set; } = new();

    [Newtonsoft.Json.JsonIgnore]
    public DateOnly Date => DateOnly.Parse(DateString);
}
