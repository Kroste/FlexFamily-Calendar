namespace FlexFamilyCalendar.Services;

/// <summary>Speichert nur im Prozessspeicher (Fallback/Tests, kein Persist).</summary>
public class InMemoryBrowserKeyValueStore : IBrowserKeyValueStore
{
    private readonly Dictionary<string, string> _data = new();

    public string? Get(string key) => _data.TryGetValue(key, out var v) ? v : null;

    public void Set(string key, string value) => _data[key] = value;
}
