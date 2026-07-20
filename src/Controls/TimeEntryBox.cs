using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using System.Globalization;

namespace FlexFamilyCalendar.Controls;

/// <summary>
/// Wiederverwendbares Uhrzeit-Eingabefeld: eine schmale TextBox, die per Tastatur mit
/// verschiedenen Formaten gefüttert werden kann („8", „830", „08:30", „8:5") und daraus
/// eine <see cref="TimeSpan"/> ableitet. Ersetzt in der App den nativen <c>TimePicker</c>,
/// weil der auf Touch/Klick zu umständlich ist.
///
/// - Live-Parsing beim Tippen; ungültige Zwischenstände lassen den <see cref="Time"/>-Wert unverändert.
/// - Beim Verlassen des Feldes (LostFocus) wird der Text auf <c>HH:mm</c> normalisiert oder,
///   falls kein gültiger Wert eingegeben wurde, auf den letzten gültigen Wert zurückgesetzt.
/// - Leer = <c>null</c>. Nur 24-h-Zeiten (00:00–23:59) sind gültig.
/// </summary>
public class TimeEntryBox : TextBox
{
    /// <summary>Der aktuell eingestellte Zeitwert (null = leer). Bindable, Zwei-Wege per Default.</summary>
    public static readonly DirectProperty<TimeEntryBox, TimeSpan?> TimeProperty =
        AvaloniaProperty.RegisterDirect<TimeEntryBox, TimeSpan?>(
            nameof(Time),
            o => o.Time,
            (o, v) => o.Time = v,
            defaultBindingMode: BindingMode.TwoWay);

    private TimeSpan? _time;
    public TimeSpan? Time
    {
        get => _time;
        set
        {
            if (SetAndRaise(TimeProperty, ref _time, value))
                SyncTextFromTime();
        }
    }

    // Verhindert Rekursion, wenn wir den Text aus dem Time-Setter selbst schreiben.
    private bool _syncingText;

    public TimeEntryBox()
    {
        MaxLength = 5;
        PlaceholderText = "hh:mm";
        Width = 90;
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        UseFloatingPlaceholder = false;

        // Avalonia 12: OnTextChanged/OnLostFocus sind nicht virtual — deshalb per Event-Subscription
        // gehen. Der Guard `_syncingText` verhindert Rekursion, wenn der Time-Setter Text neu setzt.
        TextChanged += (_, _) =>
        {
            if (_syncingText) return;
            if (TryParse(Text, out var parsed))
                Time = parsed;
        };
        LostFocus += (_, _) =>
        {
            // Beim Verlassen: auf sauberes HH:mm normalisieren — oder, falls Müll drinsteht,
            // auf den letzten gültigen Wert zurückschreiben.
            SyncTextFromTime();
        };
    }

    private void SyncTextFromTime()
    {
        _syncingText = true;
        try
        {
            Text = Time is null ? "" : $"{Time.Value.Hours:D2}:{Time.Value.Minutes:D2}";
        }
        finally
        {
            _syncingText = false;
        }
    }

    /// <summary>Robuste Parse-Logik: <c>null</c>/leer = ok mit result=null; sonst muss es eine gültige 24-h-Zeit sein.</summary>
    public static bool TryParse(string? input, out TimeSpan? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(input)) return true;

        var s = input.Trim();

        // Direkt HH:mm / H:mm / HH:m / H:m
        if (TimeSpan.TryParseExact(s, new[] { @"h\:m", @"h\:mm", @"hh\:m", @"hh\:mm" },
                CultureInfo.InvariantCulture, TimeSpanStyles.None, out var t))
        {
            if (t.Days == 0 && t.TotalHours < 24 && t.Milliseconds == 0)
            {
                result = new TimeSpan(t.Hours, t.Minutes, 0);
                return true;
            }
            return false;
        }

        // Reine Ziffern („8" / „08" / „830" / „0830")
        var digits = new string(s.Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return false;
        if (digits.Length != s.Length) return false; // gemischt (z.B. „8h") = ungültig

        int h, m;
        switch (digits.Length)
        {
            case 1: case 2: h = int.Parse(digits); m = 0; break;
            case 3: h = int.Parse(digits[..1]); m = int.Parse(digits[1..]); break;
            case 4: h = int.Parse(digits[..2]); m = int.Parse(digits[2..]); break;
            default: return false;
        }
        if (h < 0 || h > 23 || m < 0 || m > 59) return false;
        result = new TimeSpan(h, m, 0);
        return true;
    }
}
