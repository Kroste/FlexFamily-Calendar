using System.Globalization;
using System.Linq;
using System.Text;
using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services;

/// <summary>
/// Erzeugt das Wochenplan-PDF als Tabelle (Personen × Wochentage) — ohne externe Abhängigkeit
/// (reines Managed, Standard-Helvetica/WinAnsi). Nativ-frei → läuft zuverlässig im Avalonia-Prozess.
/// </summary>
public static class PdfExportService
{
    private const double PageW = 842, PageH = 595, Margin = 20;
    private const double PersonColW = 150;
    private const double HeaderTop = 52, HeaderH = 42;
    // Hinweiszeile: Platz für den Titel „Hinweise" links und pro Wochentag bis zu 2 Zeilen
    // umgebrochenen Text — sonst würden lange Hinweise stumm abgeschnitten.
    private const int NotesLineLimit = 2;
    private const double NotesLineH = 10;
    private const double NotesH = 14 + NotesLineLimit * NotesLineH;

    public static byte[] Render(WeekExport export) => Assemble(BuildPages(export));

    /// <summary>
    /// Zeichnet den Plan und gibt einen Content-Stream je Seite zurück.
    ///
    /// <para>Vorher entstand genau eine Seite, und Personen, die unten nicht mehr draufpassten,
    /// fehlten ersatzlos — ohne Hinweis im Dokument. Bei acht Personen mit je einem Termin ging
    /// das gerade noch auf; sobald Zeilen durch mehrere Termine wachsen, reißt der Plan ab.
    /// Jetzt bricht er um, und jede Folgeseite wiederholt den Spaltenkopf.</para>
    /// </summary>
    private static List<string> BuildPages(WeekExport export)
    {
        var pages = new List<string>();
        var c = new StringBuilder();

        void Fill(double r, double g, double b) => c.Append($"{F(r)} {F(g)} {F(b)} rg\n");
        void Stroke(double v) => c.Append($"{F(v)} {F(v)} {F(v)} RG\n");
        void RectFill(double x, double top, double w, double h) => c.Append($"{F(x)} {F(PageH - top - h)} {F(w)} {F(h)} re\nf\n");
        void Line(double x1, double t1, double x2, double t2) => c.Append($"{F(x1)} {F(PageH - t1)} m {F(x2)} {F(PageH - t2)} l S\n");
        void Text(double x, double top, double size, bool bold, string s)
            => c.Append("BT\n").Append($"/{(bold ? "F2" : "F1")} {F(size)} Tf\n")
                .Append($"1 0 0 1 {F(x)} {F(PageH - top)} Tm\n").Append($"({Escape(s)}) Tj\nET\n");
        void Center(double cx, double top, double size, bool bold, string s)
            => Text(cx - s.Length * size * 0.25, top, size, bold, s);

        var left = Margin;
        var right = PageW - Margin;
        var dayX = left + PersonColW;
        var colW = (right - dayX) / 7;
        var bodyTop = HeaderTop + HeaderH;
        var bodyBottom = PageH - Margin - NotesH - 14;   // Platz für Hinweiszeile + Fußzeile

        // Kopf und Spaltenköpfe. Auf jeder Seite, sonst weiß man auf Seite 2 nicht mehr,
        // welche Spalte welcher Wochentag ist.
        void DrawHeader(int pageNo, int pageCount)
        {
            Fill(0, 0, 0); Text(Margin, 24, 16, true, export.Title);
            Fill(0.4, 0.4, 0.4);
            var label = pageCount > 1
                ? $"{export.WeekLabel}  ({pageNo}/{pageCount})"
                : export.WeekLabel;
            Text(Margin, 40, 10.5, false, label);

            for (int i = 0; i < export.Days.Count && i < 7; i++)
            {
                var h = export.Days[i];
                var cx = dayX + i * colW + colW / 2;
                Fill(0, 0, 0); Center(cx, HeaderTop + 12, 10, true, h.DayName);
                Fill(0.45, 0.45, 0.45); Center(cx, HeaderTop + 23, 8, false, h.DateLabel);
                if (!string.IsNullOrEmpty(h.Holiday))
                { Fill(0.8, 0.45, 0.1); Center(cx, HeaderTop + 34, 7.5, false, Truncate(h.Holiday, colW - 6, 7.5)); }
            }
        }

        // Erst aufteilen, dann zeichnen: die Seitenzahl im Kopf ("1/3") muss beim Zeichnen
        // der ersten Seite schon feststehen.
        var pageRows = SplitIntoPages(export.Rows, bodyTop, bodyBottom);

        for (int pageIndex = 0; pageIndex < pageRows.Count; pageIndex++)
        {
            c.Clear();
            DrawHeader(pageIndex + 1, pageRows.Count);
            var isLastPage = pageIndex == pageRows.Count - 1;

        // Personenzeilen
        var y = bodyTop;
        foreach (var row in pageRows[pageIndex])
        {
            var rowH = RowHeight(row);

            // Personenspalte
            var (pr, pg, pb) = Hex(row.ColorHex);
            Fill(pr, pg, pb); RectFill(left + 5, y + 5, 9, 9);
            Fill(0, 0, 0); Text(left + 19, y + 13, 9, true, Truncate(row.Name, PersonColW - 24, 9));
            Fill(0.5, 0.5, 0.5); Text(left + 19, y + 23, 7, false, Truncate(row.Category, PersonColW - 24, 7));

            // Tageszellen
            for (int i = 0; i < row.Cells.Count && i < 7; i++)
            {
                var cx = dayX + i * colW;
                var cy = y + 2.5;
                foreach (var e in row.Cells[i])
                {
                    var ch = string.IsNullOrEmpty(e.Time) ? 11.0 : 18.0;
                    if (cy + ch > y + rowH) break;
                    var (er, eg, eb) = Hex(e.ColorHex);
                    Fill(er, eg, eb); RectFill(cx + 1.5, cy, colW - 3, ch - 1.5);
                    var (tr, tg, tb) = TextColor(e.ColorHex);
                    Fill(tr, tg, tb);
                    var ty = cy + 6.5;
                    if (!string.IsNullOrEmpty(e.Time)) { Text(cx + 4, ty, 6.2, false, Truncate(e.Time, colW - 8, 6.2)); ty += 7.5; }
                    Text(cx + 4, ty, 7, false, Truncate(e.Label, colW - 8, 7));
                    cy += ch + 1.5;
                }
            }

            y += rowH;
            Stroke(0.88); Line(left, y, right, y);   // Zeilentrenner
        }

        // Hinweiszeile steht nur unter der letzten Person des Dokuments — sie gehört zur
        // Woche, nicht zur Seite.
        var notesTop = y;
        var tableBottom = isLastPage ? notesTop + NotesH : notesTop;
        if (isLastPage)
        {
            Fill(0.4, 0.4, 0.4); Text(left + 6, notesTop + 14, 9, true, "Hinweise");
            for (int i = 0; i < export.Notes.Count && i < 7; i++)
            {
                if (string.IsNullOrEmpty(export.Notes[i])) continue;
                var cx = dayX + i * colW + colW / 2;
                Fill(0.3, 0.3, 0.3);
                var lines = WrapText(export.Notes[i], colW - 6, 7.5, NotesLineLimit);
                for (int ln = 0; ln < lines.Count; ln++)
                    Center(cx, notesTop + 14 + ln * NotesLineH, 7.5, false, lines[ln]);
            }
        }

        // Rahmen + Spaltenlinien
        Stroke(0.75);
        Line(left, HeaderTop, right, HeaderTop);
        Line(left, bodyTop, right, bodyTop);
        if (isLastPage) Line(left, notesTop, right, notesTop);
        Line(left, tableBottom, right, tableBottom);
        Line(left, HeaderTop, left, tableBottom);
        Line(dayX, HeaderTop, dayX, tableBottom);
        for (int i = 1; i <= 7; i++)
            Line(dayX + i * colW, HeaderTop, dayX + i * colW, tableBottom);

        // Fußzeile
        Fill(0.5, 0.5, 0.5);
        Text(right - export.GeneratedLabel.Length * 8 * 0.45, PageH - 8, 8, false, export.GeneratedLabel);

            pages.Add(c.ToString());
        }

        return pages;
    }

    /// <summary>
    /// Verteilt die Personenzeilen auf Seiten. Eine Zeile wandert komplett auf die nächste
    /// Seite, statt zerschnitten zu werden — eine halbe Person über den Seitenrand hinweg
    /// wäre schlechter lesbar als ein Umbruch davor.
    /// </summary>
    /// <remarks>
    /// Passt eine einzelne Zeile selbst auf eine leere Seite nicht (sehr viele Termine an
    /// einem Tag), bekommt sie trotzdem ihre eigene Seite. Sonst entstünde eine Endlosschleife,
    /// und der untere Teil wird abgeschnitten — immer noch besser, als die Person ganz
    /// wegzulassen.
    /// </remarks>
    internal static List<List<PlanPersonRow>> SplitIntoPages(
        IReadOnlyList<PlanPersonRow> rows, double bodyTop, double bodyBottom)
    {
        var pages = new List<List<PlanPersonRow>>();
        var current = new List<PlanPersonRow>();
        var y = bodyTop;

        foreach (var row in rows)
        {
            var rowH = RowHeight(row);
            if (current.Count > 0 && y + rowH > bodyBottom)
            {
                pages.Add(current);
                current = [];
                y = bodyTop;
            }
            current.Add(row);
            y += rowH;
        }

        // Auch ohne Zeilen entsteht eine Seite: ein leerer Plan ist ein gültiges Dokument.
        pages.Add(current);
        return pages;
    }

    private static double RowHeight(PlanPersonRow row)
    {
        double max = 28;
        foreach (var cell in row.Cells)
        {
            double h = 5;
            foreach (var e in cell) h += (string.IsNullOrEmpty(e.Time) ? 11.0 : 18.0) + 1.5;
            if (h > max) max = h;
        }
        return max;
    }

    private static string Truncate(string text, double width, double size)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var max = Math.Max(1, (int)(width / (size * 0.5)));
        return text.Length <= max ? text : text[..Math.Max(1, max - 1)] + "…";
    }

    /// <summary>Bricht Text an Wortgrenzen in bis zu <paramref name="maxLines"/> Zeilen um. Reicht
    /// der Platz nicht, endet die letzte Zeile mit „…". Öffentlich für den Test.</summary>
    public static IReadOnlyList<string> WrapText(string text, double width, double size, int maxLines)
    {
        if (string.IsNullOrWhiteSpace(text) || maxLines <= 0) return Array.Empty<string>();
        var max = Math.Max(1, (int)(width / (size * 0.5)));
        var result = new List<string>();
        var remaining = text.Trim();
        while (remaining.Length > 0)
        {
            if (remaining.Length <= max) { result.Add(remaining); break; }
            var isLast = result.Count == maxLines - 1;
            if (isLast)
            {
                result.Add(remaining[..Math.Max(1, max - 1)] + "…");
                break;
            }
            // Zeile bis zum letzten Space vor `max` — sonst hart am Char-Limit brechen.
            var searchEnd = Math.Min(max, remaining.Length - 1);
            var split = remaining.LastIndexOf(' ', searchEnd);
            if (split <= 0) split = max;
            result.Add(remaining[..split].TrimEnd());
            remaining = remaining[split..].TrimStart();
        }
        return result;
    }

    private static (double, double, double) Hex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6) return (0.5, 0.5, 0.5);
        return (
            int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber) / 255.0,
            int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber) / 255.0,
            int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber) / 255.0);
    }

    /// <summary>Lesbare Textfarbe auf der Kachel. Delegiert bewusst an <see cref="EntryColors.OnTile"/>:
    /// vorher entschied hier eine eigene Helligkeitsschwelle, und seit die Kachelfarbe aus der
    /// Art des Eintrags kommt, wichen Bildschirm und Ausdruck bei mittleren Farben voneinander
    /// ab — dieselbe Kachel einmal mit weißer, einmal mit schwarzer Uhrzeit.</summary>
    private static (double, double, double) TextColor(string colorHex)
        => EntryColors.OnTile(colorHex) == "#000000" ? (0.15, 0.15, 0.15) : (1, 1, 1);

    private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Escape(string s)
    {
        var b = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            var w = WinAnsi(ch);
            if (w is '(' or ')' or '\\') b.Append('\\');
            b.Append(w);
        }
        return b.ToString();
    }

    private static char WinAnsi(char ch)
    {
        if (ch <= 0xFF) return ch;
        return ch switch
        {
            '–' => (char)0x96, '—' => (char)0x97,
            '‘' => (char)0x91, '’' => (char)0x92, '“' => (char)0x93, '”' => (char)0x94,
            '•' => (char)0x95, '·' => (char)0xB7, '…' => (char)0x85, '€' => (char)0x80,
            _ => '?'
        };
    }

    /// <summary>
    /// Setzt die Seiten-Streams zu einem PDF zusammen. Die Objektnummern werden mitgezählt,
    /// weil ihre Anzahl jetzt von der Seitenzahl abhängt:
    /// 1 Katalog, 2 Seitenbaum, dann je Seite ein Page- und ein Content-Objekt, zuletzt die
    /// beiden Fonts.
    /// </summary>
    private static byte[] Assemble(IReadOnlyList<string> pages)
    {
        var n = Math.Max(1, pages.Count);
        var firstPageObj = 3;
        var firstContentObj = firstPageObj + n;
        var fontRegular = firstContentObj + n;
        var fontBold = fontRegular + 1;
        var totalObjects = fontBold + 1;      // +1 für das Freilisten-Objekt 0

        var sb = new StringBuilder();
        var offsets = new List<int>();
        void Obj(string body) { offsets.Add(sb.Length); sb.Append(body); }

        sb.Append("%PDF-1.4\n");
        Obj("1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n");

        var kids = string.Join(" ", Enumerable.Range(0, n).Select(i => $"{firstPageObj + i} 0 R"));
        Obj($"2 0 obj<</Type/Pages/Kids[{kids}]/Count {n}>>endobj\n");

        for (int i = 0; i < n; i++)
            Obj($"{firstPageObj + i} 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 842 595]" +
                $"/Resources<</Font<</F1 {fontRegular} 0 R/F2 {fontBold} 0 R>>>>" +
                $"/Contents {firstContentObj + i} 0 R>>endobj\n");

        for (int i = 0; i < n; i++)
        {
            var content = i < pages.Count ? pages[i] : "";
            Obj($"{firstContentObj + i} 0 obj<</Length {content.Length}>>stream\n{content}\nendstream endobj\n");
        }

        Obj($"{fontRegular} 0 obj<</Type/Font/Subtype/Type1/BaseFont/Helvetica/Encoding/WinAnsiEncoding>>endobj\n");
        Obj($"{fontBold} 0 obj<</Type/Font/Subtype/Type1/BaseFont/Helvetica-Bold/Encoding/WinAnsiEncoding>>endobj\n");

        var xref = sb.Length;
        sb.Append($"xref\n0 {totalObjects}\n0000000000 65535 f \n");
        foreach (var off in offsets) sb.Append(off.ToString("D10")).Append(" 00000 n \n");
        sb.Append($"trailer<</Size {totalObjects}/Root 1 0 R>>\nstartxref\n{xref}\n%%EOF");

        return Encoding.Latin1.GetBytes(sb.ToString());
    }
}
