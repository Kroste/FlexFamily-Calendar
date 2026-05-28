using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services;

/// <summary>
/// Reine Aufbereitung der tabellarischen Plansicht (Person × Wochentag): Reihenfolge der Personen
/// und die Einträge je Zelle. UI-unabhängig, testbar.
/// </summary>
public static class PlanLayout
{
    /// <summary>Anzeige-Reihenfolge der Rollen (unabhängig vom persistierten Enum-Wert): Eltern → Au-Pair → Angestellte → Kinder.</summary>
    private static int Rank(PersonCategory c) => c switch
    {
        PersonCategory.Parent => 0,
        PersonCategory.AuPair => 1,
        PersonCategory.Employee => 2,
        PersonCategory.Child => 3,
        _ => 9
    };

    /// <summary>Personen in Rollen-Reihenfolge: Eltern → Au-Pair → Angestellte → Kinder, je Gruppe nach Name.</summary>
    public static IReadOnlyList<User> OrderPeople(IEnumerable<User> users)
        => users
            .OrderBy(u => Rank(u.Category))
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
