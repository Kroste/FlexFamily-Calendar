using FlexFamilyCalendar.Api.Entries;
using FlexFamilyCalendar.Api.Models;

namespace FlexFamilyCalendar.Api.Tests;

public class EntryVisibilityTests
{
    static CalendarEntry Sick(Guid owner, string status = EntryStatus.Approved) => new()
    {
        UserId = owner,
        Type = EntryTypes.SickLeave,
        Date = new DateOnly(2026, 5, 25),
        EndDate = new DateOnly(2026, 5, 27),
        Note = "Grippe",
        Status = status
    };

    static CalendarEntry Work(Guid owner) => new()
    {
        UserId = owner,
        Type = EntryTypes.Work,
        Date = new DateOnly(2026, 5, 25),
        StartTime = new TimeOnly(8, 0),
        EndTime = new TimeOnly(16, 0),
        Status = EntryStatus.Approved
    };

    [Fact]
    public void Admin_sees_full_private_entry()
    {
        var dto = EntryVisibility.Project(Sick(Guid.NewGuid()), requesterId: Guid.NewGuid(), isAdmin: true);
        Assert.NotNull(dto);
        Assert.False(dto!.Masked);
        Assert.Equal(EntryTypes.SickLeave, dto.Type);
        Assert.Equal("Grippe", dto.Note);
    }

    [Fact]
    public void Owner_sees_full_private_entry()
    {
        var owner = Guid.NewGuid();
        var dto = EntryVisibility.Project(Sick(owner), requesterId: owner, isAdmin: false);
        Assert.NotNull(dto);
        Assert.False(dto!.Masked);
        Assert.Equal("Grippe", dto.Note);
    }

    [Fact]
    public void Stranger_sees_private_entry_masked_as_absence()
    {
        var dto = EntryVisibility.Project(Sick(Guid.NewGuid()), requesterId: Guid.NewGuid(), isAdmin: false);
        Assert.NotNull(dto);
        Assert.True(dto!.Masked);
        Assert.Equal(EntryTypes.Absence, dto.Type);
        Assert.Null(dto.Note);
        Assert.Null(dto.StartTime);
        // Zeitraum bleibt sichtbar (Verfügbarkeit), Grund nicht.
        Assert.Equal(new DateOnly(2026, 5, 25), dto.Date);
        Assert.Equal(new DateOnly(2026, 5, 27), dto.EndDate);
    }

    [Fact]
    public void Stranger_does_not_see_pending_entry()
    {
        var dto = EntryVisibility.Project(Sick(Guid.NewGuid(), EntryStatus.Pending), requesterId: Guid.NewGuid(), isAdmin: false);
        Assert.Null(dto);
    }

    [Fact]
    public void Owner_sees_own_pending_entry()
    {
        var owner = Guid.NewGuid();
        var dto = EntryVisibility.Project(Sick(owner, EntryStatus.Pending), requesterId: owner, isAdmin: false);
        Assert.NotNull(dto);
    }

    [Fact]
    public void Stranger_sees_work_shift_in_full()
    {
        var dto = EntryVisibility.Project(Work(Guid.NewGuid()), requesterId: Guid.NewGuid(), isAdmin: false);
        Assert.NotNull(dto);
        Assert.False(dto!.Masked);
        Assert.Equal(EntryTypes.Work, dto.Type);
        Assert.Equal(new TimeOnly(8, 0), dto.StartTime);
    }
}

public class EntryWriteRulesTests
{
    [Fact]
    public void Admin_may_create_work_for_anyone()
        => Assert.Null(EntryWriteRules.CheckCreate(EntryTypes.Work, Guid.NewGuid(), Guid.NewGuid(), isAdmin: true));

    [Fact]
    public void Nonadmin_may_not_create_for_others()
        => Assert.NotNull(EntryWriteRules.CheckCreate(EntryTypes.Vacation, Guid.NewGuid(), Guid.NewGuid(), isAdmin: false));

    [Fact]
    public void Nonadmin_may_not_create_work_for_self()
    {
        var me = Guid.NewGuid();
        Assert.NotNull(EntryWriteRules.CheckCreate(EntryTypes.Work, me, me, isAdmin: false));
    }

    [Fact]
    public void Nonadmin_may_create_vacation_wish_for_self()
    {
        var me = Guid.NewGuid();
        Assert.Null(EntryWriteRules.CheckCreate(EntryTypes.Vacation, me, me, isAdmin: false));
    }

    [Fact]
    public void Nonadmin_may_create_sickleave_for_self()
    {
        var me = Guid.NewGuid();
        Assert.Null(EntryWriteRules.CheckCreate(EntryTypes.SickLeave, me, me, isAdmin: false));
    }

    [Fact]
    public void Unknown_type_is_rejected()
        => Assert.NotNull(EntryWriteRules.CheckCreate("Nonsense", Guid.NewGuid(), Guid.NewGuid(), isAdmin: true));

    [Fact]
    public void Vacation_wish_starts_pending_for_nonadmin()
        => Assert.Equal(EntryStatus.Pending, EntryWriteRules.InitialStatus(EntryTypes.Vacation, isAdmin: false));

    [Fact]
    public void Sickleave_starts_approved_for_nonadmin()
        => Assert.Equal(EntryStatus.Approved, EntryWriteRules.InitialStatus(EntryTypes.SickLeave, isAdmin: false));

    [Fact]
    public void Admin_entries_start_approved()
        => Assert.Equal(EntryStatus.Approved, EntryWriteRules.InitialStatus(EntryTypes.Vacation, isAdmin: true));

    [Fact]
    public void Timed_entry_requires_times()
        => Assert.NotNull(EntryWriteRules.Validate(EntryTypes.Work, new DateOnly(2026, 5, 25), null, null, null, null));

    [Fact]
    public void Activity_with_no_label_and_no_category_is_rejected()
        => Assert.NotNull(EntryWriteRules.Validate(EntryTypes.Activity, new DateOnly(2026, 5, 25), null, new TimeOnly(10, 0), new TimeOnly(12, 0), categoryLabel: null, activityTypeId: null));

    [Fact]
    public void Activity_with_only_activity_type_id_is_accepted()
        => Assert.Null(EntryWriteRules.Validate(EntryTypes.Activity, new DateOnly(2026, 5, 25), null, new TimeOnly(10, 0), new TimeOnly(12, 0), categoryLabel: null, activityTypeId: "act-123"));

    [Fact]
    public void Activity_with_only_free_text_label_is_accepted()
        => Assert.Null(EntryWriteRules.Validate(EntryTypes.Activity, new DateOnly(2026, 5, 25), null, new TimeOnly(10, 0), new TimeOnly(12, 0), categoryLabel: "Klavier", activityTypeId: null));

    [Fact]
    public void Range_with_end_before_start_is_rejected()
        => Assert.NotNull(EntryWriteRules.Validate(EntryTypes.Vacation, new DateOnly(2026, 5, 25), new DateOnly(2026, 5, 20), null, null, null));

    [Fact]
    public void Valid_vacation_range_passes()
        => Assert.Null(EntryWriteRules.Validate(EntryTypes.Vacation, new DateOnly(2026, 5, 25), new DateOnly(2026, 5, 27), null, null, null));
}
