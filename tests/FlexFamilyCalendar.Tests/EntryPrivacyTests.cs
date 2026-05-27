using FlexFamilyCalendar.Models;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class EntryPrivacyTests
{
    [Theory]
    [InlineData(EntryType.SickLeave)]
    [InlineData(EntryType.Vacation)]
    public void Private_ForOthers_ShownAsAbsence_NoReason(EntryType type)
    {
        Assert.Equal(EntryType.Absence, EntryPrivacy.DisplayType(type, canSeeReason: false));
        Assert.False(EntryPrivacy.ShowReason(type, canSeeReason: false));
    }

    [Theory]
    [InlineData(EntryType.SickLeave)]
    [InlineData(EntryType.Vacation)]
    public void Private_ForOwnerOrAdmin_RealTypeAndReason(EntryType type)
    {
        Assert.Equal(type, EntryPrivacy.DisplayType(type, canSeeReason: true));
        Assert.True(EntryPrivacy.ShowReason(type, canSeeReason: true));
    }

    [Theory]
    [InlineData(EntryType.Work)]
    [InlineData(EntryType.Activity)]
    [InlineData(EntryType.Absence)]
    public void NonPrivate_NeverMasked(EntryType type)
    {
        Assert.Equal(type, EntryPrivacy.DisplayType(type, canSeeReason: false));
        Assert.True(EntryPrivacy.ShowReason(type, canSeeReason: false));
    }
}
