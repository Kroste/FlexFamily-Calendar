using Avalonia.Controls;
using FlexFamilyCalendar.ViewModels;

namespace FlexFamilyCalendar.Views;

public partial class AiSettingsDialog : Window
{
    private AiSettingsViewModel? _vm;

    public AiSettingsDialog() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_vm != null)
            _vm.Closed -= OnClosed;

        _vm = DataContext as AiSettingsViewModel;

        if (_vm != null)
            _vm.Closed += OnClosed;
    }

    private void OnClosed() => Close();
}
