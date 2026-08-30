using System.Text;
using FlexFamilyCalendar.Services;

namespace FlexFamilyCalendar.Tests;

/// <summary>
/// Das PDF erzeugte genau eine Seite, und Personen, die unten nicht mehr draufpassten, fehlten
/// ersatzlos — ohne Hinweis im Dokument. Diese Tests halten fest, dass jetzt umgebrochen wird
/// und niemand verloren geht.
/// </summary>
public class PdfExportPagingTests
{
    // Dieselben Maße wie im Renderer: A4 quer, Kopfbereich oben, Hinweiszeile unten.
    private const double BodyTop = 94;
    private const double BodyBottom = 547;

    private static PlanPersonRow Row(string name, int entriesPerDay = 0)
    {
        var cells = new List<IReadOnlyList<PlanCellEntry>>();
        for (int d = 0; d < 7; d++)
        {
            var cell = new List<PlanCellEntry>();
            for (int e = 0; e < entriesPerDay; e++)
                cell.Add(new PlanCellEntry("#3498DB", "08:00-09:00", "Arbeit"));
            cells.Add(cell);
        }
        return new PlanPersonRow(name, "#C0392B", "Eltern", cells);
    }

    private static WeekExport Export(params PlanPersonRow[] rows) => new(
        "FlexFamily", "KW 34 / 2026", "erzeugt am 23.08.2026",
        [.. Enumerable.Range(0, 7).Select(i => new PlanDayHeader($"Tag{i}", $"0{i + 1}.08.", ""))],
        rows,
        ["", "", "", "", "", "", ""]);

    private static int CountPages(byte[] pdf)
    {
        var text = Encoding.Latin1.GetString(pdf);
        var match = System.Text.RegularExpressions.Regex.Match(text, @"/Type/Pages/Kids\[(.*?)\]/Count (\d+)");
        Assert.True(match.Success, "Seitenbaum nicht gefunden");
        return int.Parse(match.Groups[2].Value);
    }

    [Fact]
    public void Wenige_Personen_passen_auf_eine_Seite()
    {
        var pdf = PdfExportService.Render(Export(Row("Anna"), Row("Bert"), Row("Cem")));
        Assert.Equal(1, CountPages(pdf));
    }

    [Fact]
    public void Viele_Personen_ergeben_mehrere_Seiten()
    {
        // 40 Personen passen sicher nicht auf eine Seite.
        var rows = Enumerable.Range(1, 40).Select(i => Row($"Person {i}")).ToArray();
        var pdf = PdfExportService.Render(Export(rows));

        Assert.True(CountPages(pdf) > 1, "Es hätte umgebrochen werden müssen");
    }

    [Fact]
    public void Keine_Person_geht_beim_Umbruch_verloren()
    {
        // Der eigentliche Fehler: vorher fielen die hinteren Zeilen still aus dem Dokument.
        var rows = Enumerable.Range(1, 40).Select(i => Row($"Person {i}")).ToArray();
        var pages = PdfExportService.SplitIntoPages(rows, BodyTop, BodyBottom);

        var verteilt = pages.SelectMany(p => p).Select(r => r.Name).ToList();
        Assert.Equal(40, verteilt.Count);
        Assert.Equal(rows.Select(r => r.Name), verteilt);
    }

    [Fact]
    public void Hohe_Zeilen_brauchen_mehr_Seiten_als_niedrige()
    {
        // Eine Person mit sechs Terminen am Tag braucht deutlich mehr Höhe als eine ohne.
        var schmal = Enumerable.Range(1, 12).Select(i => Row($"P{i}")).ToArray();
        var hoch = Enumerable.Range(1, 12).Select(i => Row($"P{i}", entriesPerDay: 6)).ToArray();

        var seitenSchmal = PdfExportService.SplitIntoPages(schmal, BodyTop, BodyBottom).Count;
        var seitenHoch = PdfExportService.SplitIntoPages(hoch, BodyTop, BodyBottom).Count;

        Assert.True(seitenHoch > seitenSchmal,
            $"hohe Zeilen: {seitenHoch} Seiten, schmale: {seitenSchmal}");
    }

    [Fact]
    public void Eine_Zeile_wird_nicht_zerschnitten()
    {
        // Zeilen wandern komplett auf die nächste Seite. Jede Seite trägt mindestens eine.
        var rows = Enumerable.Range(1, 40).Select(i => Row($"P{i}")).ToArray();
        var pages = PdfExportService.SplitIntoPages(rows, BodyTop, BodyBottom);

        Assert.All(pages, p => Assert.NotEmpty(p));
    }

    [Fact]
    public void Leerer_Plan_ergibt_trotzdem_ein_gueltiges_Dokument()
    {
        var pdf = PdfExportService.Render(Export());

        Assert.Equal(1, CountPages(pdf));
        Assert.StartsWith("%PDF-1.4", Encoding.Latin1.GetString(pdf, 0, 8));
    }

    [Fact]
    public void Mehrseitiges_PDF_hat_gueltige_Objektstruktur()
    {
        // Die Objektnummern hängen jetzt an der Seitenzahl. Stimmen xref-Anzahl und
        // trailer/Size nicht, öffnen strenge Betrachter das Dokument nicht.
        var rows = Enumerable.Range(1, 40).Select(i => Row($"P{i}")).ToArray();
        var text = Encoding.Latin1.GetString(PdfExportService.Render(Export(rows)));

        var pages = CountPagesFrom(text);
        var erwartet = 3 + pages * 2 + 2;   // 0-Objekt + Katalog + Baum + je Seite 2 + 2 Fonts

        Assert.Contains($"xref\n0 {erwartet}\n", text);
        Assert.Contains($"trailer<</Size {erwartet}/Root 1 0 R>>", text);
        Assert.EndsWith("%%EOF", text);
    }

    private static int CountPagesFrom(string text) =>
        int.Parse(System.Text.RegularExpressions.Regex.Match(text, @"/Count (\d+)").Groups[1].Value);
}
