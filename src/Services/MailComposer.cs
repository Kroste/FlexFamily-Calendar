using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services;

public record MailRecipient(string Name, string Email);

/// <summary>Reine Aufbereitung für den Mail-Versand (Empfänger, Konfig-Prüfung) — UI-/IO-unabhängig, testbar.</summary>
public static class MailComposer
{
    /// <summary>SMTP ist nutzbar konfiguriert (Server + Absender vorhanden).</summary>
    public static bool IsConfigured(AppSettings s)
        => !string.IsNullOrWhiteSpace(s.SmtpHost) && !string.IsNullOrWhiteSpace(s.SmtpFrom);

    /// <summary>Personen mit (plausibler) E-Mail-Adresse, nach Name sortiert.</summary>
    public static IReadOnlyList<MailRecipient> RecipientsWithEmail(IEnumerable<User> users)
        => users.Where(u => LooksLikeEmail(u.Email))
            .Select(u => new MailRecipient(
                string.IsNullOrEmpty(u.DisplayName) ? u.Username : u.DisplayName, u.Email.Trim()))
            .OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    /// <summary>Einfache Plausibilitätsprüfung (keine vollständige RFC-Validierung).</summary>
    public static bool LooksLikeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        var e = email.Trim();
        var at = e.IndexOf('@');
        return at > 0 && at < e.Length - 1 && !e.Contains(' ') && e.LastIndexOf('.') > at;
    }
}
