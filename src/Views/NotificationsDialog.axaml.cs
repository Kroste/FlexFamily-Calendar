using Avalonia.Controls;
using FlexFamilyCalendar.ViewModels;

namespace FlexFamilyCalendar.Views;

public partial class NotificationsDialog : Window
{
    private NotificationsViewModel? _vm;

    public NotificationsDialog() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_vm != null)
            _vm.CloseRequested -= OnCloseRequested;

        _vm = DataContext as NotificationsViewModel;

        if (_vm != null)
            _vm.CloseRequested += OnCloseRequested;
    }

    private void OnCloseRequested(DateOnly? navigateTo) => Close(navigateTo);
}
