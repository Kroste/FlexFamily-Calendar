using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services.Api;

/// <summary>Übersetzt zwischen Server-DTO und Desktop-<see cref="RecurringActivity"/> (Weekdays als int 0..6).</summary>
public static class RecurringActivityMapping
{
    public static RecurringActivity ToDesktop(ServerRecurringActivityDto d) => new()
    {
        Id = d.Id,
        UserId = d.UserId,
        UserDisplayName = d.UserDisplayName ?? "",
        Title = d.Title ?? "",
        ActivityTypeId = string.IsNullOrWhiteSpace(d.ActivityTypeId) ? null : d.ActivityTypeId,
        StartTime = d.StartTime.ToTimeSpan(),
        EndTime = d.EndTime.ToTimeSpan(),
        Weekdays = (d.Weekdays ?? new()).Select(i => (DayOfWeek)i).ToList(),
        SkipOnHolidays = d.SkipOnHolidays
    };

    public static ServerRecurringActivityDto ToServer(RecurringActivity a) => new(
        a.Id, a.UserId, a.UserDisplayName, a.Title,
        string.IsNullOrWhiteSpace(a.ActivityTypeId) ? null : a.ActivityTypeId,
        TimeOnly.FromTimeSpan(a.StartTime), TimeOnly.FromTimeSpan(a.EndTime),
        a.Weekdays.Select(d => (int)d).ToList(), a.SkipOnHolidays);
}
