using System.Text.Json;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using FlexFamilyCalendar.Services.AI;
using FlexFamilyCalendar.ViewModels;
using Xunit;

namespace FlexFamilyCalendar.Tests;

/// <summary>
/// Einträge ohne Kategorien und ohne Typ-Auswahl: die Freitext-Bezeichnung ist der Name. Der
/// heikle Teil ist der Bestand — Kategoriename und -farbe müssen am Eintrag landen, sonst sieht
/// der Plan nach dem Umbau anders aus.
/// </summary>
public class LegacyCategoryEntryTests
{
    private static readonly List<LegacyCategory> Categories = new()
    {
        new("abc-123", "Sprachschule", "#8e44ad"),
        new("def-456", "Remise", "lila")          // ungültige Farbe
    };

    private static CalendarEntry Legacy(string categoryId, string title = "", string color = "") => new()
    { Id = Guid.NewGuid().ToString(), Type = EntryType.Activity, LegacyActivityTypeId = categoryId, Title = title, Color = color };

    [Fact]
    public void Name_and_colour_move_to_the_entry()
    {
        var e = Legacy("abc-123");

        Assert.True(LegacyCategoryMigration.ApplyToEntries(new[] { e }, Categories));

        Assert.Equal("Sprachschule", e.Title);
        Assert.Equal("#8E44AD", e.Color);
        Assert.Null(e.LegacyActivityTypeId);
    }

    [Fact]
    public void Own_name_and_own_colour_win()
    {
        var e = Legacy("abc-123", title: "Englisch B1", color: "#16A085");

        LegacyCategoryMigration.ApplyToEntries(new[] { e }, Categories);

        Assert.Equal("Englisch B1", e.Title);
        Assert.Equal("#16A085", e.Color);
    }

    [Fact]
    public void Invalid_category_colour_is_not_taken_over()
    {
        var e = Legacy("def-456");

        LegacyCategoryMigration.ApplyToEntries(new[] { e }, Categories);

        Assert.Equal("Remise", e.Title);
        Assert.Equal("", e.Color);
    }

    [Fact]
    public void Id_match_ignores_casing()
    {
        var e = Legacy("ABC-123");

        LegacyCategoryMigration.ApplyToEntries(new[] { e }, Categories);

        Assert.Equal("Sprachschule", e.Title);
    }

    [Fact]
    public void Deleted_category_only_clears_the_link()
    {
        var e = Legacy("weg");

        Assert.True(LegacyCategoryMigration.ApplyToEntries(new[] { e }, Categories));

        Assert.Equal("", e.Title);
        Assert.Null(e.LegacyActivityTypeId);
    }

    [Fact]
    public void Entries_without_a_category_are_left_alone()
    {
        var e = new CalendarEntry { Type = EntryType.Work, Title = "Arbeit" };

        Assert.False(LegacyCategoryMigration.ApplyToEntries(new[] { e }, Categories));
    }

    [Fact]
    public void Old_day_file_still_yields_the_category_and_new_one_does_not_write_it()
    {
        const string oldDay = """{ "DateString": "2026-09-21", "Entries": [ { "Id": "e1", "Type": 4, "Title": "", "ActivityTypeId": "abc-123" } ] }""";

        var day = JsonSerializer.Deserialize<CalendarDay>(oldDay, JsonOptions.Pretty)!;
        Assert.Equal("abc-123", day.Entries.Single().LegacyActivityTypeId);

        LegacyCategoryMigration.ApplyToEntries(day.Entries, Categories);
        var written = JsonSerializer.Serialize(day, JsonOptions.Pretty);

        Assert.DoesNotContain("ActivityTypeId", written);
        Assert.Contains("Sprachschule", written);
    }
}

/// <summary>Der Dialog ohne Typ-Auswahl: die Bezeichnung ist Pflicht, bestehende Einträge behalten ihren Typ.</summary>
[Collection("Localizer")]
public class FreeTextEntryDialogTests
{
    private static readonly DateOnly Day = new(2026, 9, 21);
    private static User Person() => new() { Id = "u1", Username = "mara", DisplayName = "Mara" };

    private static EntryDialogResult? Save(EntryEditorViewModel vm)
    {
        EntryDialogResult? result = null;
        vm.Closed += r => result = r;
        vm.SaveCommand.Execute(null);
        return result;
    }

    [Fact]
    public void New_entry_needs_a_name()
    {
        var vm = new EntryEditorViewModel(Day, new[] { Person() })
        {
            StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(16, 0, 0), Title = "  "
        };

        Assert.Null(Save(vm));
        Assert.Equal(Localizer.Instance["Entry_ErrorNoName"], vm.ErrorMessage);
    }

    [Fact]
    public void New_entry_is_saved_with_its_name_as_work()
    {
        var vm = new EntryEditorViewModel(Day, new[] { Person() })
        {
            StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(12, 0, 0), Title = " Sprachschule "
        };

        var result = Save(vm);

        Assert.NotNull(result);
        Assert.Equal("Sprachschule", result!.Entry.Title);
        // Bis das Stundenkonto umgebaut ist, zählen neue Einträge als Arbeit — so laufen Stunden,
        // Tausch und Freigabe-Regel unverändert weiter.
        Assert.Equal(EntryType.Work, result.Entry.Type);
    }

    [Fact]
    public void Editing_an_old_activity_keeps_its_type()
    {
        var existing = new CalendarEntry
        {
            Id = "e1", UserId = "u1", Type = EntryType.Activity, Title = "Fußball",
            StartTime = new TimeSpan(16, 0, 0), EndTime = new TimeSpan(17, 0, 0)
        };
        var vm = new EntryEditorViewModel(Day, new[] { Person() }, existing);

        Assert.Equal(EntryType.Activity, Save(vm)!.Entry.Type);
    }

    [Fact]
    public void Editing_an_untitled_old_shift_prefills_what_the_tile_showed()
    {
        // Alt-Einträge ohne Bezeichnung zeigten im Plan ihren Typ. Der landet im Pflichtfeld,
        // damit die Kachel nach dem Speichern gleich aussieht.
        var existing = new CalendarEntry
        {
            Id = "e1", UserId = "u1", Type = EntryType.Work, Title = "",
            StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(16, 0, 0)
        };
        var vm = new EntryEditorViewModel(Day, new[] { Person() }, existing);

        Assert.Equal(Localizer.Instance[EntryTypeInfo.Key(EntryType.Work)], vm.Title);
        Assert.NotNull(Save(vm));
    }

    [Fact]
    public void Editing_an_absence_opens_in_absence_mode_without_a_switch()
    {
        var existing = new CalendarEntry
        {
            Id = "e1", UserId = "u1", Type = EntryType.SickLeave,
            AbsenceStart = Day, AbsenceEnd = Day.AddDays(2)
        };
        var vm = new EntryEditorViewModel(Day, new[] { Person() }, existing);

        Assert.True(vm.IsAbsenceMode);
        Assert.False(vm.CanSwitchMode);
        Assert.Equal(EntryType.SickLeave, vm.SelectedAbsenceKind!.Type);
    }

    [Fact]
    public void Absence_does_not_need_a_name()
    {
        var vm = new EntryEditorViewModel(Day, new[] { Person() }) { IsAbsenceMode = true };

        Assert.NotNull(Save(vm));
    }
}

/// <summary>Kopieren und Verschieben verlieren die eigene Kachelfarbe nicht mehr.</summary>
public class CopyKeepsColourTests
{
    private static CalendarEntry Shift() => new()
    {
        Id = "e1", UserId = "u1", UserDisplayName = "Mara", Type = EntryType.Work, Title = "Remise",
        Color = "#8E44AD", StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(16, 0, 0)
    };

    [Fact]
    public void Week_copy_keeps_the_colour()
        => Assert.Equal("#8E44AD", WeekCopy.TemplateEntries(new[] { Shift() }).Single().Color);

    [Theory]
    [InlineData(MoveCopyAction.Copy)]
    [InlineData(MoveCopyAction.Move)]
    public void Move_and_copy_keep_the_colour(MoveCopyAction action)
    {
        var plan = EntryMoveCopy.Plan(Shift(), new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 22), "u2", "Tim", action);

        Assert.Equal("#8E44AD", plan!.Save.Color);
    }
}

/// <summary>Vorschläge fürs Tippen der Bezeichnung.</summary>
[Collection("Localizer")]
public class TitleSuggestionTests
{
    [Fact]
    public async Task Suggestions_come_from_the_week_and_the_series_but_never_from_absences()
    {
        var monday = DateOnly.FromDateTime(DateTime.Today);
        monday = monday.AddDays(-(((int)monday.DayOfWeek + 6) % 7));
        var admin = new User { Id = "admin", Username = "lars", DisplayName = "Lars", Role = UserRole.Admin, Category = PersonCategory.Parent };

        var storage = new InMemoryStorageService();
        await storage.SaveUsersAsync(new List<User> { admin });
        await storage.SaveDayAsync(new CalendarDay
        {
            DateString = monday.ToString("yyyy-MM-dd"),
            Entries =
            {
                new() { Id = "w", UserId = "admin", Type = EntryType.Work, Title = "Remise",
                        StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(12, 0, 0) },
                new() { Id = "w2", UserId = "admin", Type = EntryType.Work, Title = "remise",
                        StartTime = new TimeSpan(13, 0, 0), EndTime = new TimeSpan(14, 0, 0) },
                new() { Id = "s", UserId = "admin", Type = EntryType.SickLeave, Title = "Grippe",
                        AbsenceStart = monday, AbsenceEnd = monday }
            }
        });
        await storage.SaveRecurringActivitiesAsync(new List<RecurringActivity>
        {
            new() { Id = "r", UserId = "admin", Title = "Fußball", Weekdays = { DayOfWeek.Monday },
                    StartTime = new TimeSpan(16, 0, 0), EndTime = new TimeSpan(17, 0, 0) }
        });

        var vm = new CalendarViewModel(storage, admin, new NotificationService(storage),
            new AiService(Array.Empty<IAiProvider>()), new LocalMailSender(storage));
        await vm.RefreshAllAsync(silent: true);

        IReadOnlyList<string>? suggestions = null;
        vm.EntryDialogRequested += (_, _, _, _, _, s) => suggestions = s;
        vm.RequestAddEntry(monday, admin);

        Assert.NotNull(suggestions);
        Assert.Contains("Remise", suggestions!);
        Assert.Contains("Fußball", suggestions);
        Assert.Single(suggestions, s => s.Equals("Remise", StringComparison.OrdinalIgnoreCase));   // keine Dubletten
        // Der Vermerk einer Krankmeldung ist privat und gehört in keine Vorschlagsliste.
        Assert.DoesNotContain("Grippe", suggestions);
    }
}
