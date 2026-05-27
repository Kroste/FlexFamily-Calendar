using FlexFamilyCalendar.Models;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class WeekCopyTests
{
    private static CalendarEntry Entry(EntryType type, string title = "") => new()
    {
        UserId = "u1", UserDisplayName = "Anna", Type = type,
        StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(12), Title = title
    };

    [Theory]
    [InlineData(EntryType.Work, true)]
    [InlineData(EntryType.Activity, true)]
    [InlineData(EntryType.SickLeave, false)]
    [InlineData(EntryType.Vacation, false)]
    [InlineData(EntryType.Absence, false)]
    public void IsTemplate_OnlyWorkAndActivity(EntryType type, bool expected)
        => Assert.Equal(expected, WeekCopy.IsTemplate(type));

    [Fact]
    public void TemplateEntries_KeepsWorkActivity_DropsAbsences()
    {
        var src = new[]
        {
            Entry(EntryType.Work, "Schicht"),
            Entry(EntryType.Activity, "Sprachkurs"),
            Entry(EntryType.SickLeave),
            Entry(EntryType.Vacation),
            Entry(EntryType.Absence),
        };

        var copy = WeekCopy.TemplateEntries(src);

        Assert.Equal(2, copy.Count);
        Assert.All(copy, e => Assert.True(e.Type is EntryType.Work or EntryType.Activity));
    }

    [Fact]
    public void TemplateEntries_ClonesWithNewId_SameData()
    {
        var original = Entry(EntryType.Work, "Schicht");
        var copy = WeekCopy.TemplateEntries(new[] { original }).Single();

        Assert.NotEqual(original.Id, copy.Id);
        Assert.Equal(original.UserId, copy.UserId);
        Assert.Equal(original.Type, copy.Type);
        Assert.Equal(original.StartTime, copy.StartTime);
        Assert.Equal(original.EndTime, copy.EndTime);
        Assert.Equal(original.Title, copy.Title);
    }
}
