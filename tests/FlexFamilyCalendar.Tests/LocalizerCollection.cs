using Xunit;

namespace FlexFamilyCalendar.Tests;

/// <summary>
/// Serialisiert alle Tests, die am globalen <c>Localizer</c>-Singleton die Sprache umschalten.
/// Ohne die Sperre zählen Notification-Tests die Sprachwechsel anderer Tests mit, und das
/// Ergebnis hängt an der Ausführungsreihenfolge — unter xunit.v3 ist die nicht mehr stabil.
/// </summary>
[CollectionDefinition("Localizer", DisableParallelization = true)]
public class LocalizerCollection { }
