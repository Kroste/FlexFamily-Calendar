using System.Net;
using System.Net.Mail;
using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services;

/// <summary>Versendet den Wochenplan per E-Mail (SMTP, abhängigkeitsfrei über System.Net.Mail).</summary>
public static class MailService
{
    /// <summary>Sendet eine Mail mit PDF-Anhang an genau einen Empfänger (jeder bekommt sein eigenes, maskiertes PDF).</summary>
    public static async Task SendAsync(AppSettings s, string recipient,
        string subject, string body, byte[] attachment, string fileName)
    {
        using var msg = new MailMessage(s.SmtpFrom, recipient, subject, body);

        using var ms = new MemoryStream(attachment);
        msg.Attachments.Add(new Attachment(ms, fileName, "application/pdf"));

        using var client = new SmtpClient(s.SmtpHost, s.SmtpPort) { EnableSsl = s.SmtpUseSsl };
        if (!string.IsNullOrWhiteSpace(s.SmtpUser) && !string.IsNullOrEmpty(s.SmtpPasswordEnc))
            client.Credentials = new NetworkCredential(s.SmtpUser, SecretService.Decrypt(s.SmtpPasswordEnc));

        await client.SendMailAsync(msg);
        LogService.Debug("Plan-Mail gesendet an {0}", recipient);
    }
}
