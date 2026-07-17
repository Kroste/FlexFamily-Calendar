using Avalonia;
using Avalonia.Controls;
using FlexFamilyCalendar.Services;

namespace FlexFamilyCalendar.Views;

/// <summary>
/// Attached Property <c>Hint.Text</c> — statt <c>ToolTip.Tip="…"</c> direkt zu setzen,
/// deklarieren Views ihre Hover-Hilfe als <c>views:Hint.Text="Hilfe-Text"</c>. Der Text
/// landet in ToolTip.Tip NUR wenn <see cref="HintService.IsEnabled"/> true ist — der User
/// kann Hinweise damit über einen Profil-Toggle global abstellen.
///
/// Änderung von HintService.IsEnabled wird live in alle angehängten Controls durchgereicht.
/// </summary>
public static class Hint
{
    public static readonly AttachedProperty<string?> TextProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("Text", typeof(Hint));

    static Hint()
    {
        TextProperty.Changed.AddClassHandler<Control>(OnTextChanged);
        HintService.EnabledChanged += (_, _) =>
        {
            // Bei globalem Toggle alle bekannten Controls neu evaluieren.
            foreach (var c in _tracked)
                Apply(c);
        };
    }

    private static readonly HashSet<Control> _tracked = new();

    public static void SetText(Control obj, string? value) => obj.SetValue(TextProperty, value);
    public static string? GetText(Control obj) => obj.GetValue(TextProperty);

    private static void OnTextChanged(Control control, AvaloniaPropertyChangedEventArgs args)
    {
        _tracked.Add(control);
        Apply(control);
    }

    private static void Apply(Control control)
    {
        var text = control.GetValue(TextProperty);
        if (HintService.IsEnabled && !string.IsNullOrWhiteSpace(text))
            ToolTip.SetTip(control, text);
        else
            ToolTip.SetTip(control, null);
    }
}
