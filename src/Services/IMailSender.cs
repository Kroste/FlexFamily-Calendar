namespace FlexFamilyCalendar.Services;

public record MailSendItem(string Email, byte[] Pdf);

public record MailSendResult(int Sent, int Failed, IReadOnlyList<string> Errors);

/// <summary>
/// Plattform-Backend für den Plan-Mail-Versand. Auf Desktop schickt
/// <see cref="LocalMailSender"/> direkt per <c>SmtpClient</c>; im Server-/Browser-Modus
/// schickt <see cref="Api.ApiMailSender"/> einen Batch an <c>/api/mail/send-week-plan</c>.
/// </summary>
public interface IMailSender
{
    /// <summary>True = SMTP-Konfig liegt serverseitig (ENV, Smtp__Host etc.); der Settings-Tab
    /// blendet seine SMTP-Sektion dann aus, weil die Werte nicht benutzt werden.</summary>
    bool IsServerConfigured { get; }

    /// <summary>Schneller Vorabcheck, ob Versand grundsätzlich möglich ist
    /// (Local: SMTP-Felder gesetzt; Server: immer true — Server entscheidet beim Senden).</summary>
    Task<bool> IsConfiguredAsync();

    Task<MailSendResult> SendWeekPlanAsync(
        string subject, string body, string fileName, IReadOnlyList<MailSendItem> recipients);
}
