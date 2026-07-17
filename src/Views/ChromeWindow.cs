using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace FlexFamilyCalendar.Views;

/// <summary>
/// Basisklasse für alle App-Fenster (Master-CLAUDE.md-DoD). Setzt Custom-Chrome-Defaults:
/// keine OS-Titelbar (BorderOnly, sonst gehen die Resize-Griffe verloren), sondern eine
/// selbst-gerenderte Titelleiste (siehe <c>ChromeWindow.axaml</c>-Style) mit Drag/Min/Max/Close.
/// Alle Fenster sind default resizable.
/// </summary>
public class ChromeWindow : Window
{
    public ChromeWindow()
    {
        WindowDecorations = WindowDecorations.BorderOnly;
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaTitleBarHeightHint = 32;
        CanResize = true;

        // Class-basierter Selector, damit der ChromeWindow-Style auch für alle Subklassen
        // (MainWindow, *Dialog) greift — Avalonia-Type-Selektoren matchen nur den exakten Typ.
        Classes.Add("chrome-window");
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (e.NameScope.Find<Control>("PART_DragArea") is { } dragArea)
            dragArea.PointerPressed += OnTitleBarPressed;

        if (e.NameScope.Find<Button>("PART_MinimizeButton") is { } min)
            min.Click += (_, _) => WindowState = WindowState.Minimized;

        if (e.NameScope.Find<Button>("PART_MaximizeButton") is { } max)
            max.Click += (_, _) => WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal : WindowState.Maximized;

        if (e.NameScope.Find<Button>("PART_CloseButton") is { } close)
            close.Click += (_, _) => Close();
    }

    private void OnTitleBarPressed(object? sender, PointerPressedEventArgs e)
    {
        // Doppelklick auf die Titelleiste toggelt Maximieren (OS-typisch).
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal : WindowState.Maximized;
            return;
        }

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }
}
