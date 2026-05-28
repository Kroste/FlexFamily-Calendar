using System.Globalization;
using System.Text;

namespace FlexFamilyCalendar.Services;

/// <summary>
/// Erzeugt das Wochenplan-PDF ohne externe Abhängigkeit (reines Managed, Standard-Helvetica/WinAnsi).
/// Bewusst nativ-frei, damit es im Avalonia-Prozess zuverlässig läuft (kein Skia-Konflikt). UI-unabhängig.
/// </summary>
public static class PdfExportService
{
    private const double PageW = 842, PageH = 595, Margin = 30;  // A4 quer (Punkte)

    public static byte[] Render(WeekExport export) => Assemble(BuildContent(export));

    private static string BuildContent(WeekExport export)
    {
        var c = new StringBuilder();

        void Color(double r, double g, double b) => c.Append($"{F(r)} {F(g)} {F(b)} rg\n");
        void Stroke(double v) => c.Append($"{F(v)} {F(v)} {F(v)} RG\n");
        void Line(double x1, double yt1, double x2, double yt2)
            => c.Append($"{F(x1)} {F(PageH - yt1)} m {F(x2)} {F(PageH - yt2)} l S\n");
        void Text(double x, double yTop, double size, bool bold, string s)
        {
            c.Append("BT\n").Append($"/{(bold ? "F2" : "F1")} {F(size)} Tf\n")
             .Append($"1 0 0 1 {F(x)} {F(PageH - yTop)} Tm\n")
             .Append($"({Escape(s)}) Tj\nET\n");
        }

        // Kopf
        Color(0, 0, 0);
        Text(Margin, Margin + 13, 16, true, export.Title);
        Color(0.4, 0.4, 0.4);
        Text(Margin, Margin + 30, 11, false, export.WeekLabel);

        var usableW = PageW - 2 * Margin;
        var colW = usableW / 7;
        const double pad = 4;
        var textW = colW - 2 * pad;
        var gridTop = Margin + 44;
        var gridBottom = PageH - Margin - 16;

        // Rahmen
        Stroke(0.7);
        Line(Margin, gridTop, Margin + usableW, gridTop);
        Line(Margin, gridBottom, Margin + usableW, gridBottom);
        for (int i = 0; i <= 7; i++)
            Line(Margin + i * colW, gridTop, Margin + i * colW, gridBottom);

        var days = export.Days;
        for (int i = 0; i < days.Count && i < 7; i++)
        {
            var day = days[i];
            var x = Margin + i * colW + pad;
            var y = gridTop + 13;

            Color(0, 0, 0);
            Text(x, y, 9.5, true, day.DayName); y += 11;
            Color(0.4, 0.4, 0.4);
            Text(x, y, 8, false, day.DateLabel); y += 11;

            if (!string.IsNullOrEmpty(day.Holiday))
            {
                Color(0.8, 0.45, 0.1);
                foreach (var l in Wrap(day.Holiday, MaxChars(textW, 8))) { if (y > gridBottom) break; Text(x, y, 8, false, l); y += 10; }
            }
            if (!string.IsNullOrEmpty(day.Note))
            {
                Color(0.25, 0.25, 0.25);
                foreach (var l in Wrap(day.Note, MaxChars(textW, 8))) { if (y > gridBottom) break; Text(x, y, 8, false, l); y += 10; }
            }

            Color(0.35, 0.35, 0.35);
            foreach (var a in day.Absences)
                foreach (var l in Wrap(a.Text, MaxChars(textW, 8))) { if (y > gridBottom) break; Text(x, y, 8, false, l); y += 10; }

            y += 3;
            foreach (var s in day.Shifts)
            {
                if (y > gridBottom) break;
                Color(0.15, 0.3, 0.6);
                Text(x, y, 8, false, s.Time); y += 9;
                Color(0, 0, 0);
                foreach (var l in Wrap(s.Text, MaxChars(textW, 9))) { if (y > gridBottom) break; Text(x, y, 9, false, l); y += 10; }
                y += 2;
            }
        }

        // Fußzeile (rechtsbündig, grob ausgerichtet)
        Color(0.5, 0.5, 0.5);
        var fw = export.GeneratedLabel.Length * 8 * 0.5;
        Text(PageW - Margin - fw, PageH - Margin + 6, 8, false, export.GeneratedLabel);

        return c.ToString();
    }

    /// <summary>Bricht Text wortweise auf höchstens <paramref name="maxChars"/> Zeichen je Zeile um (harte Trennung sehr langer Wörter).</summary>
    public static List<string> Wrap(string text, int maxChars)
    {
        var lines = new List<string>();
        if (string.IsNullOrEmpty(text)) return lines;
        maxChars = Math.Max(1, maxChars);

        var line = new StringBuilder();
        foreach (var word in text.Split(' '))
        {
            var w = word;
            while (w.Length > maxChars)   // sehr langes Wort hart trennen
            {
                if (line.Length > 0) { lines.Add(line.ToString()); line.Clear(); }
                lines.Add(w[..maxChars]);
                w = w[maxChars..];
            }
            if (line.Length == 0) line.Append(w);
            else if (line.Length + 1 + w.Length <= maxChars) line.Append(' ').Append(w);
            else { lines.Add(line.ToString()); line.Clear(); line.Append(w); }
        }
        if (line.Length > 0) lines.Add(line.ToString());
        return lines;
    }

    private static int MaxChars(double width, double size) => Math.Max(1, (int)(width / (size * 0.5)));

    private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Maskiert PDF-Sonderzeichen und bildet auf WinAnsi (Windows-1252) ab.</summary>
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
        if (ch <= 0xFF) return ch;   // Latin-1 deckt 0xA0–0xFF (inkl. Umlaute, ·, », «) byte-gleich ab
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
