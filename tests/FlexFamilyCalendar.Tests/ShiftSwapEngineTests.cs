using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class ShiftSwapEngineTests
{
    private static CalendarEntry Work(string id, string userId, int startH, int endH) => new()
    {
        Id = id,
        UserId = userId,
        UserDisplayName = userId,
        Type = EntryType.Work,
        StartTime = TimeSpan.FromHours(startH),
        EndTime = TimeSpan.FromHours(endH)
    };

    private static CalendarDay Day(string date, params CalendarEntry[] entries) => new()
    {
        DateString = date,
        Entries = entries.ToList()
    };

    private const string DateA = "2026-06-01";
    private const string DateB = "2026-06-02";

    private static ShiftSwapRequest GiveAway(string fromEntryId) => new()
    {
        Mode = SwapMode.GiveAway,
        FromUserId = "A", FromUserName = "A", FromDate = DateA, FromEntryId = fromEntryId,
        ToUserId = "B", ToUserName = "B"
    };

    private static ShiftSwapRequest Exchange(string fromEntryId, string toEntryId, string toDate) => new()
    {
        Mode = SwapMode.Exchange,
        FromUserId = "A", FromUserName = "A", FromDate = DateA, FromEntryId = fromEntryId,
        ToUserId = "B", ToUserName = "B", ToDate = toDate, ToEntryId = toEntryId
    };

    [Fact]
    public void GiveAway_TransfersShiftToColleague()
    {
        var shift = Work("s1", "A", 8, 12);
        var day = Day(DateA, shift);
        var req = GiveAway("s1");

        Assert.Null(ShiftSwapEngine.Validate(req, day, null));
        ShiftSwapEngine.Apply(req, day, null);

        Assert.Equal("B", shift.UserId);
        Assert.Equal("B", shift.UserDisplayName);
    }

    [Fact]
    public void Exchange_SwapsOwnersOfBothShifts()
    {
        var aShift = Work("s1", "A", 8, 12);
        var bShift = Work("s2", "B", 14, 18);
        var dayA = Day(DateA, aShift);
        var dayB = Day(DateB, bShift);
        var req = Exchange("s1", "s2", DateB);

        Assert.Null(ShiftSwapEngine.Validate(req, dayA, dayB));
        ShiftSwapEngine.Apply(req, dayA, dayB);

        Assert.Equal("B", aShift.UserId);
        Assert.Equal("A", bShift.UserId);
        Assert.Equal("A", bShift.UserDisplayName);
    }

    [Fact]
    public void Exchange_SameDay_Works()
    {
        var aShift = Work("s1", "A", 8, 12);
        var bShift = Work("s2", "B", 14, 18);
        var day = Day(DateA, aShift, bShift);
        var req = Exchange("s1", "s2", DateA);   // gleiches Datum → dasselbe Day-Objekt

        Assert.Null(ShiftSwapEngine.Validate(req, day, day));
        ShiftSwapEngine.Apply(req, day, day);

        Assert.Equal("B", aShift.UserId);
        Assert.Equal("A", bShift.UserId);
    }

    [Fact]
    public void GiveAway_OverlapAtColleague_Rejected()
    {
        var aShift = Work("s1", "A", 8, 12);
        var bBusy = Work("s2", "B", 11, 15);   // B arbeitet bereits 11–15 → würde überschneiden
        var day = Day(DateA, aShift, bBusy);

        Assert.Equal("Swap_ErrorOverlap", ShiftSwapEngine.Validate(GiveAway("s1"), day, null));
        Assert.Equal("A", aShift.UserId);      // keine Mutation
    }

    [Fact]
    public void GiveAway_AdjacentAtColleague_Ok()
    {
        var aShift = Work("s1", "A", 8, 12);
        var bAfter = Work("s2", "B", 12, 16);  // grenzt nur an → keine Überschneidung
        var day = Day(DateA, aShift, bAfter);

        Assert.Null(ShiftSwapEngine.Validate(GiveAway("s1"), day, null));
    }

    [Fact]
    public void FinalizedDay_Rejected()
    {
        var shift = Work("s1", "A", 8, 12);
        var day = Day(DateA, shift);
        day.IsFinalized = true;

        Assert.Equal("Swap_ErrorFinalized", ShiftSwapEngine.Validate(GiveAway("s1"), day, null));
    }

    [Fact]
    public void MissingShift_Stale()
    {
        var day = Day(DateA, Work("other", "A", 8, 12));
        Assert.Equal("Swap_ErrorStale", ShiftSwapEngine.Validate(GiveAway("s1"), day, null));
    }

    [Fact]
    public void Exchange_MissingCounterShift_Stale()
    {
        var aShift = Work("s1", "A", 8, 12);
        var dayA = Day(DateA, aShift);
        var dayB = Day(DateB);   // keine Gegen-Schicht
        Assert.Equal("Swap_ErrorStale", ShiftSwapEngine.Validate(Exchange("s1", "s2", DateB), dayA, dayB));
    }
}
