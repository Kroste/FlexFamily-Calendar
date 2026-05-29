namespace FlexFamilyCalendar.Api.Mail;

/// <summary>SMTP-Konfiguration aus ENV-Variablen (Smtp__Host etc., 12-Factor-Style wie Jwt__*).
/// Keine DB-Spalte, kein Admin-UI — Operator-Setting.</summary>
public class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string From { get; set; } = "";
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public bool UseSsl { get; set; } = true;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(From);
}
