using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Services;
using System.Collections.ObjectModel;

namespace FlexFamilyCalendar.ViewModels;

/// <summary>Ein auswählbarer Empfänger im Mail-Dialog.</summary>
public partial class MailRecipientItem : ObservableObject
{
    public string Name { get; }
    public string Email { get; }
    [ObservableProperty] private bool _isSelected = true;

    public MailRecipientItem(string name, string email)
    {
        Name = name;
        Email = email;
    }
}

/// <summary>Empfänger-Auswahl für den Plan-Versand. Ergebnis: gewählte E-Mail-Adressen (oder null bei Abbruch).</summary>
public partial class MailViewModel : ViewModelBase
{
    public ObservableCollection<MailRecipientItem> Recipients { get; } = new();

    public event Action<IReadOnlyList<string>?>? Closed;

    public MailViewModel(IReadOnlyList<MailRecipient> recipients)
    {
        foreach (var r in recipients)
            Recipients.Add(new MailRecipientItem(r.Name, r.Email));
    }

    [RelayCommand]
    private void Send()
        => Closed?.Invoke(Recipients.Where(r => r.IsSelected).Select(r => r.Email).ToList());

    [RelayCommand]
    private void Cancel() => Closed?.Invoke(null);
}
