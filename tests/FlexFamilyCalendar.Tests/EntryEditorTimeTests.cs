using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.ViewModels;
using Xunit;

namespace FlexFamilyCalendar.Tests;

/// <summary>
/// Zeitfelder im Eintrag-Dialog: ein neuer Eintrag startet leer (früher fest 08:00–16:00),
/// Abwesenheiten brauchen gar keine Uhrzeit.
/// </summary>
public class EntryEditorTimeTests
{
    private static readonly DateOnly Day = new(2026, 5, 25);
    private static User Person() => new() { Id = "me", Username = "anna", DisplayName = "Anna" };

    private static EntryDialogResult? Save(EntryEditorViewModel vm)
    {
        EntryDialogResult? result = null;
        vm.Closed += r => result = r;
        vm.SaveCommand.Execute(null);
        return result;
    }

    [Fact]
    public void NewEntry_StartsWithEmptyTimes()
    {
        var vm = new EntryEditorViewModel(Day, new[] { Person() });

        Assert.Null(vm.StartTime);
        Assert.Null(vm.EndTime);
    }

    [Fact]
    public void NewEntry_ShowsTimeFields_ForWork()
    {
        var vm = new EntryEditorViewModel(Day, new[] { Person() });
        vm.SelectedType = vm.EntryTypes.First(t => t.Type == EntryType.Work);

        Assert.True(vm.ShowTimes);
    }

    [Fact]
    public void Work_WithoutTimes_IsRejected()
    {
        var vm = new EntryEditorViewModel(Day, new[] { Person() });
        vm.SelectedType = vm.EntryTypes.First(t => t.Type == EntryType.Work);

        var result = Save(vm);

        Assert.Null(result);
        Assert.False(string.IsNullOrEmpty(vm.ErrorMessage));
    }

    [Fact]
    public void Work_WithTypedTimes_Saves()
    {
        var vm = new EntryEditorViewModel(Day, new[] { Person() });
        vm.SelectedType = vm.EntryTypes.First(t => t.Type == EntryType.Work);
        vm.StartTime = new TimeSpan(7, 0, 0);
        vm.EndTime = new TimeSpan(15, 30, 0);

        var result = Save(vm);

        Assert.NotNull(result);
        Assert.Equal(new TimeSpan(7, 0, 0), result!.Entry.StartTime);
        Assert.Equal(new TimeSpan(15, 30, 0), result.Entry.EndTime);
    }

    [Theory]
    [InlineData(EntryType.Vacation)]
    [InlineData(EntryType.SickLeave)]
    public void Absence_NeedsNoTimes(EntryType type)
    {
        var vm = new EntryEditorViewModel(Day, new[] { Person() });
        vm.SelectedType = vm.EntryTypes.First(t => t.Type == type);

        Assert.False(vm.ShowTimes);

        // Ohne die Ausnahme in Save() würde der leere Startwert hier einen Fehler melden —
        // ein Urlaubsantrag hätte sich seit den leeren Feldern nicht mehr absenden lassen.
        var result = Save(vm);

        Assert.NotNull(result);
        Assert.Equal(TimeSpan.Zero, result!.Entry.StartTime);
        Assert.Equal(TimeSpan.Zero, result.Entry.EndTime);
    }

    [Fact]
    public void EditMode_KeepsTimesOfExistingEntry()
    {
        var existing = new CalendarEntry
        {
            Id = "e1",
            UserId = "me",
            Type = EntryType.Work,
            StartTime = new TimeSpan(6, 15, 0),
            EndTime = new TimeSpan(14, 45, 0)
        };

        var vm = new EntryEditorViewModel(Day, new[] { Person() }, existing);

        Assert.Equal(new TimeSpan(6, 15, 0), vm.StartTime);
        Assert.Equal(new TimeSpan(14, 45, 0), vm.EndTime);
    }

    [Fact]
    public void EditMode_Absence_KeepsOldTimes_InsteadOfZeroing()
    {
        // Altbestand: eine Abwesenheit, die noch mit den früheren Vorgabezeiten gespeichert wurde.
        // Die Felder sind jetzt ausgeblendet — beim erneuten Speichern dürfen die Werte trotzdem
        // nicht auf 00:00 zurückfallen.
        var existing = new CalendarEntry
        {
            Id = "e1",
            UserId = "me",
            Type = EntryType.Vacation,
            StartTime = new TimeSpan(8, 0, 0),
            EndTime = new TimeSpan(16, 0, 0)
        };

        var vm = new EntryEditorViewModel(Day, new[] { Person() }, existing);
        var result = Save(vm);

        Assert.NotNull(result);
        Assert.Equal(new TimeSpan(8, 0, 0), result!.Entry.StartTime);
        Assert.Equal(new TimeSpan(16, 0, 0), result.Entry.EndTime);
    }
}
