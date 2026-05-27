namespace FlexFamilyCalendar.Models;

public class CalendarEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = "";
    public string UserDisplayName { get; set; } = "";
    public EntryType Type { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string Title { get; set; } = "";
    public string Notes { get; set; } = "";

    [Newtonsoft.Json.JsonIgnore]
    public double DurationHours => (EndTime - StartTime).TotalHours;

    [Newtonsoft.Json.JsonIgnore]
    public string TimeRange => $"{StartTime:hh\\:mm}–{EndTime:hh\\:mm}";

    [Newtonsoft.Json.JsonIgnore]
    public string EntryColor => EntryTypeInfo.Color(Type);

    [Newtonsoft.Json.JsonIgnore]
    public string TypeLabel => EntryTypeInfo.Label(Type);
}
