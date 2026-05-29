using Avalonia.Controls;
using Avalonia.Input;
using FlexFamilyCalendar.Services;
using FlexFamilyCalendar.ViewModels;

namespace FlexFamilyCalendar.Views;

public partial class MainView : UserControl
{
    private MainWindowViewModel? _vm;
    private Panel? _overlay;

    public MainView() => InitializeComponent();

    private Window? OwnerWindow() => TopLevel.GetTopLevel(this) as Window;

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Browser-Lifetime hat kein MainWindow → DialogService bleibt sonst null und Klicks
        // (z.B. neuer Eintrag) wären stille No-Ops. Auf Desktop hat App.axaml.cs den
        // WindowDialogService bereits gesetzt; hier nur den Overlay-Pfad nachziehen.
        _overlay = this.FindControl<Panel>("DialogOverlay");
        if (App.DialogService is null
            && _overlay is not null
            && this.FindControl<ContentControl>("DialogContent") is { } content)
        {
            App.DialogService = new OverlayDialogService(_overlay, content);
        }

        // Backdrop-Klick (Klick neben den Dialog-Inhalt) = Abbruch.
        if (_overlay is not null)
            _overlay.PointerPressed += OnOverlayPointerPressed;

        // ESC = Abbruch. TextBox & Co. lassen ESC durchbubbeln, also reicht der normale Handler
        // am TopLevel (existiert im Browser als EmbeddableControlRoot).
        if (TopLevel.GetTopLevel(this) is { } top)
            top.KeyDown += OnTopLevelKeyDown;
    }

    private void OnOverlayPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Klicks auf den Inhalts-Border bzw. dessen Kinder durchlassen — nur Klick auf
        // den nackten Backdrop schließt. e.Source ist im Backdrop-Fall das Overlay-Panel selbst.
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

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm != null)
        {
            _vm.ProfileRequested -= OnProfileRequested;
            _vm.MonthOverviewRequested -= OnMonthOverviewRequested;
            _vm.HoursAccountRequested -= OnHoursAccountRequested;
            _vm.NotificationsRequested -= OnNotificationsRequested;
            _vm.AdminRequested -= OnAdminRequested;
        }
        _vm = DataContext as MainWindowViewModel;
        if (_vm != null)
        {
            _vm.ProfileRequested += OnProfileRequested;
            _vm.MonthOverviewRequested += OnMonthOverviewRequested;
            _vm.HoursAccountRequested += OnHoursAccountRequested;
            _vm.NotificationsRequested += OnNotificationsRequested;
            _vm.AdminRequested += OnAdminRequested;
        }
    }

    private async void OnAdminRequested()
    {
        if (_vm == null) return;
        var owner = OwnerWindow();
        if (owner is null) { LogService.Debug("Admin-Dialog im Browser noch nicht unterstützt."); return; }
        try
        {
            var dialog = new AdminDialog { DataContext = _vm.CreateAdmin() };
            await dialog.ShowDialog(owner);
            await _vm.RefreshCurrentUserAsync();
            if (_vm.CalendarVm != null)
                await _vm.CalendarVm.RefreshAllAsync();
        }
        catch (Exception ex) { LogService.Error("Fehler im Admin-Bereich", ex); }
    }

    private async void OnNotificationsRequested()
    {
        if (_vm == null) return;
        var owner = OwnerWindow();
        if (owner is null) { LogService.Debug("Benachrichtigungs-Dialog im Browser noch nicht unterstützt."); return; }
        try
        {
            var dialog = new NotificationsDialog { DataContext = _vm.CreateNotifications() };
            var result = await dialog.ShowDialog<NotificationResult?>(owner);
            await _vm.RefreshUnreadCountAsync();
            if (result is null || _vm.CalendarVm is null) return;

            if (result.ReplanUserId != null && result.ReplanDate.HasValue)
                await _vm.CalendarVm.StartReplanAsync(result.ReplanUserId, result.ReplanDate.Value);
            else if (result.NavigateDate.HasValue)
                await _vm.CalendarVm.GoToWeekContaining(result.NavigateDate.Value);
        }
        catch (Exception ex) { LogService.Error("Fehler bei den Benachrichtigungen", ex); }
    }

    private async void OnHoursAccountRequested()
    {
        if (_vm == null) return;
        var owner = OwnerWindow();
        if (owner is null) { LogService.Debug("Stundenkonto-Dialog im Browser noch nicht unterstützt."); return; }
        try
        {
            var dialog = new HoursAccountDialog { DataContext = _vm.CreateHoursAccount() };
            await dialog.ShowDialog(owner);
        }
        catch (Exception ex) { LogService.Error("Fehler im Stundenkonto", ex); }
    }

    private async void OnMonthOverviewRequested()
    {
        if (_vm == null) return;
        var owner = OwnerWindow();
        if (owner is null) { LogService.Debug("Monatsübersicht-Dialog im Browser noch nicht unterstützt."); return; }
        try
        {
            var dialog = new MonthOverviewDialog { DataContext = _vm.CreateMonthOverview() };
            await dialog.ShowDialog(owner);
        }
        catch (Exception ex) { LogService.Error("Fehler in der Monatsübersicht", ex); }
    }

    private async void OnProfileRequested()
    {
        if (_vm == null) return;
        var owner = OwnerWindow();
        if (owner is null) { LogService.Debug("Profil-Dialog im Browser noch nicht unterstützt."); return; }
        try
        {
            var dialog = new UserEditorDialog { DataContext = _vm.CreateProfileEditor() };
            await dialog.ShowDialog<UserEditorResult?>(owner);
            await _vm.RefreshCurrentUserAsync();
        }
        catch (Exception ex) { LogService.Error("Fehler im Profil-Dialog", ex); }
    }
}
