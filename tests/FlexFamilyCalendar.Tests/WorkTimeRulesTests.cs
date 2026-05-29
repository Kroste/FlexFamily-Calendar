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
    public void Summarize_OvernightShift_LastWorkEnd_WrapsPastMidnight()
    {
        // Schicht 20:00 → 06:00 läuft bis 06:00 am Folgetag → relativ zu D1 = 30:00.
        var s = WorkTimeRules.Summarize(D1, new[] { Entry(EntryType.Work, 20, 6) });
        Assert.Equal(TimeSpan.FromHours(30), s.LastWorkEnd);
        Assert.Equal(TimeSpan.FromHours(20), s.FirstWorkStart);
    }

    [Fact]
    public void RestHoursBetween_OvernightShift_FollowedByLateShift()
    {
        // prev: Nacht 20:00→06:00 (endet faktisch 06:00 am Folgetag).
        // next: am Folgetag 22:00→04:00.
        // Ruhe = 22 − 6 = 16h.
        var prev = WorkTimeRules.Summarize(D1, new[] { Entry(EntryType.Work, 20, 6) });
        var next = WorkTimeRules.Summarize(D2, new[] { Entry(EntryType.Work, 22, 4) });
        Assert.Equal(16, WorkTimeRules.RestHoursBetween(prev, next));
    }

    [Fact]
    public void RestHoursBetween_TwoConsecutiveOvernightShifts()
    {
        // Doppelte Nacht: Mo 20:00→Di 06:00, dann Di 20:00→Mi 06:00.
        // Ruhe von Mo-Schicht-Ende (Di 06:00) bis Di-Schicht-Start (Di 20:00) = 14h.
        var prev = WorkTimeRules.Summarize(D1, new[] { Entry(EntryType.Work, 20, 6) });
        var next = WorkTimeRules.Summarize(D2, new[] { Entry(EntryType.Work, 20, 6) });
        Assert.Equal(14, WorkTimeRules.RestHoursBetween(prev, next));
    }

    [Fact]
    public void RestHoursBetween_OvernightShift_FollowedByEarlyMorningShift_FlagsOverlap()
    {
        // prev: Nacht 20:00→06:00; next: 04:00→12:00 (= startet VOR Vortags-Schicht-Ende).
        // Das ist eine Überlappung → kein sinnvoller Ruhewert.
        var prev = WorkTimeRules.Summarize(D1, new[] { Entry(EntryType.Work, 20, 6) });
        var next = WorkTimeRules.Summarize(D2, new[] { Entry(EntryType.Work, 4, 12) });
        Assert.Null(WorkTimeRules.RestHoursBetween(prev, next));
    }

    [Fact]
    public void ShortRests_OvernightShiftThenEarlyShift_IsFlagged()
    {
        // Nacht-Schicht endet faktisch 06:00 am Folgetag, dann 8:00-Schicht → nur 2h Pause.
        var days = new[]
        {
            WorkTimeRules.Summarize(D1, new[] { Entry(EntryType.Work, 20, 6) }),
            WorkTimeRules.Summarize(D2, new[] { Entry(EntryType.Work, 8, 16) }),
        };
        var shorts = WorkTimeRules.ShortRests(days, 11).ToList();
        Assert.Single(shorts);
        Assert.Equal(2, shorts[0].RestHours);
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

    [Fact]
    public void WorkOverlaps_FlagsOverlappingShifts()
    {
        var overlaps = WorkTimeRules.WorkOverlaps(new[]
        {
            Entry(EntryType.Work, 8, 12),
            Entry(EntryType.Work, 11, 14),  // überschneidet 11–12
        });
        Assert.Single(overlaps);
    }

    [Fact]
    public void WorkOverlaps_AdjacentShifts_NoOverlap()
    {
        // 12:00-Ende und 12:00-Start berühren sich nur — keine Überschneidung
        var overlaps = WorkTimeRules.WorkOverlaps(new[]
        {
            Entry(EntryType.Work, 8, 12),
            Entry(EntryType.Work, 12, 16),
        });
        Assert.Empty(overlaps);
    }

    [Fact]
    public void WorkOverlaps_IgnoresNonWork()
    {
        // Krank überspannt den ganzen Tag, soll Arbeit nicht als Kollision markieren
        var overlaps = WorkTimeRules.WorkOverlaps(new[]
        {
            Entry(EntryType.SickLeave, 0, 24),
            Entry(EntryType.Work, 8, 12),
        });
        Assert.Empty(overlaps);
    }
}
