using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services;

/// <summary>
/// Reine Materialisierung einer Abwesenheit (Urlaub/Krank/Abwesend) über einen Datumsbereich:
/// je Tag ein <see cref="CalendarEntry"/>, alle über dieselbe GroupId verbunden. UI-unabhängig, testbar.
/// </summary>
public static class AbsencePlanner
{
    public static List<(DateOnly Date, CalendarEntry Entry)> Build(
        CalendarEntry template, DateOnly from, DateOnly to, string groupId)
    {
        if (to < from) (from, to) = (to, from);

        var result = new List<(DateOnly, CalendarEntry)>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            result.Add((d, new CalendarEntry
            {
                Id = Guid.NewGuid().ToString(),
                UserId = template.UserId,
                UserDisplayName = template.UserDisplayName,
                Type = template.Type,
                StartTime = template.StartTime,
                EndTime = template.EndTime,
                Title = template.Title,
                Notes = template.Notes,
                ActivityTypeId = template.ActivityTypeId,
                AbsenceGroupId = groupId,
                AbsenceStart = from,
                AbsenceEnd = to
            }));
        }
        return result;
    }
}
