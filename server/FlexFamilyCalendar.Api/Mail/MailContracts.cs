namespace FlexFamilyCalendar.Api.Mail;

/// <summary>Ein Empfänger: Adresse + sein eigenes PDF (Datenschutz: jeder Empfänger bekommt
/// einen aus SEINER Sicht maskierten Wochenplan; der Client rendert vor dem Senden).</summary>
public record MailRecipientDto(string Email, string PdfBase64);

public record SendWeekPlanRequest(
    string Subject,
    string Body,
    string FileName,
    List<MailRecipientDto> Recipients);

public record SendWeekPlanResponse(int Sent, int Failed, List<string> Errors);
