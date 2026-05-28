using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class PlanExportTests
{
    private static string TypeLabel(EntryType t) => t.ToString();

    [Fact]
    public void BlockOf_Work_HasTypeAndPerson_AndTimePosition()
    {
        var e = new CalendarEntry
        {
            Type = EntryType.Work, DisplayType = EntryType.Work,
            StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(16),
            UserDisplayName = "Lena", OwnerColor = "#2E86C1"
        };

        var b = PlanExportBuilder.BlockOf(e, 0, 1, TypeLabel);

        Assert.Equal(8, b.StartHour, 3);
        Assert.Equal(16, b.EndHour, 3);
        Assert.Equal("08:00–16:00", b.TimeLabel);
        Assert.Equal(new[] { "Work", "Lena" }, b.Lines);
        Assert.Equal("#2E86C1", b.ColorHex);
    }

    [Fact]
    public void BlockOf_Activity_UsesCategoryName_NotType()
    {
        var e = new CalendarEntry
        {
            Type = EntryType.Activity, DisplayType = EntryType.Activity,
            StartTime = TimeSpan.FromHours(16), EndTime = TimeSpan.FromHours(17),
            ActivityName = "Sprachkurs", UserDisplayName = "Snea"
        };

        Assert.Equal(new[] { "Sprachkurs", "Snea" }, PlanExportBuilder.BlockOf(e, 0, 1, TypeLabel).Lines);
    }

    [Fact]
    public void BlockOf_WithTitle_IncludesTitleLine()
    {
        var e = new CalendarEntry
        {
            Type = EntryType.Work, DisplayType = EntryType.Work,
            StartTime = TimeSpan.FromHours(10), EndTime = TimeSpan.FromHours(11),
            DisplayTitle = "Impfen", UserDisplayName = "Lena"
        };

        Assert.Equal(new[] { "Work", "Impfen", "Lena" }, PlanExportBuilder.BlockOf(e, 0, 1, TypeLabel).Lines);
    }

    [Fact]
    public void BlockOf_Overnight_EndsAtMidnightBoundary()
    {
        var e = new CalendarEntry
        {
            Type = EntryType.Overnight, DisplayType = EntryType.Overnight,
            StartTime = TimeSpan.FromHours(20), EndTime = TimeSpan.FromHours(6)
        };

        var b = PlanExportBuilder.BlockOf(e, 0, 1, TypeLabel);
        Assert.Equal(20, b.StartHour, 3);
        Assert.Equal(24, b.EndHour, 3);   // über Mitternacht → bis Tagesende
    }

    [Fact]
    public void BlockOf_Continuation_PrefixesTimeWithMarker()
    {
        var e = new CalendarEntry
        {
            Type = EntryType.Overnight, DisplayType = EntryType.Overnight,
            StartTime = TimeSpan.Zero, EndTime = TimeSpan.FromHours(6),
            IsContinuation = true
        };

        Assert.StartsWith("» ", PlanExportBuilder.BlockOf(e, 0, 1, TypeLabel).TimeLabel);
    }

    [Fact]
    public void ChipOf_ShowsPersonTypeAndSpan()
    {
        var e = new CalendarEntry
        {
            Type = EntryType.Vacation, DisplayType = EntryType.Vacation,
            UserDisplayName = "Lena", OwnerColor = "#8E44AD",
            AbsenceStart = new DateOnly(2026, 6, 1), AbsenceEnd = new DateOnly(2026, 6, 14)
        };

        var chip = PlanExportBuilder.ChipOf(e, TypeLabel);
        Assert.Equal("#8E44AD", chip.ColorHex);
        Assert.Equal("Lena – Vacation (01.06.–14.06.)", chip.Text);
    }

    [Fact]
    public void PdfExport_ProducesValidPdfBytes()
    {
        var bytes = PdfExportService.Render(SampleWeek());

        Assert.True(bytes.Length > 500);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Contains("%%EOF", System.Text.Encoding.Latin1.GetString(bytes));
    }

    private static WeekExport SampleWeek()
    {
        PlanBlock B(double s, double e, string color, double op, string time, string[] lines, int li = 0, int lc = 1)
            => new(s, e, color, op, time, lines, li, lc);

        var days = new List<PlanExportDay>();
        for (int i = 0; i < 7; i++)
        {
            days.Add(new PlanExportDay(
                $"Tag {i + 1}", $"0{i + 1}.06.",
                i == 0 ? "Pfingstmontag" : "",
                i == 2 ? "Elternabend 19:00" : "",
                i == 1 ? new[] { new PlanChip("#8E44AD", "Lena – Urlaub (01.06.–14.06.)") } : Array.Empty<PlanChip>(),
                new[]
                {
                    B(0, 6, "#5B4B8A", 0.8, "» 00:00–06:00", new[] { "Übernachtung", "Snea" }),
                    B(8, 9, "#7F8C8D", 1.0, "08:00–09:00", new[] { "Hinfahrt Kita", "Nathalie S" }, 0, 2),
                    B(9, 15, "#E67E22", 0.55, "09:00–15:00", new[] { "Remise", "Levi S" }, 1, 2),
                    B(15, 20, "#C0392B", 1.0, "15:00–20:00", new[] { "Sprachkurs Online", "Snea 😀" }),
                    B(20, 24, "#5B4B8A", 1.0, "20:00–06:00", new[] { "Übernachtung", "Snea" }),
                }));
        }
        return new WeekExport("Wochenplan", "KW 23 / 2026", "Erstellt am 28.05.2026 12:00", days);
    }
}
