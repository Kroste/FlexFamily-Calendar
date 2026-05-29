using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services.Api;

namespace FlexFamilyCalendar.Tests;

public class RecurringActivityMappingTests
{
    [Fact]
    public void ToDesktop_maps_weekdays_times_and_fields()
    {
        var dto = new ServerRecurringActivityDto(
            "r1", "u1", "Mia", "Fußball", "cat1",
            new TimeOnly(16, 0), new TimeOnly(17, 30),
            new List<int> { 4, 2 }, SkipOnHolidays: true);   // 4=Thursday, 2=Tuesday

        var a = RecurringActivityMapping.ToDesktop(dto);

        Assert.Equal("r1", a.Id);
        Assert.Equal("u1", a.UserId);
        Assert.Equal("Mia", a.UserDisplayName);
        Assert.Equal("Fußball", a.Title);
        Assert.Equal("cat1", a.ActivityTypeId);
        Assert.Equal(new TimeSpan(16, 0, 0), a.StartTime);
        Assert.Equal(new TimeSpan(17, 30, 0), a.EndTime);
        Assert.Equal(new[] { DayOfWeek.Thursday, DayOfWeek.Tuesday }, a.Weekdays);
        Assert.True(a.SkipOnHolidays);
    }

    [Fact]
    public void Empty_activity_type_id_becomes_null()
    {
        var dto = new ServerRecurringActivityDto("r1", "u1", "X", "T", "",
            new TimeOnly(8, 0), new TimeOnly(9, 0), new List<int> { 1 }, false);
        Assert.Null(RecurringActivityMapping.ToDesktop(dto).ActivityTypeId);
    }

    [Fact]
    public void ToServer_serializes_weekdays_as_ints()
    {
        var a = new RecurringActivity
        {
            Id = "r2", UserId = "u1", UserDisplayName = "Tom", Title = "Klavier",
            ActivityTypeId = null,
            StartTime = new TimeSpan(18, 0, 0), EndTime = new TimeSpan(19, 0, 0),
            Weekdays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Wednesday },
            SkipOnHolidays = false
        };

        var dto = RecurringActivityMapping.ToServer(a);

        Assert.Equal(new[] { 1, 3 }, dto.Weekdays);
        Assert.Null(dto.ActivityTypeId);
        Assert.Equal(new TimeOnly(18, 0), dto.StartTime);
    }

    [Fact]
    public void Round_trip_preserves_weekdays_and_times()
    {
        var a = new RecurringActivity
        {
            Id = "r3", UserId = "u9", Title = "Reiten",
            StartTime = new TimeSpan(20, 0, 0), EndTime = new TimeSpan(6, 0, 0),  // über Mitternacht
            Weekdays = new List<DayOfWeek> { DayOfWeek.Sunday, DayOfWeek.Saturday },
            SkipOnHolidays = true
        };

        var back = RecurringActivityMapping.ToDesktop(RecurringActivityMapping.ToServer(a));

        Assert.Equal(a.Weekdays, back.Weekdays);
        Assert.Equal(a.StartTime, back.StartTime);
        Assert.Equal(a.EndTime, back.EndTime);
        Assert.True(back.SkipOnHolidays);
    }
}
