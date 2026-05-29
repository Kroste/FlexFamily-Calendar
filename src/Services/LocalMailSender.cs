using System.Net;
using System.Net.Mail;

namespace FlexFamilyCalendar.Services;

/// <summary>Desktop-Backend: liest die SMTP-Settings lokal und sendet je Empfänger einzeln
/// (Datenschutz: jeder bekommt sein vom CalendarVM vorab maskiertes PDF).</summary>
public class LocalMailSender : IMailSender
{
    private readonly IStorageService _storage;

    public LocalMailSender(IStorageService storage) => _storage = storage;

    public async Task<bool> IsConfiguredAsync()
    {
        var s = await _storage.LoadSettingsAsync();
        return MailComposer.IsConfigured(s);
    }

    public async Task<MailSendResult> SendWeekPlanAsync(
        string subject, string body, string fileName, IReadOnlyList<MailSendItem> recipients)
    {
        var settings = await _storage.LoadSettingsAsync();
        var sent = 0;
        var failed = 0;
        var errors = new List<string>();

        foreach (var r in recipients)
        {
            try
            {
                using var msg = new MailMessage(settings.SmtpFrom, r.Email, subject, body);
                using var ms = new MemoryStream(r.Pdf);
                msg.Attachments.Add(new Attachment(ms, fileName, "application/pdf"));

                using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort) { EnableSsl = settings.SmtpUseSsl };
                if (!string.IsNullOrWhiteSpace(settings.SmtpUser) && !string.IsNullOrEmpty(settings.SmtpPasswordEnc))
                    client.Credentials = new NetworkCredential(settings.SmtpUser, SecretService.Decrypt(settings.SmtpPasswordEnc));

                await client.SendMailAsync(msg);
                LogService.Debug("Plan-Mail gesendet an {0}", r.Email);
                sent++;
            }
            catch (Exception ex)
            {
                LogService.Warn("Plan-Mail an {0} fehlgeschlagen: {1}", r.Email, ex.Message);
                failed++;
                errors.Add($"{r.Email}: {ex.Message}");
            }
        }
        return new MailSendResult(sent, failed, errors);
    }
}
