using Avalonia.Controls;
using FlexFamilyCalendar.ViewModels;

namespace FlexFamilyCalendar.Views;

public partial class ReplanDialog : Window
{
    private ReplanViewModel? _vm;

    public ReplanDialog() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_vm != null)
            _vm.Closed -= OnClosed;

        _vm = DataContext as ReplanViewModel;

        if (_vm != null)
            _vm.Closed += OnClosed;
    }

    private void OnClosed(ReplanResult? result) => Close(result);
}
