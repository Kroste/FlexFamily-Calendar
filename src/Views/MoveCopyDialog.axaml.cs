using Avalonia.Controls;
using FlexFamilyCalendar.ViewModels;

namespace FlexFamilyCalendar.Views;

public partial class MoveCopyDialog : Window
{
    private MoveCopyViewModel? _vm;

    public MoveCopyDialog() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm != null) _vm.Closed -= OnClosed;
        _vm = DataContext as MoveCopyViewModel;
        if (_vm != null) _vm.Closed += OnClosed;
    }

    private void OnClosed(MoveCopyResult? result) => Close(result);
}
