using System.Net;
using System.Net.Mail;

namespace FlexFamilyCalendar.Api.Mail;

/// <summary>Versendet eine PDF-behaftete Mail über SMTP. Stateless, je Empfänger ein Send.</summary>
public class MailSender
{
    private readonly SmtpOptions _opts;
    private readonly ILogger<MailSender> _log;

    public MailSender(SmtpOptions opts, ILogger<MailSender> log)
    {
        _opts = opts;
        _log = log;
    }

    public async Task<(bool Ok, string? Error)> SendAsync(
        string recipient, string subject, string body, byte[] pdf, string fileName)
    {
        if (!_opts.IsConfigured)
            return (false, "SMTP serverseitig nicht konfiguriert (Smtp__Host/Smtp__From fehlen).");

        try
        {
            using var msg = new MailMessage(_opts.From, recipient, subject, body);
            using var ms = new MemoryStream(pdf);
            msg.Attachments.Add(new Attachment(ms, fileName, "application/pdf"));

            using var client = new SmtpClient(_opts.Host, _opts.Port) { EnableSsl = _opts.UseSsl };
            if (!string.IsNullOrWhiteSpace(_opts.User) && !string.IsNullOrWhiteSpace(_opts.Password))
                client.Credentials = new NetworkCredential(_opts.User, _opts.Password);

            await client.SendMailAsync(msg);
            _log.LogInformation("Plan-Mail gesendet an {Recipient}", recipient);
            return (true, null);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Plan-Mail an {Recipient} fehlgeschlagen", recipient);
            return (false, ex.Message);
        }
    }
}
