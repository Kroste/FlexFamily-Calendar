using Avalonia.Controls;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using FlexFamilyCalendar.ViewModels;

namespace FlexFamilyCalendar.Views;

public partial class UserManagementDialog : Window
{
    private UserManagementViewModel? _vm;

    public UserManagementDialog() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm != null) _vm.EditRequested -= OnEditRequested;
        _vm = DataContext as UserManagementViewModel;
        if (_vm != null) _vm.EditRequested += OnEditRequested;
    }

    private async void OnEditRequested(User? user)
    {
        if (_vm == null) return;
        try
        {
            var dialog = new UserEditorDialog { DataContext = _vm.CreateEditor(user) };
            var result = await dialog.ShowDialog<UserEditorResult?>(this);
            if (result != null)
                await _vm.ReloadAsync();
        }
        catch (Exception ex)
        {
            LogService.Error("Fehler im Benutzer-Editor", ex);
        }
    }
}
