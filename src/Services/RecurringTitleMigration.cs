using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services;

/// <summary>
/// Überträgt bei alten lokalen Dateien den Kategorienamen einer Serie in ihren Titel. Bis v0.18
/// war die Kategorie der Name im Kalender und der Titel blieb leer — ohne diese Übernahme stünden
/// alle bestehenden Serien nach dem Umbau namenlos im Plan. Im Server-Modus erledigt das die
/// EF-Migration RecurringFreeTextTitle mit derselben Regel.
/// </summary>
public static class RecurringTitleMigration
{
    /// <returns><c>true</c>, wenn sich etwas geändert hat und die Datei neu geschrieben werden muss.</returns>
    public static bool Apply(IEnumerable<RecurringActivity> rules, IReadOnlyList<ActivityType> types)
    {
        var changed = false;
        foreach (var rule in rules.Where(r => r.LegacyActivityTypeId is not null))
        {
            // Ein selbst vergebener Titel hat Vorrang — genau wie serverseitig.
            if (string.IsNullOrWhiteSpace(rule.Title))
            {
                var name = types.FirstOrDefault(t =>
                    string.Equals(t.Id, rule.LegacyActivityTypeId, StringComparison.OrdinalIgnoreCase))?.Name;
                if (!string.IsNullOrWhiteSpace(name)) rule.Title = name;
            }
            rule.LegacyActivityTypeId = null;
            changed = true;
        }
        return changed;
    }
}
