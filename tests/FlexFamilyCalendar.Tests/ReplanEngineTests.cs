using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class ReplanEngineTests
{
    private static readonly DateOnly Mon = new(2026, 6, 1);

    private static CalendarEntry Work(string userId, int startH, int endH) => new()
    {
        UserId = userId,
        Type = EntryType.Work,
        StartTime = TimeSpan.FromHours(startH),
        EndTime = TimeSpan.FromHours(endH)
    };

    private static User Person(string id, PersonCategory cat = PersonCategory.Employee,
        double maxDaily = 0, double maxWeekly = 0, double quota = 40) => new()
    {
        Id = id, Username = id, DisplayName = id, Category = cat,
        MaxDailyHours = maxDaily, MaxWeeklyHours = maxWeekly, WeeklyHoursQuota = quota
    };

    private static IReadOnlyList<(DateOnly, IReadOnlyList<CalendarEntry>)> Week(DateOnly date, params CalendarEntry[] dayEntries)
        => new (DateOnly, IReadOnlyList<CalendarEntry>)[] { (date, dayEntries.ToList()) };

    [Fact]
    public void ExcludesSickPersonAndNonWorkers()
    {
        var shift = Work("sick", 8, 12);
        var users = new[]
        {
            Person("sick"),
            Person("parent", PersonCategory.Parent),
            Person("child", PersonCategory.Child),
            Person("colleague")
        };
        var result = ReplanEngine.FindCandidates(shift, Mon, users, "sick", Week(Mon));

        Assert.Single(result);
        Assert.Equal("colleague", result[0].User.Id);
    }

    [Fact]
    public void ExcludesCandidateWithTimeConflict()
    {
        var shift = Work("sick", 8, 12);
        var users = new[] { Person("busy"), Person("free") };
        var week = Week(Mon, Work("busy", 10, 14));   // busy arbeitet 10–14 → Konflikt mit 8–12
        var result = ReplanEngine.FindCandidates(shift, Mon, users, "sick", week);

        Assert.Single(result);
        Assert.Equal("free", result[0].User.Id);
    }

    [Fact]
    public void AdjacentShift_NoConflict()
    {
        var shift = Work("sick", 8, 12);
        var users = new[] { Person("adj") };
        var week = Week(Mon, Work("adj", 12, 16));   // grenzt nur an
        Assert.Single(ReplanEngine.FindCandidates(shift, Mon, users, "sick", week));
    }

    [Fact]
    public void ExcludesOverDailyLimit()
    {
        var shift = Work("sick", 14, 18);   // 4h
        var users = new[] { Person("c", maxDaily: 8) };
        var week = Week(Mon, Work("c", 6, 12));   // schon 6h → 6+4=10 > 8
        Assert.Empty(ReplanEngine.FindCandidates(shift, Mon, users, "sick", week));
    }

    [Fact]
    public void ExcludesOverWeeklyLimit()
    {
        var shift = Work("sick", 8, 12);   // 4h
        var users = new[] { Person("c", maxWeekly: 10) };
        // c arbeitet Mo 8h woanders → diese Woche 8h; 8+4=12 > 10
        var week = new (DateOnly, IReadOnlyList<CalendarEntry>)[]
        {
            (Mon, new List<CalendarEntry>()),
            (Mon.AddDays(1), new List<CalendarEntry> { Work("c", 8, 16) })
        };
        Assert.Empty(ReplanEngine.FindCandidates(shift, Mon, users, "sick", week));
    }

    [Fact]
    public void ZeroLimit_MeansNoLimit()
    {
        var shift = Work("sick", 8, 20);   // 12h
        var users = new[] { Person("c", maxDaily: 0, maxWeekly: 0) };
        Assert.Single(ReplanEngine.FindCandidates(shift, Mon, users, "sick", Week(Mon)));
    }

    [Fact]
    public void RanksLeastBusyFirst()
    {
        var shift = Work("sick", 8, 12);
        var users = new[] { Person("a"), Person("b") };
        var week = new (DateOnly, IReadOnlyList<CalendarEntry>)[]
        {
            (Mon, new List<CalendarEntry>()),
            (Mon.AddDays(1), new List<CalendarEntry> { Work("a", 8, 16) })   // a: 8h, b: 0h
        };
        var result = ReplanEngine.FindCandidates(shift, Mon, users, "sick", week);
        Assert.Equal("b", result[0].User.Id);   // weniger ausgelastet zuerst
        Assert.Equal("a", result[1].User.Id);
    }

    [Fact]
    public void BuildPrompt_HasNoRealNames()
    {
        var shift = Work("sick", 8, 12);
        var candidates = new[]
        {
            new ReplanEngine.ReplanCandidate(Person("Anna"), 10, 40),
            new ReplanEngine.ReplanCandidate(Person("Ben"), 20, 40)
        };
        var prompt = ReplanEngine.BuildPrompt(shift, Mon, candidates);

        Assert.DoesNotContain("Anna", prompt);
        Assert.DoesNotContain("Ben", prompt);
        Assert.Contains("A:", prompt);
        Assert.Contains("B:", prompt);
    }
}
