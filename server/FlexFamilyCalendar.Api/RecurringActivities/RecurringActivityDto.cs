using FlexFamilyCalendar.Api.Models;

namespace FlexFamilyCalendar.Api.RecurringActivities;

/// <summary>Tagesgenaue Aussetzung (From/To inklusiv); optional ein Grund (Urlaub/Krank/…).</summary>
public record RecurrenceSkipDto(Guid Id, DateOnly From, DateOnly To, string? Reason)
{
    public static RecurrenceSkipDto FromEntity(RecurrenceSkipEntity e) => new(e.Id, e.From, e.To, e.Reason);
}

/// <summary>Wiederkehrende Aktivität für Lesen und Ersetzen (PUT). Weekdays = DayOfWeek-Werte 0..6.</summary>
public record RecurringActivityDto(
    Guid Id,
    string UserId,
    string UserDisplayName,
    string Title,
    string? ActivityTypeId,
    TimeOnly StartTime,
    TimeOnly EndTime,
    List<int> Weekdays,
    bool SkipOnHolidays,
    List<RecurrenceSkipDto> Skips)
{
    public static RecurringActivityDto From(RecurringActivityEntity e) => new(
        e.Id, e.UserId, e.UserDisplayName, e.Title, e.ActivityTypeId,
        e.StartTime, e.EndTime, e.Weekdays, e.SkipOnHolidays,
        e.Skips.Select(RecurrenceSkipDto.FromEntity).ToList());
}
