using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class PlanExportTests
{
    private static string TypeLabel(EntryType t) => t.ToString();

    [Fact]
    public void CellEntry_Work_HasTimeAndTypeLabel()
    {
        var e = new CalendarEntry
        {
            Type = EntryType.Work, DisplayType = EntryType.Work,
            StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(16),
            OwnerColor = "#2E86C1"
        };

        var c = PlanExportBuilder.CellEntry(e, TypeLabel);

        Assert.Equal("08:00–16:00", c.Time);
        Assert.Equal("Work", c.Label);
        Assert.Equal("#2E86C1", c.ColorHex);
    }

    [Fact]
    public void CellEntry_Activity_UsesCategoryAndTitle()
    {
        var e = new CalendarEntry
        {
            Type = EntryType.Activity, DisplayType = EntryType.Activity,
            StartTime = TimeSpan.FromHours(16), EndTime = TimeSpan.FromHours(17),
            ActivityName = "Sprachkurs", DisplayTitle = "Online"
        };

        Assert.Equal("Sprachkurs · Online", PlanExportBuilder.CellEntry(e, TypeLabel).Label);
    }

    [Fact]
    public void CellEntry_MultiDayAbsence_ShowsSpanNotTime()
    {
        var e = new CalendarEntry
        {
            Type = EntryType.Vacation, DisplayType = EntryType.Vacation,
            StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(16),
            AbsenceStart = new DateOnly(2026, 6, 1), AbsenceEnd = new DateOnly(2026, 6, 14)
        };

        var c = PlanExportBuilder.CellEntry(e, TypeLabel);
        Assert.Equal("01.06.–14.06.", c.Time);
        Assert.Equal("Vacation", c.Label);
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
        PlanCellEntry E(string color, string time, string label) => new(color, time, label);

        var days = new List<PlanDayHeader>();
        var notes = new List<string>();
        for (int i = 0; i < 7; i++)
        {
            days.Add(new PlanDayHeader($"Tag {i + 1}", $"0{i + 1}.06.", i == 0 ? "Pfingstmontag" : ""));
            notes.Add(i == 2 ? "Elternabend 19:00" : "");
        }

        IReadOnlyList<PlanCellEntry>[] CellsFor(string color) => new IReadOnlyList<PlanCellEntry>[]
        {
            new[] { E(color, "08:00–16:00", "Arbeit"), E(color, "20:00–06:00", "Übernachtung") },
            new[] { E(color, "01.06.–14.06.", "Urlaub") },
            new[] { E(color, "16:00–17:00", "Sprachkurs · Online 😀") },
            Array.Empty<PlanCellEntry>(),
            new[] { E(color, "08:00–09:00", "Hinfahrt Kita") },
            Array.Empty<PlanCellEntry>(),
            Array.Empty<PlanCellEntry>(),
        };

        var rows = new List<PlanPersonRow>
        {
            new("Friederike Oste", "#8E44AD", "Eltern", CellsFor("#8E44AD")),
            new("Snea", "#C0392B", "Au-Pair", CellsFor("#C0392B")),
            new("Levi S", "#E67E22", "Kind", CellsFor("#E67E22")),
        };

        return new WeekExport("Wochenplan", "KW 23 / 2026", "Erstellt am 28.05.2026 12:00", days, rows, notes);
    }
}
