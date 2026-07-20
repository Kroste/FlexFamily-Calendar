using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services;

/// <summary>
/// Reine Aufbereitung der tabellarischen Plansicht (Person × Wochentag): Reihenfolge der Personen
/// und die Einträge je Zelle. UI-unabhängig, testbar.
/// </summary>
public static class PlanLayout
{
    /// <summary>Fallback-Rang nach Rolle für Personen, die noch keine explizite <c>PlanOrder</c> vom Admin haben.</summary>
    private static int Rank(PersonCategory c) => c switch
    {
        PersonCategory.Parent => 0,
        PersonCategory.AuPair => 1,
        PersonCategory.Employee => 2,
        PersonCategory.Child => 3,
        _ => 9
    };

    /// <summary>
    /// Personen für die Planansicht sortieren. Primär die vom Admin gesetzte <see cref="User.PlanOrder"/>
    /// (per Drag&amp;Drop pflegbar); bei Gleichstand Rollen-Rang und Anzeigename als stabiler Fallback.
    /// </summary>
    public static IReadOnlyList<User> OrderPeople(IEnumerable<User> users)
        => users
            .OrderBy(u => u.PlanOrder)
            .ThenBy(u => Rank(u.Category))
            .ThenBy(u => string.IsNullOrEmpty(u.DisplayName) ? u.Username : u.DisplayName,
                    StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    /// <summary>
    /// Einträge einer Person an einem Tag für die Zelle: Schichten/Aktivitäten + Abwesenheiten,
    /// nach Startzeit sortiert.
    /// </summary>
    public static List<CalendarEntry> CellEntries(
        IEnumerable<CalendarEntry> timeline, IEnumerable<CalendarEntry> absences, string userId)
        => timeline.Where(e => e.UserId == userId)
            .Concat(absences.Where(e => e.UserId == userId))
            .OrderBy(e => e.StartTime)
            .ToList();
}
