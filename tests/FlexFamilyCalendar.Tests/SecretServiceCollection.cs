using Xunit;

namespace FlexFamilyCalendar.Tests;

/// <summary>
/// Serialisiert alle Tests, die den globalen, statischen <c>SecretService</c> (re-)initialisieren,
/// damit sie sich nicht gegenseitig den AES-Schlüssel unter den Füßen wegziehen.
/// </summary>
[CollectionDefinition("SecretService")]
public class SecretServiceCollection { }
