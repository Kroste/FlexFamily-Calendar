using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using System.Collections.ObjectModel;

namespace FlexFamilyCalendar.ViewModels;

/// <summary>Verwaltung der konfigurierbaren Aktivitätstypen (Master-Detail in einem Dialog).</summary>
public partial class ActivityTypeManagementViewModel : ViewModelBase
{
    private readonly IStorageService _storage;
    private List<ActivityType> _all = new();

    public ObservableCollection<ActivityType> Types { get; } = new();
    public IReadOnlyList<string> Colors => UserColorPalette.Colors;

    [ObservableProperty] private ActivityType? _selectedType;
    [ObservableProperty] private string _editName = "";
    [ObservableProperty] private string? _editColor;
    [ObservableProperty] private bool _editParent;
    [ObservableProperty] private bool _editChild;
    [ObservableProperty] private bool _editEmployee;
    [ObservableProperty] private bool _editAuPair;
    [ObservableProperty] private string _errorMessage = "";

    /// <summary>Wird nach jedem ReloadAsync gefeuert (inkl. nach Save/Delete) — Konsumenten wie
    /// die wiederkehrenden Aktivitäten halten ihre Kategorien-Auswahl damit aktuell, ohne dass
    /// der Admin-Dialog geschlossen werden muss.</summary>
    public event Action? Changed;

    public ActivityTypeManagementViewModel(IStorageService storage)
    {
        _storage = storage;
        _editColor = Colors[0];
        _ = ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        _all = await _storage.LoadActivityTypesAsync();
        Types.Clear();
        foreach (var t in _all.OrderBy(t => t.Name))
            Types.Add(t);
        Changed?.Invoke();
    }

    partial void OnSelectedTypeChanged(ActivityType? value)
    {
        if (value == null) return;
        ErrorMessage = "";
        EditName = value.Name;
        EditColor = value.Color;
        EditParent = value.Categories.Contains(PersonCategory.Parent);
        EditChild = value.Categories.Contains(PersonCategory.Child);
        EditEmployee = value.Categories.Contains(PersonCategory.Employee);
        EditAuPair = value.Categories.Contains(PersonCategory.AuPair);
    }

    private List<PersonCategory> CollectCategories()
    {
        var list = new List<PersonCategory>();
        if (EditParent) list.Add(PersonCategory.Parent);
        if (EditChild) list.Add(PersonCategory.Child);
        if (EditEmployee) list.Add(PersonCategory.Employee);
        if (EditAuPair) list.Add(PersonCategory.AuPair);
        return list;
    }

    [RelayCommand]
    private void New()
    {
        SelectedType = null;
        EditName = "";
        EditColor = Colors[0];
        EditParent = EditChild = EditEmployee = EditAuPair = false;
        ErrorMessage = "";
    }

    [RelayCommand]
    private async Task Save()
    {
        ErrorMessage = "";
        if (string.IsNullOrWhiteSpace(EditName)) { ErrorMessage = Localizer.Instance["ActType_ErrorNoName"]; return; }

        if (SelectedType == null)
        {
            _all.Add(new ActivityType
            {
                Name = EditName.Trim(),
                Color = EditColor ?? Colors[0],
                Categories = CollectCategories()
            });
        }
        else
        {
            SelectedType.Name = EditName.Trim();
            SelectedType.Color = EditColor ?? Colors[0];
            SelectedType.Categories = CollectCategories();
        }
        await _storage.SaveActivityTypesAsync(_all);
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedType == null) return;
        _all.RemoveAll(t => t.Id == SelectedType.Id);
        await _storage.SaveActivityTypesAsync(_all);
        New();
        await ReloadAsync();
    }
}
