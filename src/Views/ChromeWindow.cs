using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

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
        // Pflicht-Guard, siehe LandedOnInteractiveChild.
        if (LandedOnInteractiveChild(e.Source, sender as Visual)) return;

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

    /// <summary>
    /// Läuft vom Ereignis-Ursprung den Visual-Tree hoch bis zur Drag-Fläche und meldet true,
    /// wenn unterwegs ein interaktives Control liegt.
    ///
    /// <para>Warum das nötig ist: <c>PointerPressed</c> bubbelt. Ein Button fängt den Press
    /// selbst ab und captured den Pointer — eine ComboBox tut das nicht. Ohne diesen Guard
    /// startet <c>BeginMoveDrag</c> einen Fenster-Drag, der Pointer wandert ans OS, und das
    /// Control sieht nie ein <c>PointerReleased</c>: das Dropdown öffnet gar nicht mehr, nur
    /// der ToolTip erscheint. Heute stehen in der Titelleiste nur die drei Fensterbuttons,
    /// die unauffällig bleiben — sobald dort ein Umschalter oder Suchfeld landet, ist der
    /// Guard der Unterschied zwischen bedienbar und tot.</para>
    ///
    /// <para>Die <c>ElementRole</c>-Rollen im Template lösen das NICHT: die regeln den
    /// OS-Hit-Test-Pfad, dieser Handler ist der managed Fallback und läuft davon unabhängig.
    /// Also nie als „dank ElementRole überflüssig" wegrefactoren.</para>
    /// </summary>
    private static bool LandedOnInteractiveChild(object? source, Visual? dragArea)
    {
        for (var v = source as Visual; v is not null; v = v.GetVisualParent())
        {
            // Die Drag-Fläche selbst (und alles darüber) ist zum Ziehen da.
            if (ReferenceEquals(v, dragArea)) return false;

            // Button deckt ToggleButton/CheckBox/RadioButton/RepeatButton mit ab.
            if (v is Button or ComboBox or TextBox or Slider or ListBox or MenuItem) return true;

            // Auffangnetz: alles Fokussierbare will den Klick selbst verarbeiten.
            if (v is InputElement { Focusable: true }) return true;
        }

        // Ursprung liegt außerhalb der Titelleiste (etwa in einem Popup-Root).
        return true;
    }
}
