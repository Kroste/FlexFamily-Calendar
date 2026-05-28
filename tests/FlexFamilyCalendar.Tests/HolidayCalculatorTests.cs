using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class HolidayCalculatorTests
{
    [Theory]
    [InlineData(2024, 3, 31)]
    [InlineData(2025, 4, 20)]
    [InlineData(2026, 4, 5)]
    public void Easter_KnownDates(int year, int month, int day)
        => Assert.Equal(new DateOnly(year, month, day), HolidayCalculator.Easter(year));

    [Theory]
    [InlineData(GermanState.BY)]
    [InlineData(GermanState.BE)]
    [InlineData(GermanState.SN)]
    public void NationwideHolidays_PresentEverywhere(GermanState state)
    {
        var hs = HolidayCalculator.ForYear(2026, state);
        Assert.Contains(hs, h => h.NameKey == "Holiday_Christmas1" && h.Date == new DateOnly(2026, 12, 25));
        Assert.Contains(hs, h => h.NameKey == "Holiday_GermanUnity" && h.Date == new DateOnly(2026, 10, 3));
        Assert.Contains(hs, h => h.NameKey == "Holiday_EasterMonday");
    }

    [Fact]
    public void CorpusChristi_InBavaria_NotInBerlin()
    {
        Assert.Contains(HolidayCalculator.ForYear(2026, GermanState.BY), h => h.NameKey == "Holiday_CorpusChristi");
        Assert.DoesNotContain(HolidayCalculator.ForYear(2026, GermanState.BE), h => h.NameKey == "Holiday_CorpusChristi");
    }

    [Fact]
    public void Reformation_InBrandenburg_NotInBavaria()
    {
        Assert.Contains(HolidayCalculator.ForYear(2026, GermanState.BB), h => h.NameKey == "Holiday_Reformation");
        Assert.DoesNotContain(HolidayCalculator.ForYear(2026, GermanState.BY), h => h.NameKey == "Holiday_Reformation");
    }

    [Fact]
    public void WomensDay_InBerlin_OnMarch8()
        => Assert.Contains(HolidayCalculator.ForYear(2026, GermanState.BE),
            h => h.NameKey == "Holiday_WomensDay" && h.Date == new DateOnly(2026, 3, 8));

    [Fact]
    public void RepentanceDay_InSaxony_IsWednesdayBeforeNov23()
    {
        var bbt = HolidayCalculator.ForYear(2026, GermanState.SN).Single(h => h.NameKey == "Holiday_RepentanceDay");
        Assert.Equal(DayOfWeek.Wednesday, bbt.Date.DayOfWeek);
        Assert.True(bbt.Date.Day is >= 16 and <= 22 && bbt.Date.Month == 11);
    }

    [Fact]
    public void ForRange_OnlyReturnsHolidaysInRange()
    {
        var range = HolidayCalculator.ForRange(new DateOnly(2026, 12, 24), new DateOnly(2026, 12, 26), GermanState.BY);
        Assert.Equal(2, range.Count);   // 25. + 26.12., nicht der 24.
        Assert.All(range, h => Assert.True(h.Date >= new DateOnly(2026, 12, 24) && h.Date <= new DateOnly(2026, 12, 26)));
    }

    [Fact]
    public void ForRange_SpanningYearBoundary_IncludesNewYear()
    {
        var range = HolidayCalculator.ForRange(new DateOnly(2026, 12, 29), new DateOnly(2027, 1, 2), GermanState.BY);
        Assert.Contains(range, h => h.NameKey == "Holiday_NewYear" && h.Date == new DateOnly(2027, 1, 1));
    }
}
