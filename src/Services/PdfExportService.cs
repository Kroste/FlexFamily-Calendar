using System.Globalization;
using System.Text;

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
    private const double NotesH = 28;

    public static byte[] Render(WeekExport export) => Assemble(BuildContent(export));

    private static string BuildContent(WeekExport export)
    {
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
        var notesBottom = PageH - Margin;
        var notesTop = notesBottom - NotesH;
        var bodyBottom = notesTop - 2;

        // Kopf
        Fill(0, 0, 0); Text(Margin, 24, 16, true, export.Title);
        Fill(0.4, 0.4, 0.4); Text(Margin, 40, 10.5, false, export.WeekLabel);

        // Spaltenköpfe (Wochentag, Datum, Feiertag)
        for (int i = 0; i < export.Days.Count && i < 7; i++)
        {
            var h = export.Days[i];
            var cx = dayX + i * colW + colW / 2;
            Fill(0, 0, 0); Center(cx, HeaderTop + 12, 10, true, h.DayName);
            Fill(0.45, 0.45, 0.45); Center(cx, HeaderTop + 23, 8, false, h.DateLabel);
            if (!string.IsNullOrEmpty(h.Holiday))
            { Fill(0.8, 0.45, 0.1); Center(cx, HeaderTop + 34, 7.5, false, Truncate(h.Holiday, colW - 6, 7.5)); }
        }

        // Personenzeilen
        var y = bodyTop;
        foreach (var row in export.Rows)
        {
            var rowH = RowHeight(row);
            if (y + rowH > bodyBottom) break;   // passt nicht mehr → abschneiden

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
                    var (tr, tg, tb) = TextColor(er, eg, eb);
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

        // Hinweiszeile unten (Tagesnotizen)
        Fill(0.4, 0.4, 0.4); Text(left + 6, notesTop + 16, 9, true, "Hinweise");
        for (int i = 0; i < export.Notes.Count && i < 7; i++)
        {
            if (string.IsNullOrEmpty(export.Notes[i])) continue;
            var cx = dayX + i * colW + colW / 2;
            Fill(0.3, 0.3, 0.3); Center(cx, notesTop + 16, 7.5, false, Truncate(export.Notes[i], colW - 6, 7.5));
        }

        // Rahmen + Spaltenlinien
        Stroke(0.75);
        Line(left, HeaderTop, right, HeaderTop);
        Line(left, bodyTop, right, bodyTop);
        Line(left, notesTop, right, notesTop);
        Line(left, notesBottom, right, notesBottom);
        Line(left, HeaderTop, left, notesBottom);
        Line(dayX, HeaderTop, dayX, notesBottom);
        for (int i = 1; i <= 7; i++)
            Line(dayX + i * colW, HeaderTop, dayX + i * colW, notesBottom);

        // Fußzeile
        Fill(0.5, 0.5, 0.5);
        Text(right - export.GeneratedLabel.Length * 8 * 0.45, PageH - 8, 8, false, export.GeneratedLabel);

        return c.ToString();
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

    private static (double, double, double) Hex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6) return (0.5, 0.5, 0.5);
        return (
            int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber) / 255.0,
            int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber) / 255.0,
            int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber) / 255.0);
    }

    /// <summary>Lesbare Textfarbe je nach Helligkeit des Hintergrunds (dunkel auf hell, sonst weiß).</summary>
    private static (double, double, double) TextColor(double r, double g, double b)
        => 0.299 * r + 0.587 * g + 0.114 * b > 0.62 ? (0.15, 0.15, 0.15) : (1, 1, 1);

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

    private static byte[] Assemble(string content)
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();
        void Obj(string body) { offsets.Add(sb.Length); sb.Append(body); }

        sb.Append("%PDF-1.4\n");
        Obj("1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n");
        Obj("2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj\n");
        Obj("3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 842 595]" +
            "/Resources<</Font<</F1 5 0 R/F2 6 0 R>>>>/Contents 4 0 R>>endobj\n");
        Obj($"4 0 obj<</Length {content.Length}>>stream\n{content}\nendstream endobj\n");
        Obj("5 0 obj<</Type/Font/Subtype/Type1/BaseFont/Helvetica/Encoding/WinAnsiEncoding>>endobj\n");
        Obj("6 0 obj<</Type/Font/Subtype/Type1/BaseFont/Helvetica-Bold/Encoding/WinAnsiEncoding>>endobj\n");

        var xref = sb.Length;
        sb.Append("xref\n0 7\n0000000000 65535 f \n");
        foreach (var off in offsets) sb.Append(off.ToString("D10")).Append(" 00000 n \n");
        sb.Append($"trailer<</Size 7/Root 1 0 R>>\nstartxref\n{xref}\n%%EOF");

        return Encoding.Latin1.GetBytes(sb.ToString());
    }
}
