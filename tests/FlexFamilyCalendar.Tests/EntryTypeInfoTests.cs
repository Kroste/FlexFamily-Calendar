using FlexFamilyCalendar.Models;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class EntryTypeInfoTests
{
    [Theory]
    [MemberData(nameof(AllTypes))]
    public void EveryType_HasKey_Color_And_Label(EntryType type)
    {
        Assert.Equal($"EntryType_{type}", EntryTypeInfo.Key(type));

        var color = EntryTypeInfo.Color(type);
        Assert.StartsWith("#", color);
        Assert.True(color.Length == 7, $"Farbe für {type} sollte #RRGGBB sein, war '{color}'");

        Assert.False(string.IsNullOrWhiteSpace(EntryTypeInfo.Label(type)));
    }

    public static IEnumerable<object[]> AllTypes()
        => Enum.GetValues<EntryType>().Select(t => new object[] { t });
}
