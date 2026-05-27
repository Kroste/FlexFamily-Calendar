using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using System.Collections.ObjectModel;

namespace FlexFamilyCalendar.ViewModels;

public partial class UserManagementViewModel : ViewModelBase
{
    private readonly AuthService _auth;

    [ObservableProperty] private User? _selectedUser;
    [ObservableProperty] private string _errorMessage = "";

    public ObservableCollection<User> Users { get; } = new();

    /// <summary>null = neuer Benutzer; sonst der zu bearbeitende.</summary>
    public event Action<User?>? EditRequested;

    public UserManagementViewModel(AuthService auth)
    {
        _auth = auth;
        _ = ReloadAsync();
    }

    /// <summary>Erzeugt den Editor für einen Benutzer (null = neu) — vom Dialog-Code-Behind genutzt.</summary>
    public UserEditorViewModel CreateEditor(User? user)
        => new(_auth, user, isNew: user == null, selfMode: false);

    public async Task ReloadAsync()
    {
        var users = await _auth.GetUsersAsync();
        Users.Clear();
        foreach (var u in users.OrderBy(u => u.DisplayName))
            Users.Add(u);
    }

    [RelayCommand]
    private void NewUser() => EditRequested?.Invoke(null);

    [RelayCommand]
    private void EditUser()
    {
        if (SelectedUser != null) EditRequested?.Invoke(SelectedUser);
    }

    [RelayCommand]
    private async Task DeleteUserAsync()
    {
        if (SelectedUser == null) return;
        ErrorMessage = "";
        try
        {
            await _auth.DeleteUserAsync(SelectedUser.Id);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
