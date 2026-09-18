using System.Text.Json;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using FlexFamilyCalendar.ViewModels;
using Xunit;

namespace FlexFamilyCalendar.Tests;

/// <summary>
/// Serien tragen eine Freitext-Bezeichnung statt einer Kategorie. Der heikle Teil ist der Bestand:
/// bisher WAR die Kategorie der Name, der Titel blieb leer — ohne Übernahme stünden alle Serien
/// danach namenlos im Plan.
/// </summary>
public class RecurringTitleMigrationTests
{
    private static readonly List<ActivityType> Types = new()
    {
        new() { Id = "abc-123", Name = "Sprachschule" },
        new() { Id = "def-456", Name = "Fußball" }
    };

    [Fact]
    public void Category_name_becomes_the_title()
    {
        var rule = new RecurringActivity { Title = "", LegacyActivityTypeId = "abc-123" };

        Assert.True(RecurringTitleMigration.Apply(new[] { rule }, Types));

        Assert.Equal("Sprachschule", rule.Title);
        Assert.Null(rule.LegacyActivityTypeId);
    }

    [Fact]
    public void Id_match_ignores_casing()
    {
        var rule = new RecurringActivity { LegacyActivityTypeId = "ABC-123" };

        RecurringTitleMigration.Apply(new[] { rule }, Types);

        Assert.Equal("Sprachschule", rule.Title);
    }

    [Fact]
    public void Own_title_wins_over_the_category()
    {
        var rule = new RecurringActivity { Title = "Englisch B1", LegacyActivityTypeId = "abc-123" };

        RecurringTitleMigration.Apply(new[] { rule }, Types);

        Assert.Equal("Englisch B1", rule.Title);
        Assert.Null(rule.LegacyActivityTypeId);
    }

    [Fact]
    public void Deleted_category_leaves_the_title_empty_but_clears_the_link()
    {
        // Der Name war in diesem Fall schon vorher weg — die Verwaltungsliste zeigte eine leere Zelle.
        var rule = new RecurringActivity { LegacyActivityTypeId = "gibt-es-nicht" };

        Assert.True(RecurringTitleMigration.Apply(new[] { rule }, Types));

        Assert.Equal("", rule.Title);
        Assert.Null(rule.LegacyActivityTypeId);
    }

    [Fact]
    public void Already_migrated_data_is_left_alone()
    {
        var rule = new RecurringActivity { Title = "Fußball" };

        Assert.False(RecurringTitleMigration.Apply(new[] { rule }, Types));
    }
}

/// <summary>Das alte Dateiformat muss lesbar bleiben — und das neue darf das Feld nicht mehr schreiben.</summary>
public class RecurringLegacyJsonTests
{
    [Fact]
    public void Old_local_file_still_yields_the_category_id()
    {
        const string oldFile = """[{ "Id": "r1", "Title": "", "ActivityTypeId": "abc-123", "Weekdays": [4] }]""";

        var rules = JsonSerializer.Deserialize<List<RecurringActivity>>(oldFile, JsonOptions.Pretty)!;

        Assert.Equal("abc-123", Assert.Single(rules).LegacyActivityTypeId);
    }

    [Fact]
    public void Migrated_rule_is_written_without_the_category_field()
    {
        var rule = new RecurringActivity { Id = "r1", Title = "Sprachschule", LegacyActivityTypeId = null };

        var json = JsonSerializer.Serialize(new[] { rule }, JsonOptions.Pretty);

        Assert.DoesNotContain("ActivityTypeId", json);
        Assert.Contains("Sprachschule", json);
    }
}

/// <summary>Der Dialog verlangt eine Bezeichnung und speichert sie.</summary>
[Collection("Localizer")]
public class RecurringDialogFreeTextTests
{
    private static async Task<(RecurringActivityManagementViewModel Vm, InMemoryStorageService Storage)> VmAsync()
    {
        var storage = new InMemoryStorageService();
        await storage.SaveUsersAsync(new List<User>
        {
            new() { Id = "kid", Username = "tim", DisplayName = "Tim", Category = PersonCategory.Child }
        });
        var vm = new RecurringActivityManagementViewModel(storage);
        await Task.Yield();          // ReloadAsync im Konstruktor läuft gegen den Fake synchron durch
        vm.NewCommand.Execute(null);
        vm.Thu = true;
        return (vm, storage);
    }

    [Fact]
    public async Task Save_without_a_name_is_refused()
    {
        var (vm, storage) = await VmAsync();
        vm.Title = "   ";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(Localizer.Instance["Recur_ErrorNoName"], vm.ErrorMessage);
        Assert.Empty(await storage.LoadRecurringActivitiesAsync());
    }

    [Fact]
    public async Task Save_stores_the_free_text()
    {
        var (vm, storage) = await VmAsync();
        vm.Title = "  Fußball  ";

        await vm.SaveCommand.ExecuteAsync(null);

        var rule = Assert.Single(await storage.LoadRecurringActivitiesAsync());
        Assert.Equal("Fußball", rule.Title);
        Assert.Equal("", vm.ErrorMessage);
    }

    [Fact]
    public async Task Selecting_a_rule_shows_its_name()
    {
        var (vm, storage) = await VmAsync();
        vm.Title = "Musikschule";
        await vm.SaveCommand.ExecuteAsync(null);

        vm.NewCommand.Execute(null);
        Assert.Equal("", vm.Title);
        vm.SelectedActivity = vm.Activities.Single();

        Assert.Equal("Musikschule", vm.Title);
    }
}

/// <summary>Auf der Kachel und im PDF ist die Bezeichnung der Name.</summary>
public class TitledActivityDisplayTests
{
    private static CalendarEntry Recurring(string title) => new()
    {
        Id = "x", UserId = "kid", Type = EntryType.Activity, Title = title,
        DisplayType = EntryType.Activity, DisplayTitle = title, IsRecurring = true,
        StartTime = new TimeSpan(16, 0, 0), EndTime = new TimeSpan(17, 0, 0)
    };

    [Fact]
    public void Title_is_the_headline_instead_of_the_generic_type()
    {
        var e = Recurring("Fußball");

        Assert.True(e.IsTitledActivity);
        Assert.False(e.ShowsTypeLabel);    // kein „Aktivität" darüber
        Assert.False(e.ShowsSubtitle);     // und nicht doppelt darunter
    }

    [Fact]
    public void Activity_without_a_title_keeps_the_type_label()
    {
        var e = Recurring("");

        Assert.False(e.IsTitledActivity);
        Assert.True(e.ShowsTypeLabel);
    }

    [Fact]
    public void Work_with_a_title_keeps_type_and_subtitle()
    {
        var e = new CalendarEntry { Type = EntryType.Work, Title = "Frühdienst", DisplayType = EntryType.Work, DisplayTitle = "Frühdienst" };

        Assert.False(e.IsTitledActivity);
        Assert.True(e.ShowsTypeLabel);
        Assert.True(e.ShowsSubtitle);
    }

    [Fact]
    public void Pdf_label_is_the_title_not_type_plus_title()
    {
        var cell = PlanExportBuilder.CellEntry(Recurring("Fußball"), viewerIsAdmin: true, viewerId: "admin", EntryTypeInfo.Label);

        Assert.Equal("Fußball", cell.Label);
    }
}
