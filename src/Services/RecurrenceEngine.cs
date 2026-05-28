using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services;

/// <summary>
/// Reine, UI-unabhängige Projektion wiederkehrender Aktivitäten auf ein Datum.
/// Erzeugt Laufzeit-<see cref="CalendarEntry"/> (IsRecurring=true) als Overlay – nicht persistiert.
/// </summary>
public static class RecurrenceEngine
{
    /// <summary>
    /// Projiziert alle Regeln, die an <paramref name="date"/> stattfinden. <paramref name="isHoliday"/>
    /// steuert das Feiertags-Verhalten je Regel: SkipOnHolidays → ausgeblendet; sonst HolidayConflict gesetzt.
    /// Ergebnis ist nach Startzeit sortiert.
    /// </summary>
    public static List<CalendarEntry> Project(IEnumerable<RecurringActivity> rules, DateOnly date, bool isHoliday)
    {
        var result = new List<CalendarEntry>();
        foreach (var r in rules)
        {
            if (!r.OccursOn(date)) continue;
            if (isHoliday && r.SkipOnHolidays) continue;

            result.Add(new CalendarEntry
            {
                Id = $"recurring:{r.Id}:{date:yyyy-MM-dd}",
                UserId = r.UserId,
                UserDisplayName = r.UserDisplayName,
                Type = EntryType.Activity,
                StartTime = r.StartTime,
                EndTime = r.EndTime,
                Title = r.Title,
                ActivityTypeId = r.ActivityTypeId,
                IsRecurring = true,
                HolidayConflict = isHoliday   // nur erreichbar, wenn die Regel an Feiertagen nicht ausgeblendet ist
            });
        }
        result.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
        return result;
    }
}
