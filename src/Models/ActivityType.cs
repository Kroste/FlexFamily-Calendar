namespace FlexFamilyCalendar.Models;

/// <summary>
/// Eine vom Admin konfigurierbare Aktivitäts-Kategorie (z.B. Schule, Sport, Sprachkurs), verfeinert
/// Einträge vom Typ <see cref="EntryType.Activity"/>. Gilt für eine oder mehrere Personentypen.
/// </summary>
public class ActivityType
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Color { get; set; } = "";
    public List<PersonCategory> Categories { get; set; } = new();

    /// <summary>Ist dieser Aktivitätstyp für die gegebene Rolle wählbar?</summary>
    public bool AppliesTo(PersonCategory category) => Categories.Contains(category);
}
