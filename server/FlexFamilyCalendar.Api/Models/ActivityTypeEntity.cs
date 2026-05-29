namespace FlexFamilyCalendar.Api.Models;

/// <summary>Vom Admin konfigurierbare Aktivitäts-Kategorie (Name + Farbe), wählbar für bestimmte Personentypen.</summary>
public class ActivityTypeEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Color { get; set; } = "";
    public List<string> Categories { get; set; } = new();   // PersonCategory-Namen, z.B. ["Child","Employee"] → text[]
}
