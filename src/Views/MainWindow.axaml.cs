using Avalonia.Controls;
using FlexFamilyCalendar.Services;
using FlexFamilyCalendar.ViewModels;

namespace FlexFamilyCalendar.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _vm;

    public MainWindow() => InitializeComponent();

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
        try
        {
            var dialog = new AdminDialog { DataContext = _vm.CreateAdmin() };
            await dialog.ShowDialog(this);
            // Nach dem Admin-Bereich: Benutzer/Einstellungen/Kategorien/Regeln neu laden, Sprache/Anzeige anwenden.
            await _vm.RefreshCurrentUserAsync();
            if (_vm.CalendarVm != null)
                await _vm.CalendarVm.RefreshAllAsync();
        }
        catch (Exception ex) { LogService.Error("Fehler im Admin-Bereich", ex); }
    }

    private async void OnNotificationsRequested()
    {
        if (_vm == null) return;
        try
        {
            var dialog = new NotificationsDialog { DataContext = _vm.CreateNotifications() };
            var result = await dialog.ShowDialog<NotificationResult?>(this);
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
        try
        {
            var dialog = new HoursAccountDialog { DataContext = _vm.CreateHoursAccount() };
            await dialog.ShowDialog(this);
        }
        catch (Exception ex) { LogService.Error("Fehler im Stundenkonto", ex); }
    }

    private async void OnMonthOverviewRequested()
    {
        if (_vm == null) return;
        try
        {
            var dialog = new MonthOverviewDialog { DataContext = _vm.CreateMonthOverview() };
            await dialog.ShowDialog(this);
        }
        catch (Exception ex) { LogService.Error("Fehler in der Monatsübersicht", ex); }
    }

    private async void OnProfileRequested()
    {
        if (_vm == null) return;
        try
        {
            var dialog = new UserEditorDialog { DataContext = _vm.CreateProfileEditor() };
            await dialog.ShowDialog<UserEditorResult?>(this);
            await _vm.RefreshCurrentUserAsync();
        }
        catch (Exception ex) { LogService.Error("Fehler im Profil-Dialog", ex); }
    }
}
