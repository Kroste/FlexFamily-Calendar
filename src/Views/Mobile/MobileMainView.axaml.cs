using Avalonia.Controls;
using Avalonia.Input;
using FlexFamilyCalendar.Services;

namespace FlexFamilyCalendar.Views.Mobile;

public partial class MobileMainView : UserControl
{
    private Panel? _overlay;

    public MobileMainView() => InitializeComponent();

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Analog zum Browser-Head (MainView.axaml.cs): SingleView-Kontext hat kein Window →
        // ein OverlayDialogService wird an das Overlay-Panel gehängt, damit z.B. der
        // "Verbindung"-Link auf dem Login-Screen ein Dialog-Overlay öffnen kann.
        _overlay = this.FindControl<Panel>("DialogOverlay");
        if (App.DialogService is null
            && _overlay is not null
            && this.FindControl<ContentControl>("DialogContent") is { } content)
        {
            App.DialogService = new OverlayDialogService(_overlay, content);
        }

        if (_overlay is not null)
            _overlay.PointerPressed += OnOverlayPointerPressed;
        if (TopLevel.GetTopLevel(this) is { } top)
            top.KeyDown += OnTopLevelKeyDown;
    }

    private void OnOverlayPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source == _overlay) App.DialogService?.CancelActive();
    }

    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _overlay?.IsVisible == true)
        {
            App.DialogService?.CancelActive();
            e.Handled = true;
        }
    }

    private void OnOverlayCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => App.DialogService?.CancelActive();
}
