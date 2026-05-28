using Avalonia.Controls;
using FlexFamilyCalendar.ViewModels;

namespace FlexFamilyCalendar.Views;

public partial class HolidaySettingsDialog : Window
{
    private HolidaySettingsViewModel? _vm;

    public HolidaySettingsDialog() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm != null) _vm.Closed -= OnClosed;
        _vm = DataContext as HolidaySettingsViewModel;
        if (_vm != null) _vm.Closed += OnClosed;
    }

    private void OnClosed() => Close();
}
