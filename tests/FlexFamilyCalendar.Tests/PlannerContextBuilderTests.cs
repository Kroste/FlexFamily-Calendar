using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services.AI;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class PlannerContextBuilderTests
{
    private static User Person(string id, string name, double soll = 40, double ruhe = 11) => new()
    {
        Id = id, DisplayName = name, Username = name.ToLowerInvariant(),
        Category = PersonCategory.Employee,
        WeeklyHoursQuota = soll, MinRestHours = ruhe, Color = "#3498DB"
    };

    private static PlannerContext Make(
        IReadOnlyList<User>? users = null,
        IReadOnlyList<RecurringActivity>? rules = null,
        IReadOnlyList<(DateOnly, IReadOnlyList<CalendarEntry>)>? week = null,
        IReadOnlyList<PlannerNote>? notes = null)
        => new(
            Today: new DateOnly(2026, 6, 1),
            WeekStart: new DateOnly(2026, 6, 1),
            Users: users ?? Array.Empty<User>(),
            ActivityTypes: Array.Empty<ActivityType>(),
            RecurringActivities: rules ?? Array.Empty<RecurringActivity>(),
            Week: week ?? Array.Empty<(DateOnly, IReadOnlyList<CalendarEntry>)>(),
            Notes: notes ?? Array.Empty<PlannerNote>());

    [Fact]
    public void Render_Person_Includes_Soll_Und_Ruhezeit()
    {
        var text = PlannerContextBuilder.Render(Make(users: new[] { Person("u1", "Lars", soll: 40, ruhe: 11) }));
        Assert.Contains("Lars", text);
        Assert.Contains("40", text);
        Assert.Contains("11", text);
    }

    [Fact]
    public void Render_RecurringRule_Lists_Days_And_Pauses()
    {
        var rule = new RecurringActivity
        {
            UserId = "u1", UserDisplayName = "Lars", Title = "Fußball",
            StartTime = TimeSpan.FromHours(16), EndTime = TimeSpan.FromHours(17),
            Weekdays = new() { DayOfWeek.Monday, DayOfWeek.Wednesday },
            Skips = { new RecurrenceSkip { From = new(2026, 7, 1), To = new(2026, 7, 14), Reason = "Urlaub" } }
        };
        var text = PlannerContextBuilder.Render(Make(
            users: new[] { Person("u1", "Lars") },
            rules: new[] { rule }));

        Assert.Contains("Fußball", text);
        Assert.Contains("16:00", text);
        Assert.Contains("Pause: 01.07.2026–14.07.2026 (Urlaub)", text);
    }

    [Fact]
    public void Render_WithNotes_AppendsNotesSection()
    {
        var notes = new[] {
            new PlannerNote { Text = "Mia hat im Mai Klassenfahrt" },
            new PlannerNote { Text = "Pausch arbeitet montags von zuhause" }
        };
        var text = PlannerContextBuilder.Render(Make(notes: notes));

        Assert.Contains("hinterlegte Hinweise", text);
        Assert.Contains("Klassenfahrt", text);
        Assert.Contains("zuhause", text);
    }

    [Fact]
    public void Render_EmptySections_AreOmitted()
    {
        var text = PlannerContextBuilder.Render(Make());
        Assert.DoesNotContain("## Wiederkehrende Aktivitäten", text);
        Assert.DoesNotContain("## Aktivitäts-Kategorien", text);
        Assert.DoesNotContain("hinterlegte Hinweise", text);
    }

    [Fact]
    public void Render_AlwaysStartsWithRoleAndDate()
    {
        var text = PlannerContextBuilder.Render(Make());
        Assert.Contains("Planungs-Assistenz", text);
        Assert.Contains("01.06.2026", text);
    }
}
