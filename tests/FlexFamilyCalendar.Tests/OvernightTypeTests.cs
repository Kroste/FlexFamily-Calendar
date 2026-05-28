using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class OvernightTypeTests
{
    private static CalendarEntry Overnight(string userId = "u1") => new()
    {
        UserId = userId,
        Type = EntryType.Overnight,
        StartTime = TimeSpan.FromHours(20),
        EndTime = TimeSpan.FromHours(6)   // über Mitternacht: Dauer = 10h
    };

    [Fact]
    public void Overnight_CountsTowardAccount_ButIsNotActiveWork_NorAbsence()
    {
        Assert.True(EntryTypeInfo.CountsTowardHours(EntryType.Overnight));
        Assert.False(EntryTypeInfo.CountsAsWork(EntryType.Overnight));
        Assert.False(EntryTypeInfo.IsAbsence(EntryType.Overnight));
    }

    [Fact]
    public void Overnight_CrossesMidnight_DurationWraps()
    {
        var e = Overnight();
        Assert.True(e.CrossesMidnight);
        Assert.True(e.ContinuesNextDay);
        Assert.Equal(10.0, e.DurationHours, 3);   // 20:00→06:00
    }

    [Fact]
    public void ActualHours_CreditsFlatRate_NotDuration()
    {
        var entries = new[] { Overnight() };
        var rates = new Dictionary<string, double> { ["u1"] = 2.0 };

        var actual = WeeklyHoursCalculator.ActualHoursByUser(entries, rates);

        Assert.Equal(2.0, actual.GetValueOrDefault("u1"), 3);   // pauschal 2h, nicht 10h
    }

    [Fact]
    public void ActualHours_WithoutRate_CreditsZero()
    {
        var actual = WeeklyHoursCalculator.ActualHoursByUser(new[] { Overnight() });
        Assert.Equal(0.0, actual.GetValueOrDefault("u1"), 3);
    }

    [Fact]
    public void ActualHours_OvernightPlusWork_SumsCreditAndWorkedDuration()
    {
        var entries = new[]
        {
            Overnight(),
            new CalendarEntry { UserId = "u1", Type = EntryType.Work,
                StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(16) }  // 8h
        };
        var rates = new Dictionary<string, double> { ["u1"] = 2.0 };

        var actual = WeeklyHoursCalculator.ActualHoursByUser(entries, rates);

        Assert.Equal(10.0, actual.GetValueOrDefault("u1"), 3);   // 2h (Übernachtung) + 8h (Arbeit)
    }

    [Fact]
    public void WorkedHours_ExcludesOvernight()
    {
        var entries = new[]
        {
            Overnight(),
            new CalendarEntry { UserId = "u1", Type = EntryType.Work,
                StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(16) }
        };

        var worked = WeeklyHoursCalculator.WorkedHoursByUser(entries);

        Assert.Equal(8.0, worked.GetValueOrDefault("u1"), 3);   // nur Arbeit zählt als geleistet
    }

    [Fact]
    public void Display_Overnight_IsFullyOpaque_LikeWork()
    {
        var (opacity, _) = EntryDisplay.Resolve(EntryType.Overnight, isOwn: false, personalView: false);
        Assert.Equal(1.0, opacity, 3);
    }
}
