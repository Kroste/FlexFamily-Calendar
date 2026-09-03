using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using FlexFamilyCalendar.Services.Api;
using FlexFamilyCalendar.ViewModels;
using Xunit;

namespace FlexFamilyCalendar.Tests;

/// <summary>
/// Frei gewählte Kachelfarbe pro Eintrag: schlägt Kategorie und Typ — darf aber die
/// Privatsphäre-Maskierung nicht unterlaufen.
/// </summary>
public class EntryCustomColorTests
{
    private static readonly DateOnly Day = new(2026, 5, 25);
    private static User Person() => new()
    { Id = "emp", Username = "mara", DisplayName = "Mara", Category = PersonCategory.Employee };

    // ───────── Rangfolge ─────────

    [Fact]
    public void OwnColor_BeatsCategoryAndType()
    {
        Assert.Equal("#123456", EntryColors.Tile(EntryType.Activity, "#8E44AD", "#123456"));
        Assert.Equal("#123456", EntryColors.Tile(EntryType.Work, null, "#123456"));
    }

    [Fact]
    public void GarbageOwnColor_FallsThroughToCategory()
    {
        Assert.Equal("#8E44AD", EntryColors.Tile(EntryType.Activity, "#8E44AD", "blau"));
        Assert.Equal(EntryTypeInfo.Color(EntryType.Work), EntryColors.Tile(EntryType.Work, null, ""));
    }

    // ───────── Maskierung ─────────

    [Fact]
    public void TileColor_UsesOwnColor_WhenNothingIsMasked()
    {
        var e = new CalendarEntry { Type = EntryType.SickLeave, Color = "#123456" };
        e.DisplayType = EntryType.SickLeave;   // Admin oder man selbst sieht den echten Typ

        Assert.Equal("#123456", e.TileColor);
    }

    [Fact]
    public void TileColor_DropsOwnColor_WhenEntryIsMasked()
    {
        // Fremde Krankmeldung erscheint als „Abwesend". Bliebe die Sonderfarbe stehen, wäre der
        // Eintrag von echten Abwesenheiten unterscheidbar — die Maskierung wäre über die Optik
        // unterlaufen.
        var e = new CalendarEntry { Type = EntryType.SickLeave, Color = "#123456" };
        e.DisplayType = EntryType.Absence;

        Assert.Equal(EntryColors.ForType(EntryType.Absence), e.TileColor);
    }

    [Fact]
    public void Export_DropsOwnColor_PerRecipient()
    {
        var e = new CalendarEntry
        {
            Id = "e1", UserId = "emp", Type = EntryType.SickLeave, Color = "#123456",
            AbsenceStart = Day, AbsenceEnd = Day
        };

        var forOwner = PlanExportBuilder.CellEntry(e, viewerIsAdmin: false, viewerId: "emp", EntryTypeInfo.Label);
        var forStranger = PlanExportBuilder.CellEntry(e, viewerIsAdmin: false, viewerId: "someone", EntryTypeInfo.Label);

        Assert.Equal("#123456", forOwner.ColorHex);
        Assert.Equal(EntryColors.ForType(EntryType.Absence), forStranger.ColorHex);
    }

    // ───────── Dialog ─────────

    [Fact]
    public void Editor_SavesChosenColor()
    {
        var vm = new EntryEditorViewModel(Day, new[] { Person() });
        vm.SelectedType = vm.EntryTypes.First(t => t.Type == EntryType.Work);
        vm.StartTime = new TimeSpan(7, 0, 0);
        vm.EndTime = new TimeSpan(15, 0, 0);
        vm.UseCustomColor = true;
        vm.Color = "#F39C12";

        EntryDialogResult? result = null;
        vm.Closed += r => result = r;
        vm.SaveCommand.Execute(null);

        Assert.Equal("#F39C12", result!.Entry.Color);
    }

    [Fact]
    public void Editor_TogglingOn_StartsFromTheAutomaticColor()
    {
        var vm = new EntryEditorViewModel(Day, new[] { Person() });
        vm.SelectedType = vm.EntryTypes.First(t => t.Type == EntryType.Work);

        vm.UseCustomColor = true;

        // Von der Farbe aus verschieben, die der Eintrag ohnehin hätte — nicht bei Schwarz starten.
        Assert.Equal(EntryColors.ForType(EntryType.Work), vm.Color);
    }

    [Fact]
    public void Editor_TogglingOff_ReturnsToAutomatic()
    {
        var vm = new EntryEditorViewModel(Day, new[] { Person() });
        vm.SelectedType = vm.EntryTypes.First(t => t.Type == EntryType.Work);
        vm.StartTime = new TimeSpan(7, 0, 0);
        vm.EndTime = new TimeSpan(15, 0, 0);
        vm.UseCustomColor = true;
        vm.Color = "#F39C12";

        vm.UseCustomColor = false;

        Assert.Equal("", vm.Color);
        Assert.Equal(vm.AutoColor, vm.PreviewColor);

        EntryDialogResult? result = null;
        vm.Closed += r => result = r;
        vm.SaveCommand.Execute(null);
        Assert.Equal("", result!.Entry.Color);
    }

    [Fact]
    public void Editor_PreviewForeground_FollowsTheChosenColor()
    {
        var vm = new EntryEditorViewModel(Day, new[] { Person() });
        vm.UseCustomColor = true;

        vm.Color = "#F39C12";
        Assert.Equal("#000000", vm.PreviewForeground);

        vm.Color = "#5B4B8A";
        Assert.Equal("#FFFFFF", vm.PreviewForeground);
    }

    [Fact]
    public void Editor_EditingKeepsStoredColor()
    {
        var existing = new CalendarEntry
        {
            Id = "e1", UserId = "emp", Type = EntryType.Work, Color = "#16A085",
            StartTime = new TimeSpan(7, 0, 0), EndTime = new TimeSpan(15, 0, 0)
        };

        var vm = new EntryEditorViewModel(Day, new[] { Person() }, existing);

        Assert.True(vm.UseCustomColor);
        Assert.Equal("#16A085", vm.Color);
    }

    // ───────── Persistenz ─────────

    [Fact]
    public void Mapping_RoundTripsTheColor()
    {
        var e = new CalendarEntry
        {
            Id = "e1", UserId = "emp", Type = EntryType.Work, Color = "#F39C12",
            StartTime = new TimeSpan(7, 0, 0), EndTime = new TimeSpan(15, 0, 0)
        };

        var body = EntryMapping.ToCreateBody(e, Day);
        Assert.Equal("#F39C12", body.Color);

        var back = EntryMapping.ToDesktop(new ServerEntryDto(
            "e1", "emp", "Work", Day, null, new TimeOnly(7, 0), new TimeOnly(15, 0),
            false, null, null, EntryStatuses.Approved, false, null, "#F39C12"), Day);
        Assert.Equal("#F39C12", back.Color);
    }

    [Fact]
    public void Mapping_MaskedServerEntry_ArrivesWithoutColor()
    {
        // So liefert der Server einen maskierten Eintrag aus (EntryDto.Mask setzt Color null).
        var back = EntryMapping.ToDesktop(new ServerEntryDto(
            "e1", "emp", "Absence", Day, Day, null, null, false, null, null,
            EntryStatuses.Approved, true, null, null), Day);

        Assert.Equal("", back.Color);
        Assert.Equal(EntryColors.ForType(EntryType.Absence), back.TileColor);
    }

    [Fact]
    public void AbsenceRange_KeepsTheColorOnEveryDay()
    {
        var template = new CalendarEntry
        { Id = "e1", UserId = "emp", Type = EntryType.Vacation, Color = "#27AE60" };

        var days = AbsencePlanner.Build(template, Day, Day.AddDays(3), "g1");

        Assert.Equal(4, days.Count);
        Assert.All(days, d => Assert.Equal("#27AE60", d.Entry.Color));
    }
}
