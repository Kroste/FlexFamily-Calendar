using FlexFamilyCalendar.Models;
using Xunit;

namespace FlexFamilyCalendar.Tests;

/// <summary>Kachelfarbe nach Art des Eintrags plus lesbare Schrift darauf.</summary>
public class EntryColorsTests
{
    [Fact]
    public void Type_UsesBuiltInPalette()
    {
        Assert.Equal(EntryTypeInfo.Color(EntryType.Work), EntryColors.ForType(EntryType.Work));
    }

    [Fact]
    public void Tile_PrefersActivityCategoryOverType()
    {
        Assert.Equal("#8E44AD", EntryColors.Tile(EntryType.Activity, "#8E44AD"));
    }

    [Fact]
    public void Tile_FallsBackToTypeColor_WithoutCategory()
    {
        Assert.Equal(EntryTypeInfo.Color(EntryType.Work), EntryColors.Tile(EntryType.Work, null));
        Assert.Equal(EntryTypeInfo.Color(EntryType.Work), EntryColors.Tile(EntryType.Work, ""));
    }

    [Fact]
    public void Tile_UsesMaskedDisplayType()
    {
        // Datenschutz: fremde Krankmeldung erscheint als „Abwesend" — dann MUSS auch die Kachel
        // die Abwesend-Farbe tragen, sonst verrät das Rot der Krankmeldung den Grund.
        Assert.Equal(EntryColors.ForType(EntryType.Absence),
                     EntryColors.Tile(EntryType.Absence, null));
        Assert.NotEqual(EntryColors.ForType(EntryType.SickLeave),
                        EntryColors.Tile(EntryType.Absence, null));
    }

    [Theory]
    [InlineData("#F39C12")]   // Orange wie im Plan — hier ist Schwarz klar besser lesbar
    [InlineData("#FFFFFF")]
    [InlineData("#27AE60")]
    // Mittleres Blau wirkt „dunkel", erreicht gegen Schwarz aber 5,3:1 und gegen Weiß nur 4,0:1.
    // Genau dafür rechnet OnTile und rät nicht: das Auge liegt hier daneben.
    [InlineData("#2E86C1")]
    public void OnTile_IsBlack_OnLightBackground(string tile)
        => Assert.Equal("#000000", EntryColors.OnTile(tile));

    [Theory]
    [InlineData("#5B4B8A")]
    [InlineData("#000000")]
    [InlineData("#C0392B")]
    public void OnTile_IsWhite_OnDarkBackground(string tile)
        => Assert.Equal("#FFFFFF", EntryColors.OnTile(tile));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nonsense")]
    public void OnTile_FallsBackToWhite_OnUnparsableColor(string? tile)
        => Assert.Equal("#FFFFFF", EntryColors.OnTile(tile));

    [Theory]
    [InlineData("#FFF", true)]
    [InlineData("#FFAA00", true)]
    [InlineData("#FF112233", true)]
    [InlineData("112233", false)]
    [InlineData("#GGHHII", false)]
    [InlineData("#FF11223", false)]
    public void IsValidHex_AcceptsTheFormatsWeStore(string value, bool expected)
        => Assert.Equal(expected, EntryColors.IsValidHex(value));

    [Fact]
    public void OnTile_AlwaysReachesReadableContrast()
    {
        // Gegen jede der eingebauten Typfarben muss die gewählte Schrift den WCAG-Wert für
        // großen Text (3:1) schaffen — sonst ist die Uhrzeit auf der Kachel nicht lesbar.
        foreach (var type in Enum.GetValues<EntryType>())
        {
            var tile = EntryColors.ForType(type);
            var fg = EntryColors.OnTile(tile);
            Assert.True(Contrast(tile, fg) >= 3.0, $"{type}: {tile} auf {fg} = {Contrast(tile, fg):F2}:1");
        }
    }

    private static double Contrast(string a, string b)
    {
        var la = Luminance(a);
        var lb = Luminance(b);
        var (hi, lo) = la > lb ? (la, lb) : (lb, la);
        return (hi + 0.05) / (lo + 0.05);
    }

    private static double Luminance(string hex)
    {
        var s = hex.TrimStart('#');
        var r = System.Convert.ToByte(s[..2], 16) / 255.0;
        var g = System.Convert.ToByte(s[2..4], 16) / 255.0;
        var b = System.Convert.ToByte(s[4..6], 16) / 255.0;
        static double Lin(double c) => c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        return 0.2126 * Lin(r) + 0.7152 * Lin(g) + 0.0722 * Lin(b);
    }
}
