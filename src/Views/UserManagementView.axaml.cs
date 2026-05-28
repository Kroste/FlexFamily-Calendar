using Avalonia.Controls;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using FlexFamilyCalendar.ViewModels;

namespace FlexFamilyCalendar.Views;

public partial class UserManagementView : UserControl
{
    private UserManagementViewModel? _vm;

    public UserManagementView() => InitializeComponent();

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
            if (TopLevel.GetTopLevel(this) is not Window owner) return;
            var dialog = new UserEditorDialog { DataContext = _vm.CreateEditor(user) };
            var result = await dialog.ShowDialog<UserEditorResult?>(owner);
            if (result != null)
                await _vm.ReloadAsync();
        }
        catch (Exception ex)
        {
            LogService.Error("Fehler im Benutzer-Editor", ex);
        }
    }
}
