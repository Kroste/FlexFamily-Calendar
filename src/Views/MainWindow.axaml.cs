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
            _vm.UserManagementRequested -= OnUserManagementRequested;
            _vm.MonthOverviewRequested -= OnMonthOverviewRequested;
            _vm.HoursAccountRequested -= OnHoursAccountRequested;
            _vm.NotificationsRequested -= OnNotificationsRequested;
            _vm.AiSettingsRequested -= OnAiSettingsRequested;
            _vm.ActivityTypesRequested -= OnActivityTypesRequested;
        }
        _vm = DataContext as MainWindowViewModel;
        if (_vm != null)
        {
            _vm.ProfileRequested += OnProfileRequested;
            _vm.UserManagementRequested += OnUserManagementRequested;
            _vm.MonthOverviewRequested += OnMonthOverviewRequested;
            _vm.HoursAccountRequested += OnHoursAccountRequested;
            _vm.NotificationsRequested += OnNotificationsRequested;
            _vm.AiSettingsRequested += OnAiSettingsRequested;
            _vm.ActivityTypesRequested += OnActivityTypesRequested;
        }
    }

    private async void OnActivityTypesRequested()
    {
        if (_vm == null) return;
        try
        {
            var dialog = new ActivityTypeManagementDialog { DataContext = _vm.CreateActivityTypeManagement() };
            await dialog.ShowDialog(this);
            if (_vm.CalendarVm != null)
                await _vm.CalendarVm.ReloadActivityTypesAsync();
        }
        catch (Exception ex) { LogService.Error("Fehler in der Aktivitätstypen-Verwaltung", ex); }
    }

    private async void OnAiSettingsRequested()
    {
        if (_vm == null) return;
        try
        {
            var dialog = new AiSettingsDialog { DataContext = _vm.CreateAiSettings() };
            await dialog.ShowDialog(this);
        }
        catch (Exception ex) { LogService.Error("Fehler in den KI-Einstellungen", ex); }
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

    private async void OnUserManagementRequested()
    {
        if (_vm == null) return;
        try
        {
            var dialog = new UserManagementDialog { DataContext = _vm.CreateUserManagement() };
            await dialog.ShowDialog(this);
            await _vm.RefreshCurrentUserAsync();
            if (_vm.CalendarVm != null)
                await _vm.CalendarVm.ReloadUsersAsync();  // neue Benutzer sofort planbar
        }
        catch (Exception ex) { LogService.Error("Fehler in der Benutzerverwaltung", ex); }
    }
}
