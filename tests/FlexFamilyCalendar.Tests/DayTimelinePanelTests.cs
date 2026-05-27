using FlexFamilyCalendar.Controls;
using FlexFamilyCalendar.Models;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class DayTimelinePanelTests
{
    private static CalendarEntry Entry(int startH, int endH) => new()
    {
        StartTime = TimeSpan.FromHours(startH),
        EndTime = TimeSpan.FromHours(endH)
    };

    [Fact]
    public void Empty_ReturnsEmpty()
        => Assert.Empty(DayTimelinePanel.AssignLanes([]));

    [Fact]
    public void Single_GetsLane0Of1()
    {
        var r = DayTimelinePanel.AssignLanes([Entry(8, 9)]);
        Assert.Equal((0, 1), r[0]);
    }

    [Fact]
    public void NonOverlapping_ShareNoLanes()
    {
        var r = DayTimelinePanel.AssignLanes([Entry(8, 9), Entry(10, 11)]);
        Assert.Equal((0, 1), r[0]);
        Assert.Equal((0, 1), r[1]);
    }

    [Fact]
    public void Overlapping_SplitIntoTwoLanes()
    {
        // 08:00–09:00 und 08:30–10:00 überlappen
        var a = Entry(8, 9);
        var b = new CalendarEntry { StartTime = TimeSpan.FromHours(8.5), EndTime = TimeSpan.FromHours(10) };
        var r = DayTimelinePanel.AssignLanes([a, b]);
        Assert.Equal(2, r[0].LaneCount);
        Assert.Equal(2, r[1].LaneCount);
        Assert.NotEqual(r[0].LaneIndex, r[1].LaneIndex);
    }

    [Fact]
    public void Adjacent_EndEqualsStart_ReusesLane()
    {
        // 08–09 und 09–10 berühren sich nur → kein Überlapp, je eigene Spur
        var r = DayTimelinePanel.AssignLanes([Entry(8, 9), Entry(9, 10)]);
        Assert.Equal((0, 1), r[0]);
        Assert.Equal((0, 1), r[1]);
    }

    [Fact]
    public void ThreeWayOverlap_ProducesThreeLanes()
    {
        var r = DayTimelinePanel.AssignLanes([Entry(8, 12), Entry(9, 11), Entry(10, 13)]);
        Assert.All(r, x => Assert.Equal(3, x.LaneCount));
        Assert.Equal(3, r.Select(x => x.LaneIndex).Distinct().Count());
    }
}
