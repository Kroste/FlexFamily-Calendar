using System.Globalization;
using System.Text;

namespace FlexFamilyCalendar.Services;

/// <summary>
/// Erzeugt das Wochenplan-PDF als Zeitraster (Zeitachse + 7 Tagesspalten, farbige Blöcke) — ohne externe
/// Abhängigkeit (reines Managed, Standard-Helvetica/WinAnsi). Nativ-frei → läuft zuverlässig im Avalonia-Prozess.
/// </summary>
public static class PdfExportService
{
    private const double PageW = 842, PageH = 595, Margin = 20;
    private const double TimeAxisW = 26;
    private const double HeaderTop = 58, GridTop = 122, FooterTop = PageH - 14;
    private static readonly double GridBottom = PageH - 22;

    public static byte[] Render(WeekExport export) => Assemble(BuildContent(export));

    private static string BuildContent(WeekExport export)
    {
        var c = new StringBuilder();

        void Fill(double r, double g, double b) => c.Append($"{F(r)} {F(g)} {F(b)} rg\n");
        void Stroke(double v) => c.Append($"{F(v)} {F(v)} {F(v)} RG\n");
        void Rect(double x, double top, double w, double h) => c.Append($"{F(x)} {F(PageH - top - h)} {F(w)} {F(h)} re\n");
        void RectFill(double x, double top, double w, double h) { Rect(x, top, w, h); c.Append("f\n"); }
        void Line(double x1, double t1, double x2, double t2) => c.Append($"{F(x1)} {F(PageH - t1)} m {F(x2)} {F(PageH - t2)} l S\n");
        void Text(double x, double top, double size, bool bold, string s)
            => c.Append("BT\n").Append($"/{(bold ? "F2" : "F1")} {F(size)} Tf\n")
                .Append($"1 0 0 1 {F(x)} {F(PageH - top)} Tm\n").Append($"({Escape(s)}) Tj\nET\n");

        var dayAreaX = Margin + TimeAxisW;
        var dayAreaW = PageW - Margin - dayAreaX;
        var colW = dayAreaW / 7;
        var hourH = (GridBottom - GridTop) / 24.0;

        // Kopf (fix oben; Tagesköpfe beginnen darunter ab HeaderTop)
        Fill(0, 0, 0); Text(Margin, 24, 16, true, export.Title);
        Fill(0.4, 0.4, 0.4); Text(Margin, 40, 10.5, false, export.WeekLabel);

        // Feiertags-Tönung der Spalte (dezent), zuerst zeichnen
        for (int i = 0; i < export.Days.Count && i < 7; i++)
        {
            if (string.IsNullOrEmpty(export.Days[i].Holiday)) continue;
            Fill(0.99, 0.95, 0.88);
            RectFill(dayAreaX + i * colW, GridTop, colW, GridBottom - GridTop);
        }

        // Stundenraster + Zeitachse
        Stroke(0.85);
        for (int h = 0; h <= 24; h++)
        {
            var y = GridTop + h * hourH;
            Line(dayAreaX, y, dayAreaX + dayAreaW, y);
            if (h < 24 && h % 2 == 0)
            {
                Fill(0.55, 0.55, 0.55);
                Text(Margin, y + 7, 6.5, false, $"{h:D2}:00");
            }
        }
        // Spaltenlinien
        Stroke(0.8);
        for (int i = 0; i <= 7; i++)
            Line(dayAreaX + i * colW, GridTop, dayAreaX + i * colW, GridBottom);

        // Tagesköpfe + Blöcke
        for (int i = 0; i < export.Days.Count && i < 7; i++)
        {
            var day = export.Days[i];
            var cx = dayAreaX + i * colW;
            var tx = cx + 4;
            var y = HeaderTop;

            Fill(0, 0, 0); Text(tx, y, 9, true, Truncate(day.DayName, colW - 8, 9)); y += 10;
            Fill(0.45, 0.45, 0.45); Text(tx, y, 7.5, false, day.DateLabel); y += 9;
            if (!string.IsNullOrEmpty(day.Holiday))
            { Fill(0.8, 0.45, 0.1); Text(tx, y, 7, false, Truncate(day.Holiday, colW - 8, 7)); y += 9; }
            if (!string.IsNullOrEmpty(day.Note))
            { Fill(0.3, 0.3, 0.3); Text(tx, y, 6.5, false, Truncate(day.Note, colW - 8, 6.5)); y += 9; }

            foreach (var chip in day.Absences)
            {
                if (y + 9 > GridTop - 1) break;
                var (r, g, b) = Hex(chip.ColorHex);
                Fill(r, g, b); RectFill(tx, y - 6.5, colW - 8, 9);
                var (tr, tg, tb) = TextColor(r, g, b);
                Fill(tr, tg, tb); Text(tx + 2, y, 6.5, false, Truncate(chip.Text, colW - 12, 6.5));
                y += 10.5;
            }

            // Blöcke
            foreach (var blk in day.Blocks)
            {
                var lanes = Math.Max(1, blk.LaneCount);
                var lw = (colW - 2) / lanes;
                var bx = cx + 1 + blk.LaneIndex * lw;
                var top = GridTop + blk.StartHour * hourH;
                var h = Math.Max(9, (blk.EndHour - blk.StartHour) * hourH - 1);

                var (br, bg, bb) = Blend(Hex(blk.ColorHex), blk.Opacity);
                Fill(br, bg, bb); RectFill(bx, top, lw - 1, h);

                var (tr, tg, tb) = TextColor(br, bg, bb);
                Fill(tr, tg, tb);
                var innerW = lw - 5;
                var ty = top + 7;
                var bottom = top + h - 1;
                Text(bx + 2, ty, 6, false, Truncate(blk.TimeLabel, innerW, 6)); ty += 7.5;
                foreach (var ln in blk.Lines)
                {
                    if (ty > bottom) break;
                    Text(bx + 2, ty, 6.8, false, Truncate(ln, innerW, 6.8)); ty += 7.5;
                }
            }
        }

        // Fußzeile
        Fill(0.5, 0.5, 0.5);
        Text(PageW - Margin - export.GeneratedLabel.Length * 8 * 0.45, FooterTop, 8, false, export.GeneratedLabel);

        return c.ToString();
    }

    /// <summary>Kürzt Text auf die verfügbare Breite (Helvetica ~0,5·Größe je Zeichen) und hängt „…" an.</summary>
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

    private static (double, double, double) Blend((double r, double g, double b) c, double op)
    {
        op = Math.Clamp(op, 0, 1);
        return (c.r * op + (1 - op), c.g * op + (1 - op), c.b * op + (1 - op));
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
            '•' => (char)0x95, '…' => (char)0x85, '€' => (char)0x80,
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
