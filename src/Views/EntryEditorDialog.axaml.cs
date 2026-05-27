using Avalonia.Controls;
using FlexFamilyCalendar.ViewModels;

namespace FlexFamilyCalendar.Views;

public partial class EntryEditorDialog : Window
{
    private EntryEditorViewModel? _vm;

    public EntryEditorDialog() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_vm != null)
            _vm.Closed -= OnVmClosed;

        _vm = DataContext as EntryEditorViewModel;

        if (_vm != null)
            _vm.Closed += OnVmClosed;
    }

    private void OnVmClosed(EntryDialogResult? result) => Close(result);
}
