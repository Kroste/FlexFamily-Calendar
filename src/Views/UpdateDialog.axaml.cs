using Avalonia.Controls;
using FlexFamilyCalendar.ViewModels;

namespace FlexFamilyCalendar.Views;

public partial class UpdateDialog : Window
{
    private UpdateViewModel? _vm;

    public UpdateDialog() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm != null) _vm.Closed -= OnClosed;
        _vm = DataContext as UpdateViewModel;
        if (_vm != null) _vm.Closed += OnClosed;
    }

    private void OnClosed(UpdateDialogAction? result) => Close(result);
}
