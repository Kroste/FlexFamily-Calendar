using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.ViewModels;
using Xunit;

namespace FlexFamilyCalendar.Tests;

/// <summary>
/// Das Typ-Dropdown listet feste Typen und die eigenen Kategorien in einer Ebene — „Sprachschule"
/// oder „Remise" sind damit direkt wählbar statt hinter „Aktivität" versteckt.
/// </summary>
public class EntryEditorTypeOptionsTests
{
    private static readonly DateOnly Day = new(2026, 5, 25);

    private static User Employee() => new()
    { Id = "emp", Username = "mara", DisplayName = "Mara", Category = PersonCategory.Employee };

    private static User Child() => new()
    { Id = "kid", Username = "tim", DisplayName = "Tim", Category = PersonCategory.Child };

    private static ActivityType Language() => new()
    { Id = "a1", Name = "Sprachschule", Color = "#8E44AD", Categories = { PersonCategory.Employee } };

    private static ActivityType Depot() => new()
    { Id = "a2", Name = "Remise", Color = "#34495E", Categories = { PersonCategory.Employee } };

    private static ActivityType School() => new()
    { Id = "a3", Name = "Schule", Color = "#16A085", Categories = { PersonCategory.Child } };

    private static EntryEditorViewModel Vm(User user, params ActivityType[] types)
        => new(Day, new[] { user }, activityTypes: types);

    [Fact]
    public void Categories_AppearDirectlyInTypeList()
    {
        var vm = Vm(Employee(), Language(), Depot());

        var labels = vm.EntryTypes.Select(t => t.Label).ToList();
        Assert.Contains("Sprachschule", labels);
        Assert.Contains("Remise", labels);
    }

    [Fact]
    public void GenericActivityOption_DisappearsWhenCategoriesExist()
    {
        var vm = Vm(Employee(), Language());

        // „Aktivität" neben „Sprachschule" wäre eine Auswahl ohne Bedeutung.
        Assert.DoesNotContain(vm.EntryTypes, t => t.Type == EntryType.Activity && t.Activity is null);
        Assert.Single(vm.EntryTypes, t => t.Type == EntryType.Activity);
    }

    [Fact]
    public void GenericActivityOption_StaysWhenNoCategoryApplies()
    {
        // Ohne passende Kategorie darf die Fähigkeit „Aktivität eintragen" nicht verloren gehen.
        var vm = Vm(Employee(), School());

        Assert.Contains(vm.EntryTypes, t => t.Type == EntryType.Activity && t.Activity is null);
    }

    [Fact]
    public void Categories_AreFilteredByPersonCategory()
    {
        var vm = Vm(Child(), Language(), School());

        var labels = vm.EntryTypes.Select(t => t.Label).ToList();
        Assert.Contains("Schule", labels);
        Assert.DoesNotContain("Sprachschule", labels);
    }

    [Fact]
    public void SwitchingPerson_RebuildsTheList()
    {
        var child = Child();
        var employee = Employee();
        var vm = new EntryEditorViewModel(Day, new[] { child, employee },
            activityTypes: new[] { Language(), School() });

        vm.SelectedUser = child;
        Assert.Contains(vm.EntryTypes, t => t.Label == "Schule");
        Assert.DoesNotContain(vm.EntryTypes, t => t.Label == "Sprachschule");

        vm.SelectedUser = employee;
        Assert.Contains(vm.EntryTypes, t => t.Label == "Sprachschule");
        Assert.DoesNotContain(vm.EntryTypes, t => t.Label == "Schule");
    }

    [Fact]
    public void ChoosingCategory_SavesActivityWithIdAndName()
    {
        var vm = Vm(Employee(), Language());
        vm.SelectedType = vm.EntryTypes.First(t => t.Label == "Sprachschule");
        vm.StartTime = new TimeSpan(9, 0, 0);
        vm.EndTime = new TimeSpan(12, 0, 0);

        EntryDialogResult? result = null;
        vm.Closed += r => result = r;
        vm.SaveCommand.Execute(null);

        Assert.NotNull(result);
        Assert.Equal(EntryType.Activity, result!.Entry.Type);
        Assert.Equal("a1", result.Entry.ActivityTypeId);
        // Ohne eigenen Titel übernimmt der Eintrag den Kategorienamen — sonst stünde er
        // im Plan und im PDF namenlos da.
        Assert.Equal("Sprachschule", result.Entry.Title);
    }

    [Fact]
    public void EditingActivity_PreselectsItsCategory()
    {
        var existing = new CalendarEntry
        {
            Id = "e1", UserId = "emp", Type = EntryType.Activity, ActivityTypeId = "a2",
            StartTime = new TimeSpan(6, 0, 0), EndTime = new TimeSpan(14, 0, 0)
        };

        var vm = new EntryEditorViewModel(Day, new[] { Employee() }, existing,
            activityTypes: new[] { Language(), Depot() });

        Assert.Equal("Remise", vm.SelectedType!.Label);
    }

    [Fact]
    public void EditingActivity_WithDeletedCategory_FallsBackInsteadOfLosingSelection()
    {
        var existing = new CalendarEntry
        {
            Id = "e1", UserId = "emp", Type = EntryType.Activity, ActivityTypeId = "weg",
            StartTime = new TimeSpan(6, 0, 0), EndTime = new TimeSpan(14, 0, 0)
        };

        var vm = new EntryEditorViewModel(Day, new[] { Employee() }, existing,
            activityTypes: new[] { Language() });

        Assert.NotNull(vm.SelectedType);
        Assert.Equal(EntryType.Activity, vm.SelectedType!.Type);
    }

    [Fact]
    public void RestrictedTypes_OfferNoCategories()
    {
        // Finalisierte Woche: nur noch Krankmeldung. Dann darf auch keine Kategorie auftauchen.
        var vm = new EntryEditorViewModel(Day, new[] { Employee() }, canPickUser: false,
            allowedTypes: new[] { EntryType.SickLeave }, activityTypes: new[] { Language() });

        Assert.Single(vm.EntryTypes);
        Assert.Equal(EntryType.SickLeave, vm.EntryTypes[0].Type);
    }

    [Fact]
    public void PreviewColor_TakesCategoryColor_ElseTypeColor()
    {
        var vm = Vm(Employee(), Language());

        Assert.Equal("#8E44AD", vm.EntryTypes.First(t => t.Label == "Sprachschule").PreviewColor);
        Assert.Equal(EntryTypeInfo.Color(EntryType.Work),
                     vm.EntryTypes.First(t => t.Type == EntryType.Work).PreviewColor);
    }
}
