namespace FlexFamilyCalendar.Models;

public class AppSettings
{
    // Speicher-Modus: false = lokale JSON-Dateien, true = Server-API. Kein Parallelbetrieb (entweder/oder).
    public bool UseServer { get; set; } = false;
    public string ServerUrl { get; set; } = "";  // z.B. https://flexfamily.cloud (nur im Server-Modus genutzt)
    public string ServerTokenEnc { get; set; } = "";  // AES-verschlüsseltes JWT für „Login merken" (kein Passwort)

    public string ActiveAiProvider { get; set; } = "";
    public string AiModel { get; set; } = "";
    public string HolidayState { get; set; } = "BY";  // Bundesland-Code (Feiertage), Admin-Einstellung
    public double OvernightHoursPerDay { get; set; } = 2.0;  // Pauschale Gutschrift je Übernachtung (auf Abruf), global

    // SMTP für den E-Mail-Versand des Plans (Passwort AES-verschlüsselt, nie im Klartext)
    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public string SmtpUser { get; set; } = "";
    public string SmtpFrom { get; set; } = "";
    public bool SmtpUseSsl { get; set; } = true;
    public string SmtpPasswordEnc { get; set; } = "";

    // Nur der Benutzername (kein Geheimnis) — leer = kein Auto-Login
    public string RememberedUsername { get; set; } = "";
    // Values are AES-encrypted base64 — never store API keys in plaintext
    public Dictionary<string, string> EncryptedApiKeys { get; set; } = new();
}
