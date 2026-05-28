using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FlexFamilyCalendar.Services;

/// <summary>Rendert einen <see cref="WeekExport"/> als PDF (A4 quer, 7 Tagesspalten) — UI-unabhängig.</summary>
public static class PdfExportService
{
    static PdfExportService() => QuestPDF.Settings.License = LicenseType.Community;

    public static byte[] Render(WeekExport export)
    {
        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(t => t.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Text(export.Title).FontSize(16).Bold();
                    col.Item().Text(export.WeekLabel).FontSize(11).FontColor(Colors.Grey.Darken1);
                });

                page.Content().PaddingVertical(8).Row(row =>
                {
                    foreach (var day in export.Days)
                    {
                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Column(c =>
                        {
                            c.Item().Text(day.DayName).Bold();
                            c.Item().Text(day.DateLabel).FontSize(8).FontColor(Colors.Grey.Darken1);

                            if (!string.IsNullOrEmpty(day.Holiday))
                                c.Item().Text(day.Holiday).FontSize(8).FontColor(Colors.Orange.Darken2);
                            if (!string.IsNullOrEmpty(day.Note))
                                c.Item().Text(day.Note).FontSize(8).Italic();

                            foreach (var a in day.Absences)
                                c.Item().PaddingTop(2).Text(a.Text).FontSize(8).FontColor(Colors.Grey.Darken2);

                            foreach (var s in day.Shifts)
                                c.Item().PaddingTop(3).Column(sc =>
                                {
                                    sc.Item().Text(s.Time).FontSize(8).FontColor(Colors.Blue.Darken2);
                                    sc.Item().Text(s.Text);
                                });
                        });
                    }
                });

                page.Footer().AlignRight().Text(export.GeneratedLabel).FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        }).GeneratePdf();
    }
}
