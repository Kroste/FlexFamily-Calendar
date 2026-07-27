using FlexFamilyCalendar.Services;
using Xunit;

namespace FlexFamilyCalendar.Tests;

/// <summary>
/// Absichern, dass mehrzeilige Hinweise im PDF sauber umgebrochen werden — bisher waren sie
/// hart abgeschnitten, was den Nutzer verwirrt hat.
/// </summary>
public class PdfExportWrapTests
{
    // Char-Kapazität pro Zeile bei width=90, size=7.5 ≈ 90 / (7.5*0.5) = 24 Zeichen.
    private const double W = 90;
    private const double S = 7.5;

    [Fact]
    public void ShortText_KeptAsSingleLine()
    {
        var lines = PdfExportService.WrapText("Kurzer Hinweis", W, S, 2);
        Assert.Single(lines);
        Assert.Equal("Kurzer Hinweis", lines[0]);
    }

    [Fact]
    public void LongText_WrapsAtWordBoundary()
    {
        var lines = PdfExportService.WrapText("Elias wird von Oma abgeholt und schläft dort", W, S, 2);
        Assert.Equal(2, lines.Count);
        // Keine der Zeilen darf länger sein als die berechnete Char-Kapazität.
        Assert.All(lines, l => Assert.True(l.Length <= 24));
        // Erste Zeile endet an einer Wortgrenze (kein Wortfragment am Ende).
        Assert.DoesNotContain('…', lines[0]);
    }

    [Fact]
    public void TextTooLongForMaxLines_LastLineEndsWithEllipsis()
    {
        var text = "Viel viel viel viel viel viel viel viel Text der garantiert nicht in zwei Zeilen passt zusätzlich mit Wörtern die weiterlaufen bis in Zeile drei und vier";
        var lines = PdfExportService.WrapText(text, W, S, 2);
        Assert.Equal(2, lines.Count);
        Assert.EndsWith("…", lines[^1]);
    }

    [Fact]
    public void EmptyText_ReturnsNoLines()
    {
        Assert.Empty(PdfExportService.WrapText("", W, S, 2));
        Assert.Empty(PdfExportService.WrapText("   ", W, S, 2));
    }

    [Fact]
    public void WordLongerThanLine_IsBrokenHard()
    {
        // Kein Space im Wort → hart am Char-Limit umbrechen, keine Endlosschleife.
        var lines = PdfExportService.WrapText(new string('A', 60), W, S, 2);
        Assert.Equal(2, lines.Count);
        Assert.EndsWith("…", lines[^1]);
    }
}
