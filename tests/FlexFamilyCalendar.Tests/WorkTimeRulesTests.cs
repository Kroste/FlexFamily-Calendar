using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class WorkTimeRulesTests
{
    private static CalendarEntry Entry(EntryType type, int startH, int endH) => new()
    {
        UserId = "u1",
        Type = type,
        StartTime = TimeSpan.FromHours(startH),
        EndTime = TimeSpan.FromHours(endH)
    };

    private static readonly DateOnly D1 = new(2026, 6, 1);
    private static readonly DateOnly D2 = new(2026, 6, 2);

    [Fact]
    public void Summarize_WorkOnly_SumsAndBounds()
    {
        var s = WorkTimeRules.Summarize(D1, new[]
        {
            Entry(EntryType.Work, 8, 12),       // 4h
            Entry(EntryType.Work, 13, 18),      // 5h
            Entry(EntryType.SickLeave, 6, 7),   // keine Arbeit
            Entry(EntryType.Activity, 19, 20),  // keine Arbeit
        });

        Assert.Equal(9, s.WorkedHours);
        Assert.Equal(TimeSpan.FromHours(8), s.FirstWorkStart);
        Assert.Equal(TimeSpan.FromHours(18), s.LastWorkEnd);
    }

    [Fact]
    public void Summarize_NoWork_NullBounds()
    {
        var s = WorkTimeRules.Summarize(D1, new[] { Entry(EntryType.Vacation, 0, 24) });
        Assert.Equal(0, s.WorkedHours);
        Assert.Null(s.FirstWorkStart);
        Assert.Null(s.LastWorkEnd);
    }

    [Fact]
    public void OverDailyLimit_FlagsDaysAboveLimit()
    {
        var days = new[]
        {
            WorkTimeRules.Summarize(D1, new[] { Entry(EntryType.Work, 6, 18) }),  // 12h
            WorkTimeRules.Summarize(D2, new[] { Entry(EntryType.Work, 9, 17) }),  // 8h
        };

        var over = WorkTimeRules.OverDailyLimit(days, 10).ToList();
        Assert.Single(over);
        Assert.Equal(D1, over[0].Date);
    }

    [Fact]
    public void OverDailyLimit_NoLimit_Empty()
    {
        var days = new[] { WorkTimeRules.Summarize(D1, new[] { Entry(EntryType.Work, 0, 20) }) };
        Assert.Empty(WorkTimeRules.OverDailyLimit(days, 0));
    }

    [Fact]
    public void RestHoursBetween_SpansMidnight()
    {
        var prev = WorkTimeRules.Summarize(D1, new[] { Entry(EntryType.Work, 8, 22) }); // endet 22:00
        var next = WorkTimeRules.Summarize(D2, new[] { Entry(EntryType.Work, 6, 14) }); // beginnt 06:00
        // Ruhe = (24-22) + 6 = 8h
        Assert.Equal(8, WorkTimeRules.RestHoursBetween(prev, next));
    }

    [Fact]
    public void RestHoursBetween_NoWork_Null()
    {
        var prev = WorkTimeRules.Summarize(D1, Array.Empty<CalendarEntry>());
        var next = WorkTimeRules.Summarize(D2, new[] { Entry(EntryType.Work, 6, 14) });
        Assert.Null(WorkTimeRules.RestHoursBetween(prev, next));
    }

    [Fact]
    public void ShortRests_FlagsConsecutivePairBelowMinimum()
    {
        var days = new[]
        {
            WorkTimeRules.Summarize(D1, new[] { Entry(EntryType.Work, 8, 22) }), // endet 22
            WorkTimeRules.Summarize(D2, new[] { Entry(EntryType.Work, 6, 14) }), // beginnt 6 → 8h Ruhe
        };

        var shorts = WorkTimeRules.ShortRests(days, 11).ToList();
        Assert.Single(shorts);
        Assert.Equal(8, shorts[0].RestHours);
    }

    [Fact]
    public void ShortRests_AdequateRest_Empty()
    {
        var days = new[]
        {
            WorkTimeRules.Summarize(D1, new[] { Entry(EntryType.Work, 8, 16) }), // endet 16
            WorkTimeRules.Summarize(D2, new[] { Entry(EntryType.Work, 8, 16) }), // beginnt 8 → 16h Ruhe
        };
        Assert.Empty(WorkTimeRules.ShortRests(days, 11));
    }

    [Fact]
    public void ShortRests_NoMinimum_Empty()
    {
        var days = new[]
        {
            WorkTimeRules.Summarize(D1, new[] { Entry(EntryType.Work, 8, 23) }),
            WorkTimeRules.Summarize(D2, new[] { Entry(EntryType.Work, 1, 9) }),
        };
        Assert.Empty(WorkTimeRules.ShortRests(days, 0));
    }
}
