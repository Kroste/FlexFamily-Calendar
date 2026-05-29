using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class EntryMoveCopyTests
{
    private static readonly DateOnly Mon = new(2026, 5, 25);
    private static readonly DateOnly Tue = new(2026, 5, 26);

    private static CalendarEntry Shift(string id = "e1", string user = "anna", EntryType type = EntryType.Work) => new()
    {
        Id = id,
        UserId = user,
        UserDisplayName = user,
        Type = type,
        StartTime = TimeSpan.FromHours(8),
        EndTime = TimeSpan.FromHours(16),
        Title = "Frühschicht"
    };

    [Fact]
    public void CanDrag_RejectsAbsence()
    {
        Assert.False(EntryMoveCopy.CanDrag(Shift(type: EntryType.SickLeave)));
        Assert.False(EntryMoveCopy.CanDrag(Shift(type: EntryType.Vacation)));
        Assert.False(EntryMoveCopy.CanDrag(Shift(type: EntryType.Absence)));
    }

    [Fact]
    public void CanDrag_RejectsRecurring()
    {
        var e = Shift();
        e.IsRecurring = true;
        Assert.False(EntryMoveCopy.CanDrag(e));
    }

    [Fact]
    public void CanDrag_AcceptsWorkActivityOvernight()
    {
        Assert.True(EntryMoveCopy.CanDrag(Shift(type: EntryType.Work)));
        Assert.True(EntryMoveCopy.CanDrag(Shift(type: EntryType.Activity)));
        Assert.True(EntryMoveCopy.CanDrag(Shift(type: EntryType.Overnight)));
    }

    [Fact]
    public void Plan_Move_ProducesDeleteAndSave()
    {
        var src = Shift();
        var plan = EntryMoveCopy.Plan(src, Mon, Tue, "bob", "Bob", MoveCopyAction.Move);

        Assert.NotNull(plan);
        Assert.Same(src, plan!.Delete);
        Assert.Equal(Mon, plan.DeleteFromDate);
        Assert.Equal(Tue, plan.SaveToDate);
        Assert.Equal("bob", plan.Save.UserId);
        Assert.Equal("Bob", plan.Save.UserDisplayName);
        Assert.NotEqual(src.Id, plan.Save.Id);   // neue Id
        Assert.Equal(src.StartTime, plan.Save.StartTime);
        Assert.Equal(src.EndTime, plan.Save.EndTime);
        Assert.Equal(src.Title, plan.Save.Title);
        Assert.Equal(src.Type, plan.Save.Type);
    }

    [Fact]
    public void Plan_Copy_ProducesOnlySave_WithNewId()
    {
        var src = Shift();
        var plan = EntryMoveCopy.Plan(src, Mon, Tue, "bob", "Bob", MoveCopyAction.Copy);

        Assert.NotNull(plan);
        Assert.Null(plan!.Delete);
        Assert.Null(plan.DeleteFromDate);
        Assert.Equal(Tue, plan.SaveToDate);
        Assert.Equal("bob", plan.Save.UserId);
        Assert.NotEqual(src.Id, plan.Save.Id);
    }

    [Fact]
    public void Plan_SameUserSameDay_IsNoOp()
    {
        var src = Shift();
        Assert.Null(EntryMoveCopy.Plan(src, Mon, Mon, src.UserId, src.UserDisplayName, MoveCopyAction.Move));
    }

    [Fact]
    public void Plan_SameUserDifferentDay_IsAllowed()
    {
        var src = Shift();
        var plan = EntryMoveCopy.Plan(src, Mon, Tue, src.UserId, src.UserDisplayName, MoveCopyAction.Move);
        Assert.NotNull(plan);
        Assert.Equal(src.UserId, plan!.Save.UserId);
    }

    [Fact]
    public void Plan_DifferentUserSameDay_IsAllowed()
    {
        var src = Shift();
        var plan = EntryMoveCopy.Plan(src, Mon, Mon, "bob", "Bob", MoveCopyAction.Copy);
        Assert.NotNull(plan);
        Assert.Equal("bob", plan!.Save.UserId);
    }

    [Fact]
    public void Plan_OnRecurring_ReturnsNull()
    {
        var src = Shift();
        src.IsRecurring = true;
        Assert.Null(EntryMoveCopy.Plan(src, Mon, Tue, "bob", "Bob", MoveCopyAction.Move));
    }

    [Fact]
    public void Plan_OnAbsence_ReturnsNull()
    {
        var src = Shift(type: EntryType.SickLeave);
        Assert.Null(EntryMoveCopy.Plan(src, Mon, Tue, "bob", "Bob", MoveCopyAction.Move));
    }
}
