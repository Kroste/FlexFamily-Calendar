namespace FlexFamilyCalendar.Models;

/// <summary>Fachliche Kategorie (unabhängig von der Zugriffsrolle <see cref="UserRole"/>).</summary>
public enum PersonCategory
{
    Parent,    // Eltern
    Child,     // Kind
    Employee,  // Angestellte/r
    AuPair     // Au-Pair
}
