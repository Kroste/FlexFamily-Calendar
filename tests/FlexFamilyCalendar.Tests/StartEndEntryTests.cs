using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using FlexFamilyCalendar.Services.AI;
using FlexFamilyCalendar.Services.Api;
using FlexFamilyCalendar.ViewModels;
using Xunit;

namespace FlexFamilyCalendar.Tests;

/// <summary>
/// Start und Ende mit Datum und Uhrzeit (wie im Google-Kalender): wie die Eingaben gedeutet
/// werden. Der heikle Fall ist die Nachtschicht — sie bleibt ein Eintrag am Starttag.
/// </summary>
public class SpanResolveTests
{
    private static readonly DateOnly Wed = new(2026, 9, 23);
    private static TimeSpan H(int h) => TimeSpan.FromHours(h);

    [Fact]
    public void End_before_start_is_rejected()
        => Assert.Equal("Entry_ErrorEndBeforeStart", EntrySpans.Resolve(Wed, H(8), Wed.AddDays(-1), H(16), false, false).ErrorKey);

    [Fact]
    public void Ordinary_shift_stays_a_single_day_entry()
    {
        var plan = EntrySpans.Resolve(Wed, H(8), Wed, H(16), false, false).Plan!;
        Assert.False(plan.IsSpan);
        Assert.Equal(Wed, plan.To);
    }

    [Fact]
    public void Same_time_on_the_same_day_is_rejected()
        => Assert.Equal("Entry_ErrorSameTime", EntrySpans.Resolve(Wed, H(8), Wed, H(8), false, false).ErrorKey);

    [Theory]
    [InlineData(0)]   // 20:00–06:00 im selben Datum getippt, wie vor dem Umbau
    [InlineData(1)]   // Mi 20:00 → Do 06:00, wie im Google-Kalender
    public void Night_shift_stays_one_entry_on_its_start_day(int endOffset)
    {
        var plan = EntrySpans.Resolve(Wed, H(20), Wed.AddDays(endOffset), H(6), false, false).Plan!;

        Assert.False(plan.IsSpan);
        Assert.Equal(Wed, plan.From);
        Assert.Equal(Wed, plan.To);
    }

    [Fact]
    public void Multi_day_with_times_becomes_a_span()
    {
        var plan = EntrySpans.Resolve(Wed, H(14), Wed.AddDays(2), H(10), false, false).Plan!;

        Assert.True(plan.IsSpan);
        Assert.Equal(H(14), plan.StartTime);
        Assert.Equal(H(10), plan.EndTime);
    }

    [Fact]
    public void All_day_on_one_day_is_a_single_entry_without_times()
    {
        var plan = EntrySpans.Resolve(Wed, null, Wed, null, true, false).Plan!;

        Assert.False(plan.IsSpan);
        Assert.True(plan.AllDay);
        Assert.Null(plan.StartTime);
    }

    [Fact]
    public void Absence_is_always_a_span_even_timed_across_midnight()
    {
        // Abwesenheiten laufen immer über die Gruppe — die Nachtschicht-Ausnahme gilt nur für Einträge.
        var plan = EntrySpans.Resolve(Wed, H(20), Wed.AddDays(1), H(6), false, true).Plan!;

        Assert.True(plan.IsSpan);
        Assert.Equal(Wed.AddDays(1), plan.To);
    }

    [Fact]
    public void Missing_time_is_reported_unless_all_day()
    {
        Assert.Equal("Entry_ErrorNoStart", EntrySpans.Resolve(Wed, null, Wed, H(16), false, false).ErrorKey);
        Assert.Equal("Entry_ErrorNoEnd", EntrySpans.Resolve(Wed, H(8), Wed, null, false, false).ErrorKey);
    }
}

/// <summary>Ein Zeitraum auf den Tagen des Plans: jeder Tag trägt nur seinen Anteil.</summary>
[Collection("Localizer")]
public class SpanDayPortionTests
{
    private static readonly DateOnly Wed = new(2026, 9, 23);

    private static CalendarEntry Template(bool allDay = false) => new()
    {
        UserId = "u1", UserDisplayName = "Mara", Type = EntryType.Work, Title = "Messe",
        AllDay = allDay,
        SpanStartTime = allDay ? null : TimeSpan.FromHours(14),
        SpanEndTime = allDay ? null : TimeSpan.FromHours(10)
    };

    [Fact]
    public void Wednesday_to_friday_shows_from_all_day_until()
    {
        // Sprache nicht umschalten: das feuert in offene Headless-Fenster anderer Tests.
        var loc = Localizer.Instance;
        var days = EntrySpans.Build(Template(), Wed, Wed.AddDays(2), "g");

        Assert.Equal(new[]
        {
            string.Format(loc["Entry_FromTime"], "14:00"),     // „ab 14:00"
            loc["Entry_AllDayShort"],                           // „ganztägig"
            string.Format(loc["Entry_UntilTime"], "10:00")      // „bis 10:00"
        }, days.Select(d => d.Entry.TimeRange));
        // Stunden je Tagesanteil — so ist es vereinbart, bis das Stundenkonto umgebaut ist.
        Assert.Equal(new[] { 10.0, 24.0, 10.0 }, days.Select(d => d.Entry.DurationHours));
        Assert.All(days, d => Assert.False(d.Entry.IsShift));
    }

    [Fact]
    public void Span_ending_at_midnight_has_no_empty_last_tile()
    {
        var t = Template();
        t.SpanEndTime = TimeSpan.Zero;

        var days = EntrySpans.Build(t, Wed, Wed.AddDays(2), "g");

        Assert.Equal(new[] { Wed, Wed.AddDays(1) }, days.Select(d => d.Date));
    }

    [Fact]
    public void All_day_span_counts_nothing()
    {
        var days = EntrySpans.Build(Template(allDay: true), Wed, Wed.AddDays(1), "g");

        Assert.All(days, d =>
        {
            Assert.Equal(Localizer.Instance["Entry_AllDayShort"], d.Entry.TimeRange);
            Assert.Equal(0, d.Entry.DurationHours);
        });
    }

    [Fact]
    public void Work_time_rules_ignore_span_days()
    {
        // Der Mittelteil hätte 24 Stunden und zum Vortag null Ruhezeit — ein Verstoß, der keiner ist.
        var days = EntrySpans.Build(Template(), Wed, Wed.AddDays(2), "g");
        var summaries = days.Select(d => WorkTimeRules.Summarize(d.Date, new[] { d.Entry })).ToList();

        Assert.Empty(WorkTimeRules.OverDailyLimit(summaries, 10));
        Assert.Empty(WorkTimeRules.ShortRests(summaries, 11));
    }

    [Fact]
    public void Own_timed_absence_shows_its_time_masked_one_does_not()
    {
        var sick = new CalendarEntry { Type = EntryType.SickLeave, AllDay = false,
            StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(11) };
        Assert.True(sick.ShowsTime);

        sick.DisplayType = EntryType.Absence;   // für Kollegen maskiert
        Assert.False(sick.ShowsTime);

        var allDay = new CalendarEntry { Type = EntryType.Vacation };
        Assert.False(allDay.ShowsTime);
    }
}

/// <summary>Server-Übersetzung: ein Zeitraum ist dort EIN Eintrag mit den Eckzeiten.</summary>
public class SpanMappingTests
{
    private static readonly DateOnly Wed = new(2026, 9, 23);

    private static ServerEntryDto Dto(DateOnly? end, TimeOnly? start, TimeOnly? stop, string type = "Work")
        => new("srv1", "u1", type, Wed, end, start, stop, false, "Messe", null, "Approved", false);

    [Fact]
    public void Server_span_is_split_into_day_portions()
    {
        var dto = Dto(Wed.AddDays(2), new TimeOnly(14, 0), new TimeOnly(10, 0));

        var mid = EntryMapping.ToDesktop(dto, Wed.AddDays(1));
        var last = EntryMapping.ToDesktop(dto, Wed.AddDays(2));

        Assert.Equal("srv1", mid.AbsenceGroupId);
        Assert.Equal((TimeSpan.Zero, EntrySpans.EndOfDay), (mid.StartTime, mid.EndTime));
        Assert.Equal((TimeSpan.Zero, TimeSpan.FromHours(10)), (last.StartTime, last.EndTime));
        Assert.Equal(TimeSpan.FromHours(14), last.SpanStartTime);
    }

    [Fact]
    public void Span_ending_at_midnight_does_not_cover_its_last_day()
    {
        var dto = Dto(Wed.AddDays(2), new TimeOnly(14, 0), new TimeOnly(0, 0));

        Assert.True(EntryMapping.CoversDay(dto, Wed.AddDays(1)));
        Assert.False(EntryMapping.CoversDay(dto, Wed.AddDays(2)));
    }

    [Fact]
    public void Server_entry_without_times_is_all_day()
    {
        var e = EntryMapping.ToDesktop(Dto(null, null, null), Wed);

        Assert.True(e.IsAllDay);
        Assert.Equal(0, e.DurationHours);
    }

    [Fact]
    public void Any_day_of_a_span_sends_the_whole_span()
    {
        var dto = Dto(Wed.AddDays(2), new TimeOnly(14, 0), new TimeOnly(10, 0));
        var mid = EntryMapping.ToDesktop(dto, Wed.AddDays(1));

        var body = EntryMapping.ToCreateBody(mid, Wed.AddDays(1));

        Assert.Equal(Wed, body.Date);
        Assert.Equal(Wed.AddDays(2), body.EndDate);
        Assert.Equal(new TimeOnly(14, 0), body.StartTime);
        Assert.Equal(new TimeOnly(10, 0), body.EndTime);   // nicht 24:00 des Mitteltags
    }

    [Fact]
    public void All_day_single_entry_sends_no_times()
    {
        var free = new CalendarEntry { UserId = "u1", Type = EntryType.Work, Title = "Frei", AllDay = true };

        var body = EntryMapping.ToCreateBody(free, Wed);

        Assert.Null(body.StartTime);
        Assert.Null(body.EndTime);
        Assert.Null(body.EndDate);
        Assert.False(body.EndsNextDay);
    }
}

/// <summary>Der Dialog: Start, Ende, Ganztägig und das Mitwandern des Endes.</summary>
[Collection("Localizer")]
public class StartEndDialogTests
{
    private static readonly DateOnly Wed = new(2026, 9, 23);
    private static User Person() => new() { Id = "u1", Username = "mara", DisplayName = "Mara" };
    private static DateTimeOffset At(DateOnly d) => new(d.ToDateTime(TimeOnly.MinValue));
    private static TimeSpan H(int h) => TimeSpan.FromHours(h);

    private static EntryDialogResult? Save(EntryEditorViewModel vm)
    {
        EntryDialogResult? result = null;
        vm.Closed += r => result = r;
        vm.SaveCommand.Execute(null);
        return result;
    }

    [Fact]
    public void New_entry_starts_and_ends_on_the_clicked_day_with_times()
    {
        var vm = new EntryEditorViewModel(Wed, new[] { Person() });

        Assert.Equal(At(Wed), vm.StartDate);
        Assert.Equal(At(Wed), vm.EndDate);
        Assert.False(vm.IsAllDay);
        Assert.True(vm.ShowTimes);
    }

    [Fact]
    public void Absence_starts_all_day_and_switching_back_brings_the_times()
    {
        var vm = new EntryEditorViewModel(Wed, new[] { Person() }) { IsAbsenceMode = true };
        Assert.True(vm.IsAllDay);
        Assert.False(vm.ShowTimes);

        vm.IsEntryMode = true;
        Assert.False(vm.IsAllDay);
    }

    [Fact]
    public void Moving_the_start_date_moves_the_end_and_keeps_the_length()
    {
        var vm = new EntryEditorViewModel(Wed, new[] { Person() })
        {
            StartTime = H(14), EndTime = H(10)
        };
        vm.EndDate = At(Wed.AddDays(2));

        vm.StartDate = At(Wed.AddDays(7));

        Assert.Equal(At(Wed.AddDays(9)), vm.EndDate);
        Assert.Equal(H(10), vm.EndTime);
    }

    [Fact]
    public void Moving_the_start_time_moves_the_end_time()
    {
        var vm = new EntryEditorViewModel(Wed, new[] { Person() }) { StartTime = H(8), EndTime = H(16) };

        vm.StartTime = H(9);

        Assert.Equal(H(17), vm.EndTime);
        Assert.Equal(At(Wed), vm.EndDate);
    }

    [Fact]
    public void Moving_a_night_shift_keeps_it_overnight()
    {
        // 20:00–06:00 im selben Datum getippt: der Start wandert, das Ende bleibt „am Folgetag".
        var vm = new EntryEditorViewModel(Wed, new[] { Person() }) { StartTime = H(20), EndTime = H(6) };

        vm.StartTime = H(21);

        Assert.Equal(H(7), vm.EndTime);
        Assert.Equal(At(Wed), vm.EndDate);
    }

    [Fact]
    public void Night_shift_is_saved_on_its_start_day()
    {
        var vm = new EntryEditorViewModel(Wed, new[] { Person() }) { Title = "Nacht", StartTime = H(20), EndTime = H(6) };
        vm.EndDate = At(Wed.AddDays(1));

        var result = Save(vm)!;

        Assert.False(result.IsSpan);
        Assert.Equal(Wed, result.RangeStart);
        Assert.Equal(10, result.Entry.DurationHours);
    }

    [Fact]
    public void Editing_a_night_shift_shows_the_next_day_as_end()
    {
        var existing = new CalendarEntry { Id = "n", UserId = "u1", Type = EntryType.Work, Title = "Nacht",
            StartTime = H(20), EndTime = H(6) };

        var vm = new EntryEditorViewModel(Wed, new[] { Person() }, existing);

        Assert.Equal(At(Wed.AddDays(1)), vm.EndDate);
    }

    [Fact]
    public void Editing_from_the_middle_day_shows_the_whole_span()
    {
        var mid = EntrySpans.Build(new CalendarEntry
        {
            UserId = "u1", Type = EntryType.Work, Title = "Messe", AllDay = false,
            SpanStartTime = H(14), SpanEndTime = H(10)
        }, Wed, Wed.AddDays(2), "g")[1].Entry;

        var vm = new EntryEditorViewModel(Wed, new[] { Person() }, mid);

        Assert.Equal(At(Wed), vm.StartDate);
        Assert.Equal(At(Wed.AddDays(2)), vm.EndDate);
        Assert.Equal(H(14), vm.StartTime);
        Assert.Equal(H(10), vm.EndTime);   // nicht 24:00 des Mitteltags
    }

    [Fact]
    public void Free_as_a_whole_day()
    {
        var vm = new EntryEditorViewModel(Wed, new[] { Person() }) { Title = "Frei", IsAllDay = true };

        var result = Save(vm)!;

        Assert.False(result.IsSpan);
        Assert.True(result.Entry.IsAllDay);
        Assert.Equal(EntryType.Work, result.Entry.Type);
    }

    [Fact]
    public void End_before_start_shows_an_error()
    {
        var vm = new EntryEditorViewModel(Wed, new[] { Person() }) { Title = "X", StartTime = H(8), EndTime = H(9) };
        vm.EndDate = At(Wed.AddDays(-1));

        Assert.Null(Save(vm));
        Assert.Equal(Localizer.Instance["Entry_ErrorEndBeforeStart"], vm.ErrorMessage);
    }
}

/// <summary>Speichern über den Kalender: Zeitraum anlegen, ändern, zurück zum Einzeleintrag.</summary>
[Collection("Localizer")]
public class SpanStorageTests
{
    private static readonly User Admin = new()
    { Id = "admin", Username = "lars", DisplayName = "Lars", Role = UserRole.Admin, Category = PersonCategory.Parent };

    private static async Task<(CalendarViewModel Vm, InMemoryStorageService Storage)> SetupAsync()
    {
        var storage = new InMemoryStorageService();
        await storage.SaveUsersAsync(new List<User> { Admin });
        var vm = new CalendarViewModel(storage, Admin, new NotificationService(storage),
            new AiService(Array.Empty<IAiProvider>()), new LocalMailSender(storage));
        await vm.RefreshAllAsync(silent: true);
        return (vm, storage);
    }

    private static DateOnly ThisWednesday()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return today.AddDays(-(((int)today.DayOfWeek + 6) % 7)).AddDays(2);
    }

    private static EntryDialogResult Run(EntryEditorViewModel vm)
    {
        EntryDialogResult? result = null;
        vm.Closed += r => result = r;
        vm.SaveCommand.Execute(null);
        return result!;
    }

    [Fact]
    public async Task Span_lands_on_every_day_and_can_be_shortened_to_one_day()
    {
        var (cal, storage) = await SetupAsync();
        var wed = ThisWednesday();

        var create = new EntryEditorViewModel(wed, new[] { Admin })
        { Title = "Messe", StartTime = TimeSpan.FromHours(14), EndTime = TimeSpan.FromHours(10) };
        create.EndDate = new DateTimeOffset(wed.AddDays(2).ToDateTime(TimeOnly.MinValue));
        await cal.ApplyEntryResultAsync(wed, Run(create));

        for (var d = wed; d <= wed.AddDays(2); d = d.AddDays(1))
            Assert.Single((await storage.LoadDayAsync(d)).Entries);

        // Vom Donnerstag aus bearbeiten, auf einen Tag kürzen.
        var thursday = (await storage.LoadDayAsync(wed.AddDays(1))).Entries.Single();
        var edit = new EntryEditorViewModel(wed, new[] { Admin }, thursday);
        edit.EndDate = edit.StartDate;
        edit.EndTime = TimeSpan.FromHours(18);
        await cal.ApplyEntryResultAsync(wed, Run(edit));

        var only = (await storage.LoadDayAsync(wed)).Entries.Single();
        Assert.Null(only.AbsenceGroupId);
        Assert.Equal(TimeSpan.FromHours(18), only.EndTime);
        Assert.Empty((await storage.LoadDayAsync(wed.AddDays(1))).Entries);
        Assert.Empty((await storage.LoadDayAsync(wed.AddDays(2))).Entries);
    }

    [Fact]
    public async Task Moving_the_start_date_moves_a_single_entry()
    {
        var (cal, storage) = await SetupAsync();
        var wed = ThisWednesday();

        var create = new EntryEditorViewModel(wed, new[] { Admin })
        { Title = "Remise", StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(12) };
        await cal.ApplyEntryResultAsync(wed, Run(create));
        var saved = (await storage.LoadDayAsync(wed)).Entries.Single();

        var edit = new EntryEditorViewModel(wed, new[] { Admin }, saved);
        edit.StartDate = new DateTimeOffset(wed.AddDays(1).ToDateTime(TimeOnly.MinValue));
        await cal.ApplyEntryResultAsync(wed, Run(edit));

        Assert.Empty((await storage.LoadDayAsync(wed)).Entries);
        Assert.Equal(saved.Id, (await storage.LoadDayAsync(wed.AddDays(1))).Entries.Single().Id);
    }
}
