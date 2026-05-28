namespace FlexFamilyCalendar.Models;

public class AppSettings
{
    public string ActiveAiProvider { get; set; } = "";
    public string AiModel { get; set; } = "";
    public string HolidayState { get; set; } = "BY";  // Bundesland-Code (Feiertage), Admin-Einstellung
    public double OvernightHoursPerDay { get; set; } = 2.0;  // Pauschale Gutschrift je Übernachtung (auf Abruf), global
    // Nur der Benutzername (kein Geheimnis) — leer = kein Auto-Login
    public string RememberedUsername { get; set; } = "";
    // Values are AES-encrypted base64 — never store API keys in plaintext
    public Dictionary<string, string> EncryptedApiKeys { get; set; } = new();
}
