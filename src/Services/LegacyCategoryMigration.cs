using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services;

/// <summary>Eine Kategorie aus der alten <c>activity-types.json</c> — nur noch für die Übernahme gelesen.</summary>
public record LegacyCategory(string Id, string Name, string Color);

/// <summary>
/// Übernimmt bei alten LOKALEN Dateien, was die Kategorien einem Eintrag bzw. einer Serie gegeben
/// haben: den Namen als Bezeichnung und die Farbe als eigene Kachelfarbe. Bis v0.20 waren sie der
/// Name im Kalender; ohne Übernahme stünden die Einträge danach namenlos und in anderer Farbe im
/// Plan. Im Server-Modus erledigen das die EF-Migrationen RecurringFreeTextTitle und
/// DropCategories mit denselben Regeln: eigene Bezeichnung und eigene Farbe haben Vorrang, nur
/// gültiges Hex wird übernommen, die Id wird ohne Rücksicht auf Groß-/Kleinschreibung verglichen.
/// </summary>
public static class LegacyCategoryMigration
{
    /// <returns><c>true</c>, wenn sich etwas geändert hat und die Datei neu geschrieben werden muss.</returns>
    public static bool ApplyToEntries(IEnumerable<CalendarEntry> entries, IReadOnlyList<LegacyCategory> categories)
    {
        var changed = false;
        foreach (var e in entries.Where(e => e.LegacyActivityTypeId is not null))
        {
            var category = Find(categories, e.LegacyActivityTypeId);
            if (category is not null)
            {
                if (string.IsNullOrWhiteSpace(e.Title) && !string.IsNullOrWhiteSpace(category.Name))
                    e.Title = category.Name;
                if (string.IsNullOrWhiteSpace(e.Color) && EntryColors.IsValidHex(category.Color))
                    e.Color = category.Color.ToUpperInvariant();
            }
            e.LegacyActivityTypeId = null;
            changed = true;
        }
        return changed;
    }

    /// <summary>Serien: nur der Name — eine Farbe hatten Serien vor dem Umbau nur über die Kategorie,
    /// und die Serien-Migration (v0.19) lief ohne Farbübernahme; das bleibt hier gleich.</summary>
    public static bool ApplyToRules(IEnumerable<RecurringActivity> rules, IReadOnlyList<LegacyCategory> categories)
    {
        var changed = false;
        foreach (var rule in rules.Where(r => r.LegacyActivityTypeId is not null))
        {
            if (string.IsNullOrWhiteSpace(rule.Title) && Find(categories, rule.LegacyActivityTypeId) is { } category
                && !string.IsNullOrWhiteSpace(category.Name))
                rule.Title = category.Name;
            rule.LegacyActivityTypeId = null;
            changed = true;
        }
        return changed;
    }

    private static LegacyCategory? Find(IReadOnlyList<LegacyCategory> categories, string? id)
        => categories.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
}
