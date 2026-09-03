using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.ViewModels.Mobile;
using Xunit;

namespace FlexFamilyCalendar.Tests;

/// <summary>Krank-/Urlaubsmeldung aus dem Android-Head.</summary>
public class MobileAbsenceTests
{
    private static readonly DateOnly Monday = new(2026, 5, 25);

    private static User Self() => new()
    { Id = "emp", Username = "mara", DisplayName = "Mara", Category = PersonCategory.Employee };

    private static MobileAbsenceViewModel Vm(InMemoryStorageService storage, EntryType type)
    {
        var vm = new MobileAbsenceViewModel(storage, Self(), type);
        vm.From = new DateTimeOffset(Monday.ToDateTime(TimeOnly.MinValue));
        vm.To = new DateTimeOffset(Monday.AddDays(2).ToDateTime(TimeOnly.MinValue));
        return vm;
    }

    private static async Task<List<CalendarEntry>> EntriesAsync(InMemoryStorageService storage, DateOnly date)
        => (await storage.LoadDayAsync(date)).Entries;

    [Fact]
    public async Task Save_WritesOneEntryPerDayOfTheRange()
    {
        var storage = new InMemoryStorageService();
        var vm = Vm(storage, EntryType.SickLeave);

        await vm.SaveCommand.ExecuteAsync(null);

        for (var i = 0; i < 3; i++)
        {
            var entries = await EntriesAsync(storage, Monday.AddDays(i));
            var entry = Assert.Single(entries);
            Assert.Equal(EntryType.SickLeave, entry.Type);
            Assert.Equal(Monday, entry.AbsenceStart);
            Assert.Equal(Monday.AddDays(2), entry.AbsenceEnd);
        }
    }

    [Fact]
    public async Task SavingTwice_DoesNotDuplicateTheAbsence()
    {
        // Zweimal auf „Speichern" zu tippen ist auf dem Handy schnell passiert — vorher standen
        // danach zwei sich überlappende Krankmeldungen im Plan.
        var storage = new InMemoryStorageService();
        var vm = Vm(storage, EntryType.SickLeave);

        await vm.SaveCommand.ExecuteAsync(null);
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Single(await EntriesAsync(storage, Monday));
        Assert.Single(await EntriesAsync(storage, Monday.AddDays(2)));
    }

    [Fact]
    public async Task Save_LeavesOtherAbsenceTypesAlone()
    {
        var storage = new InMemoryStorageService();
        await Vm(storage, EntryType.Vacation).SaveCommand.ExecuteAsync(null);
        await Vm(storage, EntryType.SickLeave).SaveCommand.ExecuteAsync(null);

        var entries = await EntriesAsync(storage, Monday);
        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.Type == EntryType.Vacation);
        Assert.Contains(entries, e => e.Type == EntryType.SickLeave);
    }

    [Fact]
    public async Task Save_LeavesOtherPeopleAlone()
    {
        var storage = new InMemoryStorageService();
        await storage.SaveDayAsync(new CalendarDay
        {
            DateString = Monday.ToString("yyyy-MM-dd"),
            Entries = { new CalendarEntry { Id = "fremd", UserId = "andere", Type = EntryType.SickLeave } }
        });

        await Vm(storage, EntryType.SickLeave).SaveCommand.ExecuteAsync(null);

        var entries = await EntriesAsync(storage, Monday);
        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.UserId == "andere");
    }

    [Fact]
    public async Task Save_SwappedDates_AreNormalized()
    {
        var storage = new InMemoryStorageService();
        var vm = new MobileAbsenceViewModel(storage, Self(), EntryType.Vacation)
        {
            From = new DateTimeOffset(Monday.AddDays(2).ToDateTime(TimeOnly.MinValue)),
            To = new DateTimeOffset(Monday.ToDateTime(TimeOnly.MinValue))
        };

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Single(await EntriesAsync(storage, Monday));
        Assert.Single(await EntriesAsync(storage, Monday.AddDays(2)));
    }
}
