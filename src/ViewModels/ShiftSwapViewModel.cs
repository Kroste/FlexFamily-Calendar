using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using System.Collections.ObjectModel;
using System.Globalization;

namespace FlexFamilyCalendar.ViewModels;

public enum SwapDialogMode { Initiate, Respond, Withdraw }
public enum SwapDialogAction { Create, Accept, Reject, Withdraw }

public record SwapDialogResult(SwapDialogAction Action, ShiftSwapRequest Request);
public record SwapModeOption(SwapMode Mode, string Label);

/// <summary>Eine wählbare Gegen-Schicht eines Kollegen (für den echten Tausch).</summary>
public record SwapShiftOption(string EntryId, string Date, string UserId, string Label);

/// <summary>
/// Dialog für den Schichttausch in drei Modi: Initiate (anbieten), Respond (annehmen/ablehnen),
/// Withdraw (eigene Anfrage zurückziehen).
/// </summary>
public partial class ShiftSwapViewModel : ViewModelBase
{
    private readonly User _me;
    private readonly CalendarEntry? _myShift;
    private readonly DateOnly _myDate;
    private readonly ShiftSwapRequest? _existing;
    private readonly IReadOnlyList<SwapShiftOption> _allColleagueShifts = [];

    public SwapDialogMode DialogMode { get; }

    // --- Initiate ---
    public IReadOnlyList<User> Colleagues { get; } = [];
    public IReadOnlyList<SwapModeOption> Modes { get; } = [];
    public ObservableCollection<SwapShiftOption> CounterShifts { get; } = new();

    [ObservableProperty] private User? _selectedColleague;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExchange))]
    [NotifyPropertyChangedFor(nameof(ShowNoCounterShifts))]
    private SwapModeOption? _selectedMode;

    [ObservableProperty] private SwapShiftOption? _selectedCounterShift;
    [ObservableProperty] private string _message = "";
    [ObservableProperty] private string _errorMessage = "";

    public bool IsInitiate => DialogMode == SwapDialogMode.Initiate;
    public bool IsRespond => DialogMode == SwapDialogMode.Respond;
    public bool IsWithdraw => DialogMode == SwapDialogMode.Withdraw;
    public bool IsExchange => IsInitiate && SelectedMode?.Mode == SwapMode.Exchange;
    public bool ShowNoCounterShifts => IsExchange && CounterShifts.Count == 0;

    public string HeaderText => Localizer.Instance[IsInitiate ? "Swap_Title" : IsRespond ? "Swap_IncomingTitle" : "Swap_WithdrawTitle"];
    public string ShiftLabel { get; } = "";
    public string Summary { get; } = "";
    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);

    public event Action<SwapDialogResult?>? Closed;

    /// <summary>Initiate: eigene Schicht einem Kollegen anbieten (abgeben oder tauschen).</summary>
    public ShiftSwapViewModel(User me, CalendarEntry myShift, DateOnly myDate,
        IReadOnlyList<User> colleagues, IReadOnlyList<SwapShiftOption> colleagueShifts)
    {
        _me = me;
        _myShift = myShift;
        _myDate = myDate;
        _allColleagueShifts = colleagueShifts;
        DialogMode = SwapDialogMode.Initiate;

        Colleagues = colleagues;
        Modes =
        [
            new SwapModeOption(SwapMode.GiveAway, Localizer.Instance["Swap_ModeGiveAway"]),
            new SwapModeOption(SwapMode.Exchange, Localizer.Instance["Swap_ModeExchange"])
        ];
        ShiftLabel = ShiftDescription(myDate, myShift);

        _selectedColleague = colleagues.FirstOrDefault();
        _selectedMode = Modes[0];
        RefreshCounterShifts();
    }

    /// <summary>Respond/Withdraw: bestehende Anfrage anzeigen. <paramref name="summary"/> wird vom Aufrufer lokalisiert gebaut.</summary>
    public ShiftSwapViewModel(User me, ShiftSwapRequest existing, SwapDialogMode mode, string summary)
    {
        _me = me;
        _existing = existing;
        DialogMode = mode;
        Summary = summary;
        Message = existing.Message;
    }

    partial void OnSelectedColleagueChanged(User? value) => RefreshCounterShifts();

    private void RefreshCounterShifts()
    {
        CounterShifts.Clear();
        if (SelectedColleague == null) return;
        foreach (var s in _allColleagueShifts.Where(s => s.UserId == SelectedColleague.Id))
            CounterShifts.Add(s);
        SelectedCounterShift = CounterShifts.FirstOrDefault();
        OnPropertyChanged(nameof(ShowNoCounterShifts));
    }

    private static string ShiftDescription(DateOnly date, CalendarEntry e)
        => $"{date.ToString("ddd dd.MM.", CultureInfo.CurrentCulture)} {e.TimeRange}";

    [RelayCommand]
    private void Send()
    {
        ErrorMessage = "";
        if (SelectedColleague == null) { ErrorMessage = Localizer.Instance["Swap_ErrorNoColleague"]; return; }
        if (IsExchange && SelectedCounterShift == null) { ErrorMessage = Localizer.Instance["Swap_ErrorNoCounterShift"]; return; }

        var colleagueName = string.IsNullOrEmpty(SelectedColleague.DisplayName)
            ? SelectedColleague.Username : SelectedColleague.DisplayName;
        var myName = string.IsNullOrEmpty(_me.DisplayName) ? _me.Username : _me.DisplayName;

        var req = new ShiftSwapRequest
        {
            Mode = SelectedMode?.Mode ?? SwapMode.GiveAway,
            FromUserId = _me.Id,
            FromUserName = myName,
            FromDate = _myDate.ToString("yyyy-MM-dd"),
            FromEntryId = _myShift!.Id,
            ToUserId = SelectedColleague.Id,
            ToUserName = colleagueName,
            Message = Message.Trim()
        };
        if (req.Mode == SwapMode.Exchange && SelectedCounterShift != null)
        {
            req.ToDate = SelectedCounterShift.Date;
            req.ToEntryId = SelectedCounterShift.EntryId;
        }

        LogService.Debug("Tausch-Dialog: Senden ({0} → {1})", myName, colleagueName);
        Closed?.Invoke(new SwapDialogResult(SwapDialogAction.Create, req));
    }

    [RelayCommand]
    private void Accept() => Closed?.Invoke(new SwapDialogResult(SwapDialogAction.Accept, _existing!));

    [RelayCommand]
    private void Reject() => Closed?.Invoke(new SwapDialogResult(SwapDialogAction.Reject, _existing!));

    [RelayCommand]
    private void Withdraw() => Closed?.Invoke(new SwapDialogResult(SwapDialogAction.Withdraw, _existing!));

    [RelayCommand]
    private void Cancel() => Closed?.Invoke(null);
}
