using FlexFamilyCalendar.Services;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class MonthlyHoursTests
{
    [Theory]
    [InlineData(40, 28, 160)]   // 4 Wochen
    [InlineData(40, 7, 40)]     // genau 1 Woche
    [InlineData(0, 31, 0)]      // kein Soll
    [InlineData(20, 14, 40)]    // 2 Wochen Halbzeit
    public void MonthlyTarget_ProratesByDays(double quota, int days, double expected)
        => Assert.Equal(expected, WeeklyHoursCalculator.MonthlyTarget(quota, days), 3);

    [Fact]
    public void MonthlyTarget_31Days_IsAboutFourThirdsWeeks()
        => Assert.Equal(40 * 31 / 7.0, WeeklyHoursCalculator.MonthlyTarget(40, 31), 6);
}
