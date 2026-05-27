using Avalonia.Styling;
using FlexFamilyCalendar.Theming;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class ThemeManagerTests
{
    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void ParseVariant_KnownValues(string id)
        => Assert.Equal(id == "Light" ? ThemeVariant.Light : ThemeVariant.Dark,
                        ThemeManager.ParseVariant(id));

    [Theory]
    [InlineData("System")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("unbekannt")]
    public void ParseVariant_FallsBackToDefault(string? id)
        => Assert.Equal(ThemeVariant.Default, ThemeManager.ParseVariant(id));

    [Fact]
    public void Variants_AreAvailable()
        => Assert.Contains(ThemeManager.Instance.AvailableVariants, v => v.Id == "Dark");
}
