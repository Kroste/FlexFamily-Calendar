using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.ViewModels;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class EntrySelfAbsenceTests
{
    private static readonly DateOnly Day = new(2026, 5, 25);
    private static User Self() => new() { Id = "me", Username = "anna", DisplayName = "Anna" };

    private static readonly EntryType[] SickAndVacation = { EntryType.SickLeave, EntryType.Vacation };
    private static readonly EntryType[] SickOnly = { EntryType.SickLeave };

    [Fact]
    public void SelfAbsence_LimitsTypes_ToSickAndVacation()
    {
        var vm = new EntryEditorViewModel(Day, new[] { Self() }, canPickUser: false, allowedTypes: SickAndVacation);

        var types = vm.EntryTypes.Select(t => t.Type).ToList();
        Assert.Equal(2, types.Count);
        Assert.Contains(EntryType.SickLeave, types);
        Assert.Contains(EntryType.Vacation, types);
        Assert.False(vm.CanPickUser);
        Assert.Equal(EntryType.SickLeave, vm.SelectedType!.Type);
    }

    [Fact]
    public void FinalizedWeek_AllowsOnlySick()
    {
        var vm = new EntryEditorViewModel(Day, new[] { Self() }, canPickUser: false, allowedTypes: SickOnly);

        Assert.Single(vm.EntryTypes);
        Assert.Equal(EntryType.SickLeave, vm.EntryTypes[0].Type);
        Assert.Equal(EntryType.SickLeave, vm.SelectedType!.Type);
    }

    [Fact]
    public void FullMode_HasAllTypes_AndUserPicker()
    {
        var vm = new EntryEditorViewModel(Day, new[] { Self() });

        Assert.True(vm.CanPickUser);
        Assert.Equal(Enum.GetValues<EntryType>().Length, vm.EntryTypes.Count);
        Assert.Equal(EntryType.Work, vm.SelectedType!.Type);
    }

    [Fact]
    public void SelfAbsence_Save_ProducesEntryForSelf()
    {
        var vm = new EntryEditorViewModel(Day, new[] { Self() }, canPickUser: false, allowedTypes: SickAndVacation);
        vm.SelectedType = vm.EntryTypes.First(t => t.Type == EntryType.Vacation);

        EntryDialogResult? result = null;
        vm.Closed += r => result = r;
        vm.SaveCommand.Execute(null);

        Assert.NotNull(result);
        Assert.Equal(EntryDialogAction.Save, result!.Action);
        Assert.Equal("me", result.Entry.UserId);
        Assert.Equal(EntryType.Vacation, result.Entry.Type);
    }
}
