using Avalonia.Controls;
using FlexFamilyCalendar.ViewModels;

namespace FlexFamilyCalendar.Views;

public partial class ShiftSwapDialog : ChromeWindow
{
    private ShiftSwapViewModel? _vm;

    public ShiftSwapDialog() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_vm != null)
            _vm.Closed -= OnVmClosed;

        _vm = DataContext as ShiftSwapViewModel;

        if (_vm != null)
            _vm.Closed += OnVmClosed;
    }

    private void OnVmClosed(SwapDialogResult? result) => Close(result);
}
