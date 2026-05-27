using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class WeeklyHoursTests
{
    private static CalendarEntry Entry(string userId, EntryType type, int startH, int endH) => new()
    {
        UserId = userId,
        Type = type,
        StartTime = TimeSpan.FromHours(startH),
        EndTime = TimeSpan.FromHours(endH)
    };

    [Theory]
    [InlineData(EntryType.Work, true)]
    [InlineData(EntryType.Vacation, false)]
    [InlineData(EntryType.SickLeave, false)]
    [InlineData(EntryType.Activity, false)]
    [InlineData(EntryType.Absence, false)]
    public void CountsAsWork_OnlyWork(EntryType type, bool expected)
        => Assert.Equal(expected, EntryTypeInfo.CountsAsWork(type));

    [Theory]
    [InlineData(EntryType.Work, true)]
    [InlineData(EntryType.SickLeave, true)]
    [InlineData(EntryType.Vacation, true)]
    [InlineData(EntryType.Activity, false)]
    [InlineData(EntryType.Absence, false)]
    public void CountsTowardHours_WorkSickVacation(EntryType type, bool expected)
        => Assert.Equal(expected, EntryTypeInfo.CountsTowardHours(type));

    [Fact]
    public void SumsWork_PerUser()
    {
        var entries = new[]
        {
            Entry("u1", EntryType.Work, 8, 12),  // 4h
            Entry("u1", EntryType.Work, 14, 16), // 2h
            Entry("u2", EntryType.Work, 9, 17),  // 8h
        };

        var result = WeeklyHoursCalculator.ActualHoursByUser(entries);

        Assert.Equal(6, result["u1"]);
        Assert.Equal(8, result["u2"]);
    }

    [Fact]
    public void CreditsWorkSickVacation_IgnoresActivityAndAbsence()
    {
        var entries = new[]
        {
            Entry("u1", EntryType.Work, 8, 12),       // 4h zählt
            Entry("u1", EntryType.SickLeave, 8, 12),  // 4h angerechnet
            Entry("u1", EntryType.Vacation, 8, 12),   // 4h angerechnet
            Entry("u1", EntryType.Activity, 13, 15),  // ignoriert
            Entry("u1", EntryType.Absence, 0, 1),     // ignoriert
        };

        Assert.Equal(12, WeeklyHoursCalculator.ActualHoursByUser(entries)["u1"]);
    }

    [Fact]
    public void EmptyInput_EmptyResult()
        => Assert.Empty(WeeklyHoursCalculator.ActualHoursByUser([]));
}
