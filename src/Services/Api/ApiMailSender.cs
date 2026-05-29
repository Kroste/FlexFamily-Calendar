namespace FlexFamilyCalendar.Services.Api;

/// <summary>Server-/Browser-Backend: schickt den ganzen Batch (mit pro Empfänger vor-gerendertem
/// PDF) an <c>/api/mail/send-week-plan</c>. SMTP-Konfig steckt serverseitig in ENV-Variablen.</summary>
public class ApiMailSender : IMailSender
{
    private readonly ApiClient _api;

    public ApiMailSender(ApiClient api) => _api = api;

    public Task<bool> IsConfiguredAsync() => Task.FromResult(true);   // Server entscheidet

    public async Task<MailSendResult> SendWeekPlanAsync(
        string subject, string body, string fileName, IReadOnlyList<MailSendItem> recipients)
    {
        var dtoRecipients = recipients
            .Select(r => new ServerMailRecipientDto(r.Email, Convert.ToBase64String(r.Pdf)))
            .ToList();
        var body2 = body ?? "";
        var req = new SendWeekPlanBody(subject, body2, fileName, dtoRecipients);
        var resp = await _api.SendWeekPlanAsync(req);
        return new MailSendResult(resp.Sent, resp.Failed, resp.Errors ?? new List<string>());
    }
}
