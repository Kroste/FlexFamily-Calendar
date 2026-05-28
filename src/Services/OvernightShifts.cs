using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services;

/// <summary>
/// Reine Erzeugung der Folgetag-Fortsetzungen für Schichten über Mitternacht (z.B. 20:00–06:00):
/// am Folgetag wird ein Block 00:00→Endzeit gezeigt. Display-only (IsContinuation), zählt nicht erneut.
/// </summary>
public static class OvernightShifts
{
    public static List<CalendarEntry> Continuations(IEnumerable<CalendarEntry> previousDayEntries)
    {
        var result = new List<CalendarEntry>();
        foreach (var e in previousDayEntries)
        {
            if (!e.ContinuesNextDay) continue;
            if (EntryTypeInfo.IsAbsence(e.Type)) continue;   // nur Arbeit/Aktivität laufen über Mitternacht

            result.Add(new CalendarEntry
            {
                Id = e.Id,                       // gleiche Id → Verweis auf die Originalschicht des Vortags
                UserId = e.UserId,
                UserDisplayName = e.UserDisplayName,
                Type = e.Type,
                StartTime = TimeSpan.Zero,
                EndTime = e.EndTime,
                Title = e.Title,
                ActivityTypeId = e.ActivityTypeId,
                IsContinuation = true,
                OwnerColor = e.OwnerColor,
                EffectiveOpacity = e.EffectiveOpacity * 0.8,   // Fortsetzung dezent gedämpft
                DisplayType = e.DisplayType,
                DisplayTitle = e.DisplayTitle,
                ActivityName = e.ActivityName,
                ActivityColor = e.ActivityColor
            });
        }
        return result;
    }
}
