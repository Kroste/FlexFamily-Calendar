using System.Net;
using System.Net.Mail;
using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services;

/// <summary>Versendet den Wochenplan per E-Mail (SMTP, abhängigkeitsfrei über System.Net.Mail).</summary>
public static class MailService
{
    /// <summary>Sendet eine Mail mit PDF-Anhang. Empfänger stehen im BCC (Adressen bleiben untereinander verborgen).</summary>
    public static async Task SendAsync(AppSettings s, IReadOnlyList<string> recipients,
        string subject, string body, byte[] attachment, string fileName)
    {
        using var msg = new MailMessage { From = new MailAddress(s.SmtpFrom), Subject = subject, Body = body };
        msg.To.Add(s.SmtpFrom);                       // Kopie an den Absender; eigentliche Empfänger im BCC
        foreach (var r in recipients) msg.Bcc.Add(r);

        using var ms = new MemoryStream(attachment);
        msg.Attachments.Add(new Attachment(ms, fileName, "application/pdf"));

        using var client = new SmtpClient(s.SmtpHost, s.SmtpPort) { EnableSsl = s.SmtpUseSsl };
        if (!string.IsNullOrWhiteSpace(s.SmtpUser) && !string.IsNullOrEmpty(s.SmtpPasswordEnc))
            client.Credentials = new NetworkCredential(s.SmtpUser, SecretService.Decrypt(s.SmtpPasswordEnc));

        await client.SendMailAsync(msg);
        LogService.Debug("Plan-Mail gesendet an {0} Empfänger", recipients.Count);
    }
}
