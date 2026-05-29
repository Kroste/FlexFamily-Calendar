using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services.Api;

namespace FlexFamilyCalendar.Tests;

public class EntryMappingTests
{
    [Theory]
    [InlineData("Work", EntryType.Work)]
    [InlineData("Vacation", EntryType.Vacation)]
    [InlineData("SickLeave", EntryType.SickLeave)]
    [InlineData("Activity", EntryType.Activity)]
    [InlineData("Absence", EntryType.Absence)]
    [InlineData("Overnight", EntryType.Overnight)]
    public void Type_round_trips(string serverType, EntryType expected)
    {
        Assert.Equal(expected, EntryMapping.ParseType(serverType));
        Assert.Equal(serverType, EntryMapping.TypeToServer(expected));
    }

    [Fact]
    public void Unknown_server_type_falls_back_to_work()
        => Assert.Equal(EntryType.Work, EntryMapping.ParseType("Nonsense"));

    [Fact]
    public void ToDesktop_timed_shift_sets_times()
    {
        var dto = new ServerEntryDto("id1", "u1", "Work",
            new DateOnly(2026, 6, 1), null,
            new TimeOnly(8, 0), new TimeOnly(16, 0), false,
            null, null, "Approved", false);

        var e = EntryMapping.ToDesktop(dto, new DateOnly(2026, 6, 1));

        Assert.Equal(EntryType.Work, e.Type);
        Assert.Equal(new TimeSpan(8, 0, 0), e.StartTime);
        Assert.Equal(new TimeSpan(16, 0, 0), e.EndTime);
        Assert.Null(e.AbsenceStart);
        Assert.Equal("u1", e.UserId);
    }

    [Fact]
    public void ToDesktop_absence_range_sets_span_and_group()
    {
        var dto = new ServerEntryDto("abs1", "u1", "Vacation",
            new DateOnly(2026, 6, 2), new DateOnly(2026, 6, 5),
            null, null, false, null, "Urlaub", "Approved", false);

        var e = EntryMapping.ToDesktop(dto, new DateOnly(2026, 6, 3));

        Assert.Equal(EntryType.Vacation, e.Type);
        Assert.Equal("abs1", e.AbsenceGroupId);
        Assert.Equal(new DateOnly(2026, 6, 2), e.AbsenceStart);
        Assert.Equal(new DateOnly(2026, 6, 5), e.AbsenceEnd);
    }

    [Fact]
    public void ToDesktop_maps_category_and_note()
    {
        var dto = new ServerEntryDto("id2", "u1", "Activity",
            new DateOnly(2026, 6, 1), null,
            new TimeOnly(10, 0), new TimeOnly(12, 0), false,
            "Schwimmen", "mit Bus", "Approved", false);

        var e = EntryMapping.ToDesktop(dto, new DateOnly(2026, 6, 1));

        Assert.Equal("Schwimmen", e.Title);
        Assert.Equal("mit Bus", e.Notes);
    }

    [Fact]
    public void Activity_category_round_trips_via_activity_type_id()
    {
        var dto = new ServerEntryDto("id3", "u1", "Activity",
            new DateOnly(2026, 6, 1), null,
            new TimeOnly(10, 0), new TimeOnly(12, 0), false,
            null, null, "Approved", false, ActivityTypeId: "cat-7");

        var e = EntryMapping.ToDesktop(dto, new DateOnly(2026, 6, 1));
        Assert.Equal("cat-7", e.ActivityTypeId);

        var body = EntryMapping.ToCreateBody(e, new DateOnly(2026, 6, 1));
        Assert.Equal("cat-7", body.ActivityTypeId);

        var update = EntryMapping.ToUpdateBody(e, new DateOnly(2026, 6, 1));
        Assert.Equal("cat-7", update.ActivityTypeId);
    }

    [Fact]
    public void ToCreateBody_timed_uses_day_and_times()
    {
        var e = new CalendarEntry
        {
            UserId = "u1",
            Type = EntryType.Work,
            StartTime = new TimeSpan(8, 0, 0),
            EndTime = new TimeSpan(16, 0, 0)
        };

        var body = EntryMapping.ToCreateBody(e, new DateOnly(2026, 6, 1));

        Assert.Equal("Work", body.Type);
        Assert.Equal(new DateOnly(2026, 6, 1), body.Date);
        Assert.Null(body.EndDate);
        Assert.Equal(new TimeOnly(8, 0), body.StartTime);
        Assert.Equal(new TimeOnly(16, 0), body.EndTime);
        Assert.False(body.EndsNextDay);
    }

    [Fact]
    public void ToCreateBody_overnight_sets_ends_next_day()
    {
        var e = new CalendarEntry
        {
            UserId = "u1",
            Type = EntryType.Overnight,
            StartTime = new TimeSpan(20, 0, 0),
            EndTime = new TimeSpan(6, 0, 0)   // EndTime <= StartTime => über Mitternacht
        };

        var body = EntryMapping.ToCreateBody(e, new DateOnly(2026, 6, 1));

        Assert.True(body.EndsNextDay);
        Assert.Equal("Overnight", body.Type);
    }

    [Fact]
    public void ToCreateBody_absence_uses_span_no_times()
    {
        var e = new CalendarEntry
        {
            UserId = "u1",
            Type = EntryType.Vacation,
            AbsenceGroupId = "g1",
            AbsenceStart = new DateOnly(2026, 6, 2),
            AbsenceEnd = new DateOnly(2026, 6, 5)
        };

        var body = EntryMapping.ToCreateBody(e, new DateOnly(2026, 6, 3));

        Assert.Equal("Vacation", body.Type);
        Assert.Equal(new DateOnly(2026, 6, 2), body.Date);
        Assert.Equal(new DateOnly(2026, 6, 5), body.EndDate);
        Assert.Null(body.StartTime);
        Assert.Null(body.EndTime);
    }

    [Fact]
    public void ToUpdateBody_mirrors_create_fields()
    {
        var e = new CalendarEntry
        {
            UserId = "u1",
            Type = EntryType.Work,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(17, 0, 0)
        };

        var body = EntryMapping.ToUpdateBody(e, new DateOnly(2026, 6, 1));

        Assert.Equal(new DateOnly(2026, 6, 1), body.Date);
        Assert.Equal(new TimeOnly(9, 0), body.StartTime);
        Assert.Equal(new TimeOnly(17, 0), body.EndTime);
    }
}
