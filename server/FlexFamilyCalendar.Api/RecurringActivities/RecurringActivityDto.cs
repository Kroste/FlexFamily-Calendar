using FlexFamilyCalendar.Api.Models;

namespace FlexFamilyCalendar.Api.RecurringActivities;

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
    bool SkipOnHolidays)
{
    public static RecurringActivityDto From(RecurringActivityEntity e) => new(
        e.Id, e.UserId, e.UserDisplayName, e.Title, e.ActivityTypeId,
        e.StartTime, e.EndTime, e.Weekdays, e.SkipOnHolidays);
}
