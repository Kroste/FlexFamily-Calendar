using Avalonia.Controls;
using FlexFamilyCalendar.ViewModels;

namespace FlexFamilyCalendar.Views;

public partial class MailDialog : Window
{
    private MailViewModel? _vm;

    public MailDialog() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm != null) _vm.Closed -= OnClosed;
        _vm = DataContext as MailViewModel;
        if (_vm != null) _vm.Closed += OnClosed;
    }

    private void OnClosed(IReadOnlyList<string>? result) => Close(result);
}
