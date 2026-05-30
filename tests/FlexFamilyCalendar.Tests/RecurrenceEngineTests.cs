using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class RecurrenceEngineTests
{
    private static RecurringActivity Football(params DayOfWeek[] days) => new()
    {
        UserId = "u1",
        UserDisplayName = "Kind",
        Title = "Fußball",
        StartTime = TimeSpan.FromHours(16),
        EndTime = TimeSpan.FromHours(17),
        Weekdays = days.ToList(),
        SkipOnHolidays = false
    };

    [Fact]
    public void Project_IncludesRule_OnMatchingWeekday()
    {
        var date = new DateOnly(2026, 5, 28);
        var rule = Football(date.DayOfWeek);

        var result = RecurrenceEngine.Project(new[] { rule }, date, isHoliday: false);

        var entry = Assert.Single(result);
        Assert.True(entry.IsRecurring);
        Assert.Equal(EntryType.Activity, entry.Type);
        Assert.Equal("Fußball", entry.Title);
        Assert.Equal(TimeSpan.FromHours(16), entry.StartTime);
    }

    [Fact]
    public void Project_Excludes_OnNonMatchingWeekday()
    {
        var date = new DateOnly(2026, 5, 28);
        var rule = Football(date.AddDays(1).DayOfWeek);   // anderer Wochentag

        Assert.Empty(RecurrenceEngine.Project(new[] { rule }, date, isHoliday: false));
    }

    [Fact]
    public void Project_Daily_OccursEveryDayOfWeek()
    {
        var rule = Football(Enum.GetValues<DayOfWeek>());
        var monday = new DateOnly(2026, 5, 25);

        for (int i = 0; i < 7; i++)
            Assert.Single(RecurrenceEngine.Project(new[] { rule }, monday.AddDays(i), isHoliday: false));
    }

    [Fact]
    public void Project_EmptyWeekdays_NeverOccurs()
    {
        var date = new DateOnly(2026, 5, 28);
        Assert.Empty(RecurrenceEngine.Project(new[] { Football() }, date, isHoliday: false));
    }

    [Fact]
    public void Project_SkipOnHoliday_HidesOccurrence()
    {
        var date = new DateOnly(2026, 5, 28);
        var rule = Football(date.DayOfWeek);
        rule.SkipOnHolidays = true;

        Assert.Empty(RecurrenceEngine.Project(new[] { rule }, date, isHoliday: true));
        Assert.Single(RecurrenceEngine.Project(new[] { rule }, date, isHoliday: false));
    }

    [Fact]
    public void Project_NotSkipping_OnHoliday_FlagsConflict()
    {
        var date = new DateOnly(2026, 5, 28);
        var rule = Football(date.DayOfWeek);   // SkipOnHolidays = false

        var entry = Assert.Single(RecurrenceEngine.Project(new[] { rule }, date, isHoliday: true));
        Assert.True(entry.HolidayConflict);
    }

    [Fact]
    public void Project_NoHoliday_NoConflict()
    {
        var date = new DateOnly(2026, 5, 28);
        var entry = Assert.Single(RecurrenceEngine.Project(new[] { Football(date.DayOfWeek) }, date, isHoliday: false));
        Assert.False(entry.HolidayConflict);
    }

    [Fact]
    public void Project_SortsByStartTime()
    {
        var date = new DateOnly(2026, 5, 28);
        var late = Football(date.DayOfWeek);
        late.StartTime = TimeSpan.FromHours(19);
        late.Title = "Spät";
        var early = Football(date.DayOfWeek);
        early.StartTime = TimeSpan.FromHours(8);
        early.Title = "Früh";

        var result = RecurrenceEngine.Project(new[] { late, early }, date, isHoliday: false);

        Assert.Equal("Früh", result[0].Title);
        Assert.Equal("Spät", result[1].Title);
    }

    [Fact]
    public void ProjectedId_IsStable_PerRuleAndDate()
    {
        var date = new DateOnly(2026, 5, 28);
        var rule = Football(date.DayOfWeek);

        var a = RecurrenceEngine.Project(new[] { rule }, date, isHoliday: false)[0];
        var b = RecurrenceEngine.Project(new[] { rule }, date, isHoliday: false)[0];

        Assert.Equal(a.Id, b.Id);
        Assert.Contains(rule.Id, a.Id);
    }

    [Fact]
    public void Project_PauseRange_MarksAsPaused_ButKeepsEntry()
    {
        var date = new DateOnly(2026, 5, 28);
        var rule = Football(date.DayOfWeek);
        rule.Skips.Add(new RecurrenceSkip { From = new(2026, 5, 25), To = new(2026, 5, 31), Reason = "Urlaub" });

        var entry = Assert.Single(RecurrenceEngine.Project(new[] { rule }, date, isHoliday: false));
        Assert.True(entry.IsPaused);
    }

    [Fact]
    public void Project_OutsidePauseRange_NotPaused()
    {
        var date = new DateOnly(2026, 6, 4);     // nach dem Pausen-Ende
        var rule = Football(date.DayOfWeek);
        rule.Skips.Add(new RecurrenceSkip { From = new(2026, 5, 25), To = new(2026, 5, 31) });

        var entry = Assert.Single(RecurrenceEngine.Project(new[] { rule }, date, isHoliday: false));
        Assert.False(entry.IsPaused);
    }

    [Fact]
    public void Project_PauseRange_InclusiveOnBothEnds()
    {
        var rule = Football(DayOfWeek.Monday);
        rule.Skips.Add(new RecurrenceSkip { From = new(2026, 5, 25), To = new(2026, 5, 25) });

        var entry = Assert.Single(RecurrenceEngine.Project(new[] { rule }, new DateOnly(2026, 5, 25), isHoliday: false));
        Assert.True(entry.IsPaused);
    }

    [Fact]
    public void Project_MultiDayRange_PausesAllDaysWithinRange()
    {
        var rule = Football(DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday);
        rule.Skips.Add(new RecurrenceSkip { From = new(2026, 6, 1), To = new(2026, 6, 5) });

        for (int i = 0; i < 5; i++)
        {
            var date = new DateOnly(2026, 6, 1).AddDays(i);
            var entry = Assert.Single(RecurrenceEngine.Project(new[] { rule }, date, false));
            Assert.True(entry.IsPaused, $"Pause sollte auf {date:yyyy-MM-dd} greifen");
        }
    }

    [Fact]
    public void Project_MultipleSkips_EachRangeChecked()
    {
        var rule = Football(DayOfWeek.Monday);
        rule.Skips.Add(new RecurrenceSkip { From = new(2026, 5, 4), To = new(2026, 5, 4) });
        rule.Skips.Add(new RecurrenceSkip { From = new(2026, 6, 1), To = new(2026, 6, 1) });

        Assert.True (RecurrenceEngine.Project(new[] { rule }, new DateOnly(2026, 5, 4), false)[0].IsPaused);
        Assert.False(RecurrenceEngine.Project(new[] { rule }, new DateOnly(2026, 5, 11), false)[0].IsPaused);
        Assert.True (RecurrenceEngine.Project(new[] { rule }, new DateOnly(2026, 6, 1), false)[0].IsPaused);
    }

    [Fact]
    public void RecurrenceSkip_Contains_BothEndsInclusive()
    {
        var s = new RecurrenceSkip { From = new(2026, 1, 10), To = new(2026, 1, 15) };
        Assert.False(s.Contains(new(2026, 1, 9)));
        Assert.True (s.Contains(new(2026, 1, 10)));
        Assert.True (s.Contains(new(2026, 1, 13)));
        Assert.True (s.Contains(new(2026, 1, 15)));
        Assert.False(s.Contains(new(2026, 1, 16)));
    }

    [Fact]
    public async Task Storage_Roundtrip_PreservesRule()
    {
        var storage = new InMemoryStorageService();
        var rule = Football(DayOfWeek.Thursday, DayOfWeek.Tuesday);
        rule.SkipOnHolidays = true;
        rule.ActivityTypeId = "act-1";

        await storage.SaveRecurringActivitiesAsync(new List<RecurringActivity> { rule });
        var loaded = Assert.Single(await storage.LoadRecurringActivitiesAsync());

        Assert.Equal(rule.Id, loaded.Id);
        Assert.Equal("Fußball", loaded.Title);
        Assert.Equal("act-1", loaded.ActivityTypeId);
        Assert.True(loaded.SkipOnHolidays);
        Assert.Equal(new[] { DayOfWeek.Thursday, DayOfWeek.Tuesday }, loaded.Weekdays);
    }

    [Fact]
    public void PauseDialog_Save_AutoAdds_PendingDateRange()
    {
        // UX: User wählt Datum, klickt direkt „Speichern" → die ungeschickte Pause muss
        // trotzdem in die Liste übernommen werden, statt lautlos verloren zu gehen.
        var rule = Football(DayOfWeek.Thursday);
        var clicked = new DateOnly(2026, 5, 28);
        var vm = new ViewModels.RecurrencePauseViewModel(rule, clicked);

        IReadOnlyList<RecurrenceSkip>? saved = null;
        vm.Closed += s => saved = s;
        vm.SaveCommand.Execute(null);

        Assert.NotNull(saved);
        var skip = Assert.Single(saved!);
        Assert.Equal(clicked, skip.From);
        Assert.Equal(clicked, skip.To);
    }

    [Fact]
    public void PauseDialog_Save_DoesNotDuplicate_AlreadyAddedRange()
    {
        var rule = Football(DayOfWeek.Thursday);
        var clicked = new DateOnly(2026, 5, 28);
        rule.Skips.Add(new RecurrenceSkip { From = clicked, To = clicked, Reason = "alt" });
        var vm = new ViewModels.RecurrencePauseViewModel(rule, clicked);

        IReadOnlyList<RecurrenceSkip>? saved = null;
        vm.Closed += s => saved = s;
        vm.SaveCommand.Execute(null);

        Assert.NotNull(saved);
        Assert.Single(saved!);   // kein Duplikat trotz gleicher Vorbelegung
    }

    [Fact]
    public void Json_Roundtrip_PreservesSkips()
    {
        var rule = Football(DayOfWeek.Monday);
        rule.Skips.Add(new RecurrenceSkip { From = new(2026, 7, 1), To = new(2026, 7, 14), Reason = "Urlaub" });
        rule.Skips.Add(new RecurrenceSkip { From = new(2026, 12, 22), To = new(2027, 1, 5), Reason = "Weihnachten" });

        var json = Newtonsoft.Json.JsonConvert.SerializeObject(rule);
        var loaded = Newtonsoft.Json.JsonConvert.DeserializeObject<RecurringActivity>(json)!;

        Assert.Equal(2, loaded.Skips.Count);
        Assert.Equal(new DateOnly(2026, 7, 1), loaded.Skips[0].From);
        Assert.Equal(new DateOnly(2026, 7, 14), loaded.Skips[0].To);
        Assert.Equal("Urlaub", loaded.Skips[0].Reason);
        Assert.Equal(new DateOnly(2027, 1, 5), loaded.Skips[1].To);
    }
}
