using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class OvernightShiftTests
{
    private static CalendarEntry Work(int startH, int endH, string id = "w1") => new()
    {
        Id = id, UserId = "u1", UserDisplayName = "Au-Pair", Type = EntryType.Work,
        StartTime = TimeSpan.FromHours(startH), EndTime = TimeSpan.FromHours(endH)
    };

    [Theory]
    [InlineData(8, 16, 8)]    // normaler Tag
    [InlineData(20, 6, 10)]   // über Mitternacht
    [InlineData(20, 0, 4)]    // endet exakt um Mitternacht (00:00 = nächster Tag)
    [InlineData(22, 7, 9)]
    public void DurationHours_WrapsAcrossMidnight(int startH, int endH, double expected)
        => Assert.Equal(expected, Work(startH, endH).DurationHours, 3);

    [Fact]
    public void CrossesMidnight_And_ContinuesNextDay()
    {
        Assert.True(Work(20, 6).CrossesMidnight);
        Assert.True(Work(20, 6).ContinuesNextDay);
        Assert.True(Work(20, 0).CrossesMidnight);
        Assert.False(Work(20, 0).ContinuesNextDay);   // endet um Mitternacht → kein Folgetag-Anteil
        Assert.False(Work(8, 16).CrossesMidnight);
    }

    [Fact]
    public void Continuations_ProducesNextDayTail_ForOvernightWork()
    {
        var prev = new[] { Work(20, 6, "night"), Work(8, 16, "day") };

        var tails = OvernightShifts.Continuations(prev);

        var tail = Assert.Single(tails);
        Assert.Equal("night", tail.Id);            // verweist auf die Originalschicht
        Assert.True(tail.IsContinuation);
        Assert.Equal(TimeSpan.Zero, tail.StartTime);
        Assert.Equal(TimeSpan.FromHours(6), tail.EndTime);
    }

    [Fact]
    public void Continuations_Ignores_ShiftEndingAtMidnight_And_Absences()
    {
        var endsAtMidnight = Work(20, 0, "m");
        var absence = new CalendarEntry
        {
            Id = "a", Type = EntryType.SickLeave,
            StartTime = TimeSpan.FromHours(22), EndTime = TimeSpan.FromHours(6)
        };

        Assert.Empty(OvernightShifts.Continuations(new[] { endsAtMidnight, absence }));
    }
}
