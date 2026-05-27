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
        }
        _vm = DataContext as MainWindowViewModel;
        if (_vm != null)
        {
            _vm.ProfileRequested += OnProfileRequested;
            _vm.UserManagementRequested += OnUserManagementRequested;
            _vm.MonthOverviewRequested += OnMonthOverviewRequested;
        }
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
