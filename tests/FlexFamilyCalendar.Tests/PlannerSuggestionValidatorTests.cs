using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services.AI;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class PlannerSuggestionValidatorTests
{
    private static User Person(string id, double rest = 11) =>
        new() { Id = id, DisplayName = id.ToUpperInvariant(), Username = id, MinRestHours = rest };

    private static CalendarEntry Entry(string userId, EntryType type, string start, string end, string? id = null)
        => new()
        {
            Id = id ?? Guid.NewGuid().ToString(),
            UserId = userId, Type = type,
            StartTime = TimeSpan.Parse(start), EndTime = TimeSpan.Parse(end)
        };

    private static IReadOnlyList<(DateOnly, IReadOnlyList<CalendarEntry>)> Week(params (DateOnly, CalendarEntry[])[] days)
        => days.Select(d => (d.Item1, (IReadOnlyList<CalendarEntry>)d.Item2.ToList())).ToList();

    [Fact]
    public void SelfOverlap_DetectedForSamePerson()
    {
        var day = new DateOnly(2026, 6, 1);
        var users = new[] { Person("u1") };
        var existing = Entry("u1", EntryType.Work, "08:00", "16:00");
        var week = Week((day, new[] { existing }));
        var s = new PlannerSuggestion(SuggestionAction.Add, day, null, "u1", EntryType.Work,
            TimeSpan.FromHours(14), TimeSpan.FromHours(22), null);

        var w = PlannerSuggestionValidator.Validate(s, users, week);
        Assert.Contains(w, x => x.Kind == SuggestionWarningKind.SelfOverlap);
    }

    [Fact]
    public void OverlapBetweenDifferentPeople_IsNotAConflict()
    {
        var day = new DateOnly(2026, 6, 1);
        var users = new[] { Person("u1"), Person("u2") };
        var existing = Entry("u2", EntryType.Work, "08:00", "16:00");
        var week = Week((day, new[] { existing }));
        var s = new PlannerSuggestion(SuggestionAction.Add, day, null, "u1", EntryType.Work,
            TimeSpan.FromHours(8), TimeSpan.FromHours(16), null);

        var w = PlannerSuggestionValidator.Validate(s, users, week);
        Assert.DoesNotContain(w, x => x.Kind == SuggestionWarningKind.SelfOverlap);
    }

    [Fact]
    public void RestHoursViolation_AgainstPreviousDay()
    {
        var prev = new DateOnly(2026, 5, 31);
        var day = new DateOnly(2026, 6, 1);
        var users = new[] { Person("u1", rest: 11) };
        var prevShift = Entry("u1", EntryType.Work, "14:00", "22:00");   // bis 22 Uhr
        var week = Week((prev, new[] { prevShift }), (day, Array.Empty<CalendarEntry>()));
        // Start nur 8h später (06:00) — Pause = 8h, weniger als 11h
        var s = new PlannerSuggestion(SuggestionAction.Add, day, null, "u1", EntryType.Work,
            TimeSpan.FromHours(6), TimeSpan.FromHours(14), null);

        var w = PlannerSuggestionValidator.Validate(s, users, week);
        Assert.Contains(w, x => x.Kind == SuggestionWarningKind.RestHoursViolation);
    }

    [Fact]
    public void RestHoursViolation_AgainstNextDay()
    {
        var day = new DateOnly(2026, 6, 1);
        var next = new DateOnly(2026, 6, 2);
        var users = new[] { Person("u1", rest: 11) };
        var nextShift = Entry("u1", EntryType.Work, "06:00", "14:00");
        var week = Week((day, Array.Empty<CalendarEntry>()), (next, new[] { nextShift }));
        // Ende 23:00 → Folgetag 06:00 = 7h Pause
        var s = new PlannerSuggestion(SuggestionAction.Add, day, null, "u1", EntryType.Work,
            TimeSpan.FromHours(15), TimeSpan.FromHours(23), null);

        var w = PlannerSuggestionValidator.Validate(s, users, week);
        Assert.Contains(w, x => x.Kind == SuggestionWarningKind.RestHoursViolation);
    }

    [Fact]
    public void RestHoursOk_NoWarning()
    {
        var prev = new DateOnly(2026, 5, 31);
        var day = new DateOnly(2026, 6, 1);
        var users = new[] { Person("u1", rest: 11) };
        var prevShift = Entry("u1", EntryType.Work, "06:00", "14:00");
        var week = Week((prev, new[] { prevShift }), (day, Array.Empty<CalendarEntry>()));
        var s = new PlannerSuggestion(SuggestionAction.Add, day, null, "u1", EntryType.Work,
            TimeSpan.FromHours(6), TimeSpan.FromHours(14), null);

        var w = PlannerSuggestionValidator.Validate(s, users, week);
        Assert.DoesNotContain(w, x => x.Kind == SuggestionWarningKind.RestHoursViolation);
    }

    [Fact]
    public void PersonAbsent_DetectedOnSameDay()
    {
        var day = new DateOnly(2026, 6, 1);
        var users = new[] { Person("u1") };
        var vacation = Entry("u1", EntryType.Vacation, "00:00", "00:00");
        var week = Week((day, new[] { vacation }));
        var s = new PlannerSuggestion(SuggestionAction.Add, day, null, "u1", EntryType.Work,
            TimeSpan.FromHours(6), TimeSpan.FromHours(14), null);

        var w = PlannerSuggestionValidator.Validate(s, users, week);
        Assert.Contains(w, x => x.Kind == SuggestionWarningKind.PersonAbsent);
    }

    [Fact]
    public void Delete_IsAlwaysAccepted_WithoutWarnings()
    {
        var day = new DateOnly(2026, 6, 1);
        var users = new[] { Person("u1") };
        var existing = Entry("u1", EntryType.Work, "06:00", "14:00", id: "e1");
        var week = Week((day, new[] { existing }));
        var s = new PlannerSuggestion(SuggestionAction.Delete, day, "e1", null, null, null, null, null);

        Assert.Empty(PlannerSuggestionValidator.Validate(s, users, week));
    }

    [Fact]
    public void Update_IgnoresTheExistingEntryWhenCheckingOverlap()
    {
        // Update der eigenen Schicht (Zeit ändern) darf nicht gegen sie selbst kollidieren.
        var day = new DateOnly(2026, 6, 1);
        var users = new[] { Person("u1") };
        var existing = Entry("u1", EntryType.Work, "06:00", "14:00", id: "e1");
        var week = Week((day, new[] { existing }));
        var s = new PlannerSuggestion(SuggestionAction.Update, day, "e1", null, null,
            TimeSpan.FromHours(7), TimeSpan.FromHours(15), null);

        var w = PlannerSuggestionValidator.Validate(s, users, week);
        Assert.DoesNotContain(w, x => x.Kind == SuggestionWarningKind.SelfOverlap);
    }
}
