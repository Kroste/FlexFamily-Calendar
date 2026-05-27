using FlexFamilyCalendar.Models;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class EntryDisplayTests
{
    // --- Planungssicht: alle gleich, keine Hervorhebung ---
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Planning_Work_IsFull_NotHighlighted(bool isOwn)
    {
        var (opacity, hl) = EntryDisplay.Resolve(EntryType.Work, isOwn, personalView: false);
        Assert.Equal(1.0, opacity);
        Assert.False(hl);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Planning_NonWork_IsDimmed_NotHighlighted(bool isOwn)
    {
        var (opacity, hl) = EntryDisplay.Resolve(EntryType.Vacation, isOwn, personalView: false);
        Assert.Equal(EntryDisplay.NonWorkOpacity, opacity);
        Assert.False(hl);
    }

    // --- Normalsicht: eigene hervorgehoben ---
    [Fact]
    public void Personal_OwnWork_IsFull_Highlighted()
    {
        var (opacity, hl) = EntryDisplay.Resolve(EntryType.Work, isOwn: true, personalView: true);
        Assert.Equal(1.0, opacity);
        Assert.True(hl);
    }

    [Fact]
    public void Personal_OwnNonWork_Highlighted()
    {
        var (opacity, hl) = EntryDisplay.Resolve(EntryType.SickLeave, isOwn: true, personalView: true);
        Assert.Equal(EntryDisplay.OwnNonWorkOpacity, opacity);
        Assert.True(hl);
    }

    // --- Normalsicht: fremde stark gedämpft ---
    [Theory]
    [InlineData(EntryType.Work)]
    [InlineData(EntryType.AuPairShift)]
    [InlineData(EntryType.Vacation)]
    public void Personal_OtherEntries_AreDimmed_NotHighlighted(EntryType type)
    {
        var (opacity, hl) = EntryDisplay.Resolve(type, isOwn: false, personalView: true);
        Assert.Equal(EntryDisplay.OtherOpacity, opacity);
        Assert.False(hl);
    }
}
