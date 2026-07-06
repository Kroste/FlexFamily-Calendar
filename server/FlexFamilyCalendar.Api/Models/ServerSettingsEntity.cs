namespace FlexFamilyCalendar.Api.Models;

/// <summary>
/// Server-seitige Domänen-Einstellungen (Single-Row-Konfiguration, Id fest = 1). Enthält
/// Einstellungen, die im Server-Modus für alle Clients identisch sein müssen —
/// Feiertags-Bundesland und pauschale Übernachtungs-Gutschrift. Installations-Config
/// (ServerUrl, JWT, API-Keys) bleibt lokal am Client.
/// </summary>
public class ServerSettingsEntity
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    /// <summary>Bundesland-Kürzel für die Feiertagsberechnung (z.B. "BY", "NW"). Default BY.</summary>
    public string HolidayState { get; set; } = "BY";

    /// <summary>Pauschale Stunden-Gutschrift je Übernachtung (auf Abruf). Default 2.0.</summary>
    public double OvernightHoursPerDay { get; set; } = 2.0;
}
