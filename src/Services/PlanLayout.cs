using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services;

/// <summary>
/// Reine Aufbereitung der tabellarischen Plansicht (Person × Wochentag): Reihenfolge der Personen
/// und die Einträge je Zelle. UI-unabhängig, testbar.
/// </summary>
public static class PlanLayout
{
    /// <summary>Personen in Rollen-Reihenfolge: Eltern → Kinder → Angestellte → Au-Pairs, je Gruppe nach Name.</summary>
    public static IReadOnlyList<User> OrderPeople(IEnumerable<User> users)
        => users
            .OrderBy(u => (int)u.Category)
            .ThenBy(u => string.IsNullOrEmpty(u.DisplayName) ? u.Username : u.DisplayName,
                    StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    /// <summary>
    /// Einträge einer Person an einem Tag für die Zelle: Schichten/Aktivitäten (ohne Nacht-Fortsetzungen)
    /// + Abwesenheiten, nach Startzeit sortiert.
    /// </summary>
    public static List<CalendarEntry> CellEntries(
        IEnumerable<CalendarEntry> timeline, IEnumerable<CalendarEntry> absences, string userId)
        => timeline.Where(e => e.UserId == userId && !e.IsContinuation)
            .Concat(absences.Where(e => e.UserId == userId))
            .OrderBy(e => e.StartTime)
            .ToList();
}
