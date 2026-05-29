namespace FlexFamilyCalendar.Services;

/// <summary>
/// Minimaler Schlüssel/Wert-Speicher (z.B. Browser-localStorage). Vom Browser-Head implementiert,
/// damit die Library frei von JS-Interop bleibt.
/// </summary>
public interface IBrowserKeyValueStore
{
    string? Get(string key);
    void Set(string key, string value);
}
