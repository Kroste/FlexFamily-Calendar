using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class PlanExportTests
{
    private static string TypeLabel(EntryType t) => t.ToString();

    [Fact]
    public void ShiftLine_Work_ShowsTimeTypeAndPerson()
    {
        var e = new CalendarEntry
        {
            Type = EntryType.Work, DisplayType = EntryType.Work,
            StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(16),
            UserDisplayName = "Lena"
        };

        var line = PlanExportBuilder.ShiftLine(e, TypeLabel);

        Assert.Equal("08:00–16:00", line.Time);
        Assert.Equal("Work – Lena", line.Text);
    }

    [Fact]
    public void ShiftLine_Activity_PrefersCategoryNameOverType()
    {
        var e = new CalendarEntry
        {
            Type = EntryType.Activity, DisplayType = EntryType.Activity,
            StartTime = TimeSpan.FromHours(16), EndTime = TimeSpan.FromHours(17),
            ActivityName = "Sprachkurs", UserDisplayName = "Snea"
        };

        var line = PlanExportBuilder.ShiftLine(e, TypeLabel);

        Assert.Equal("Sprachkurs – Snea", line.Text);   // Kategorie statt Typ "Activity"
    }

    [Fact]
    public void ShiftLine_Continuation_PrefixesArrow()
    {
        var e = new CalendarEntry
        {
            Type = EntryType.Overnight, DisplayType = EntryType.Overnight,
            StartTime = TimeSpan.Zero, EndTime = TimeSpan.FromHours(6),
            IsContinuation = true, UserDisplayName = "Snea"
        };

        var line = PlanExportBuilder.ShiftLine(e, TypeLabel);

        Assert.StartsWith("↳ ", line.Time);
    }

    [Fact]
    public void AbsenceLine_ShowsPersonTypeAndSpan()
    {
        var e = new CalendarEntry
        {
            Type = EntryType.Vacation, DisplayType = EntryType.Vacation,
            UserDisplayName = "Lena",
            AbsenceStart = new DateOnly(2026, 6, 1), AbsenceEnd = new DateOnly(2026, 6, 14)
        };

        var line = PlanExportBuilder.AbsenceLine(e, TypeLabel);

        Assert.Equal("", line.Time);
        Assert.Equal("Lena – Vacation (01.06.–14.06.)", line.Text);
    }

    [Fact]
    public void AbsenceLine_SingleDay_NoSpan()
    {
        var e = new CalendarEntry
        {
            Type = EntryType.SickLeave, DisplayType = EntryType.SickLeave,
            UserDisplayName = "Max",
            AbsenceStart = new DateOnly(2026, 6, 1), AbsenceEnd = new DateOnly(2026, 6, 1)
        };

        Assert.Equal("Max – SickLeave", PlanExportBuilder.AbsenceLine(e, TypeLabel).Text);
    }

    [Fact]
    public void PdfExport_ProducesValidPdfBytes()
    {
        var export = SampleWeek();

        var bytes = PdfExportService.Render(export);

        Assert.True(bytes.Length > 1000);
        // PDF-Dateikennung "%PDF"
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }

    private static WeekExport SampleWeek()
    {
        var days = new List<PlanExportDay>();
        for (int i = 0; i < 7; i++)
        {
            days.Add(new PlanExportDay(
                $"Tag {i + 1}", $"0{i + 1}.06.",
                i == 0 ? "Pfingstmontag" : "",
                i == 2 ? "Elternabend 19:00" : "",
                i == 1 ? new[] { new PlanExportLine("", "Lena – Urlaub (01.06.–14.06.)") } : Array.Empty<PlanExportLine>(),
                new[]
                {
                    new PlanExportLine("08:00–16:00", "Arbeit – Lena"),
                    new PlanExportLine("16:00–17:00", "Sprachkurs – Snea"),
                    new PlanExportLine("20:00–06:00", "Übernachtung – Snea"),
                }));
        }
        return new WeekExport("Wochenplan", "KW 23 / 2026", "Erstellt am 28.05.2026 12:00", days);
    }
}
