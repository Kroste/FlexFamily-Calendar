using System.Text.Json;

namespace FlexFamilyCalendar.Services;

/// <summary>
/// Einheitliche JSON-Serialisierungs-Optionen für alle lokalen JSON-Dateien
/// (users.json, entries/YYYY/WW/YYYY-MM-DD.json etc.). PascalCase MUSS erhalten
/// bleiben, weil bestehende Nutzer-Datenbestände aus der Newtonsoft-Ära so
/// abgelegt sind — S.T.J würde sonst per Default in camelCase serialisieren und
/// die Files wären nach dem nächsten Schreiben nicht mehr abwärtskompatibel.
/// <c>PropertyNameCaseInsensitive</c> ist eine defensive Netz beim Lesen.
/// </summary>
public static class JsonOptions
{
    public static readonly JsonSerializerOptions Pretty = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null,
        PropertyNameCaseInsensitive = true
    };

    public static readonly JsonSerializerOptions Compact = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = null,
        PropertyNameCaseInsensitive = true
    };
}
