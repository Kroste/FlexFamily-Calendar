using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.ViewModels;
using Xunit;

namespace FlexFamilyCalendar.Tests;

/// <summary>Selbst-Antrag (Krank/Urlaub) und der Modus-Umschalter des Eintrag-Dialogs.</summary>
public class EntrySelfAbsenceTests
{
    private static readonly DateOnly Day = new(2026, 5, 25);
    private static User Self() => new() { Id = "me", Username = "anna", DisplayName = "Anna" };

    private static readonly EntryType[] SickAndVacation = { EntryType.SickLeave, EntryType.Vacation };
    private static readonly EntryType[] SickOnly = { EntryType.SickLeave };

    [Fact]
    public void SelfAbsence_OpensStraightInAbsenceMode_WithSickAndVacation()
    {
        var vm = new EntryEditorViewModel(Day, new[] { Self() }, canPickUser: false, allowedTypes: SickAndVacation);

        Assert.True(vm.IsAbsenceMode);
        Assert.False(vm.CanSwitchMode);          // ein Mitarbeiter legt keine Schichten an
        Assert.False(vm.CanPickUser);
        Assert.Equal(new[] { EntryType.SickLeave, EntryType.Vacation }, vm.AbsenceKinds.Select(k => k.Type));
        Assert.Equal(EntryType.SickLeave, vm.SelectedAbsenceKind!.Type);
    }

    [Fact]
    public void FinalizedWeek_AllowsOnlySick()
    {
        var vm = new EntryEditorViewModel(Day, new[] { Self() }, canPickUser: false, allowedTypes: SickOnly);

        var only = Assert.Single(vm.AbsenceKinds);
        Assert.Equal(EntryType.SickLeave, only.Type);
        Assert.Equal(EntryType.SickLeave, vm.SelectedAbsenceKind!.Type);
    }

    [Fact]
    public void Admin_NewEntry_StartsAsEntry_AndCanSwitch()
    {
        var vm = new EntryEditorViewModel(Day, new[] { Self() });

        Assert.True(vm.CanPickUser);
        Assert.True(vm.CanSwitchMode);
        Assert.True(vm.IsEntryMode);
        Assert.Equal(EntryType.Work, vm.EffectiveType);   // neue Einträge zählen als Arbeit
        Assert.Equal(3, vm.AbsenceKinds.Count);          // Urlaub, Krank, Abwesend
    }

    [Fact]
    public void SwitchingToAbsence_ChangesWhatGetsSaved()
    {
        var vm = new EntryEditorViewModel(Day, new[] { Self() });

        vm.IsAbsenceMode = true;
        vm.SelectedAbsenceKind = vm.AbsenceKinds.First(k => k.Type == EntryType.Vacation);

        Assert.True(vm.IsAllDay);            // Abwesenheiten starten ganztägig
        Assert.False(vm.ShowTimes);
        Assert.Equal(EntryType.Vacation, vm.EffectiveType);
    }

    [Fact]
    public void SelfAbsence_Save_ProducesEntryForSelf()
    {
        var vm = new EntryEditorViewModel(Day, new[] { Self() }, canPickUser: false, allowedTypes: SickAndVacation);
        vm.SelectedAbsenceKind = vm.AbsenceKinds.First(t => t.Type == EntryType.Vacation);

        EntryDialogResult? result = null;
        vm.Closed += r => result = r;
        vm.SaveCommand.Execute(null);

        Assert.NotNull(result);
        Assert.Equal(EntryDialogAction.Save, result!.Action);
        Assert.Equal("me", result.Entry.UserId);
        Assert.Equal(EntryType.Vacation, result.Entry.Type);
    }
}
