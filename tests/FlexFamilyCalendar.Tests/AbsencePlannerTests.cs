using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class AbsencePlannerTests
{
    private static CalendarEntry Template() => new()
    {
        UserId = "u1", UserDisplayName = "Lena", Type = EntryType.Vacation,
        StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(16), Title = "Urlaub"
    };

    [Fact]
    public void Build_CreatesOneEntryPerDay_WithSpanAndGroup()
    {
        var from = new DateOnly(2026, 6, 1);
        var to = new DateOnly(2026, 6, 3);

        var planned = AbsencePlanner.Build(Template(), from, to, "grp-1");

        Assert.Equal(3, planned.Count);
        Assert.Equal(new[] { from, from.AddDays(1), to }, planned.Select(p => p.Date));
        Assert.All(planned, p =>
        {
            Assert.Equal("grp-1", p.Entry.AbsenceGroupId);
            Assert.Equal(from, p.Entry.AbsenceStart);
            Assert.Equal(to, p.Entry.AbsenceEnd);
            Assert.Equal(EntryType.Vacation, p.Entry.Type);
            Assert.Equal(TimeSpan.FromHours(8), p.Entry.StartTime);
        });
    }

    [Fact]
    public void Build_SingleDay_ProducesOneEntry()
    {
        var d = new DateOnly(2026, 6, 1);
        var planned = AbsencePlanner.Build(Template(), d, d, "g");
        var one = Assert.Single(planned);
        Assert.Equal(d, one.Date);
    }

    [Fact]
    public void Build_ReversedRange_IsNormalized()
    {
        var from = new DateOnly(2026, 6, 5);
        var to = new DateOnly(2026, 6, 3);

        var planned = AbsencePlanner.Build(Template(), from, to, "g");

        Assert.Equal(3, planned.Count);
        Assert.Equal(new DateOnly(2026, 6, 3), planned.First().Date);
        Assert.Equal(new DateOnly(2026, 6, 5), planned.Last().Date);
    }

    [Fact]
    public void Build_AssignsDistinctIds()
    {
        var planned = AbsencePlanner.Build(Template(), new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 4), "g");
        Assert.Equal(planned.Count, planned.Select(p => p.Entry.Id).Distinct().Count());
    }
}
