using Avalonia.Controls;
using FlexFamilyCalendar.ViewModels;

namespace FlexFamilyCalendar.Views;

public partial class ConnectionSettingsDialog : ChromeWindow
{
    private ConnectionSettingsViewModel? _vm;

    public ConnectionSettingsDialog() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm != null) _vm.Closed -= OnClosed;
        _vm = DataContext as ConnectionSettingsViewModel;
        if (_vm != null) _vm.Closed += OnClosed;
    }

    private void OnClosed(ConnectionSettingsResult? r) => Close(r);
}
