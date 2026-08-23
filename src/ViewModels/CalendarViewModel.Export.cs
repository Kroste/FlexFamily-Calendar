using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using System.Globalization;

namespace FlexFamilyCalendar.ViewModels;

/// <summary>
/// PDF-Export und Mail-Versand des Wochenplans, jeweils aus der Sicht des Empfängers.
/// </summary>
public partial class CalendarViewModel
{
    [RelayCommand]
    private void ExportPdf()
    {
        LogService.Click(CurrentUser.Username, $"PDF-Export ({WeekLabel})");
        ExportPdfRequested?.Invoke();
    }

    /// <summary>Export-Modell aus Sicht des aktuellen Benutzers (für den „PDF"-Button).</summary>
    public WeekExport CreateWeekExport() => CreateWeekExport(CurrentUser);

    /// <summary>Baut das Export-Modell als Tabelle (Person × Wochentag) aus Sicht von <paramref name="viewer"/> (Datenschutz-Maskierung).</summary>
    public WeekExport CreateWeekExport(User viewer)
    {
        string TypeLabel(EntryType t) => Localizer.Instance[EntryTypeInfo.Key(t)];
        var isAdmin = viewer.Role == UserRole.Admin;

        var headers = Days.Select(d => new PlanDayHeader(d.DayName, d.DateLabel, d.HolidayName)).ToList();
        // Personalisierte Hinweise: jeder Empfänger sieht nur die für ihn relevanten (oder alle, wenn Admin).
        var notes = Days.Select(d =>
            PlanExportBuilder.NoteFor(d.RawNote, d.NoteUserId, isAdmin, viewer.Id)).ToList();

        var rows = new List<PlanPersonRow>();
        foreach (var r in Rows)
        {
            var cells = r.Cells
                .Select(c => (IReadOnlyList<PlanCellEntry>)c.Entries
                    .Where(PlanExportBuilder.IsInExport)
                    .Select(e => PlanExportBuilder.CellEntry(e, isAdmin, viewer.Id, TypeLabel)).ToList())
                .ToList();
            rows.Add(new PlanPersonRow(r.Name, r.Color, r.CategoryLabel, cells));
        }

        var generated = string.Format(Localizer.Instance["Pdf_Generated"],
            DateTime.Now.ToString("g", CultureInfo.CurrentCulture));
        return new WeekExport(Localizer.Instance["Pdf_Title"], WeekLabel, generated, headers, rows, notes);
    }

    /// <summary>Plan per E-Mail senden: prüft die SMTP-Konfiguration und öffnet die Empfänger-Auswahl (nur Admin).</summary>
    [RelayCommand]
    private async Task MailPlan()
    {
        if (!IsAdmin) return;
        // Local-Modus prüft SMTP-Settings hier; Server-Modus lässt durch (Server entscheidet beim Senden).
        if (!await _mailSender.IsConfiguredAsync()) { LogService.Warn(Localizer.Instance["Mail_NotConfigured"]); return; }
        var recipients = MailComposer.RecipientsWithEmail(_allUsers);
        if (recipients.Count == 0) { LogService.Warn(Localizer.Instance["Mail_NoRecipients"]); return; }

        LogService.Click(CurrentUser.Username, $"Mail-Versand ({WeekLabel})");
        MailDialogRequested?.Invoke(new MailViewModel(recipients));
    }

    /// <summary>Sendet jedem Empfänger ein eigenes, aus seiner Sicht maskiertes Wochen-PDF (Local: pro Adresse
    /// ein SmtpClient.SendMail; Server: ein Batch-POST an /api/mail/send-week-plan).</summary>
    public async Task SendPlanMailAsync(IReadOnlyList<string> emails)
    {
        if (emails.Count == 0) return;
        var subject = $"{Localizer.Instance["Pdf_Title"]} {WeekLabel}";
        var body = string.Format(Localizer.Instance["Mail_Body"], WeekLabel);

        // PDFs pro Empfänger client-seitig rendern (Datenschutz: jeder bekommt seine Sicht).
        var items = new List<MailSendItem>();
        foreach (var email in emails)
        {
            var viewer = _allUsers.FirstOrDefault(u =>
                u.Email.Trim().Equals(email, StringComparison.OrdinalIgnoreCase));
            if (viewer is null) continue;
            var pdf = PdfExportService.Render(CreateWeekExport(viewer));
            items.Add(new MailSendItem(email, pdf));
        }
        if (items.Count == 0) { LogService.Warn(Localizer.Instance["Mail_NoRecipients"]); return; }

        try
        {
            var result = await _mailSender.SendWeekPlanAsync(subject, body, ExportFileName, items);
            LogService.Info(string.Format(Localizer.Instance["Mail_Sent"], result.Sent));
            foreach (var err in result.Errors)
                LogService.Warn("Mail: {0}", err);
        }
        catch (Exception ex)
        {
            LogService.Error("Mail-Versand fehlgeschlagen", ex);
        }
    }

    /// <summary>Vorgeschlagener Dateiname für den PDF-Export der aktuellen Woche.</summary>
    public string ExportFileName
    {
        get
        {
            var kw = ISOWeek.GetWeekOfYear(WeekStart.ToDateTime(TimeOnly.MinValue));
            return $"Plan_KW{kw:D2}_{WeekStart.Year}.pdf";
        }
    }
}
