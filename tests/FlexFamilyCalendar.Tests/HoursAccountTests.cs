using FlexFamilyCalendar.Models;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class HoursAccountTests
{
    [Fact]
    public void RunningBalance_AccumulatesFromOpening()
    {
        var result = HoursAccount.RunningBalance(10, new double[] { 5, -3, 2 });
        Assert.Equal(new double[] { 15, 12, 14 }, result);
    }

    [Fact]
    public void RunningBalance_Empty_ReturnsEmpty()
        => Assert.Empty(HoursAccount.RunningBalance(10, []));

    [Fact]
    public void RunningBalance_ZeroOpening_IsCumulativeSum()
    {
        var result = HoursAccount.RunningBalance(0, new double[] { -4, -1, 6 });
        Assert.Equal(new double[] { -4, -5, 1 }, result);
    }
}
