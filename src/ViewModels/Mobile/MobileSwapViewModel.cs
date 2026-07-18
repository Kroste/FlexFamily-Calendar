using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;

namespace FlexFamilyCalendar.ViewModels.Mobile;

/// <summary>
/// Mobile-Formular für Schichttausch. Zeigt eingehende und ausgehende Anfragen zum
/// Bestätigen/Ablehnen/Zurückziehen und ein Formular für einen neuen Wunsch
/// (Mode = GiveAway — Kollege übernimmt meine Schicht). Die Schichtübertragung selbst
/// (Ownership-Wechsel des CalendarEntry) läuft nur bei Accept über die ShiftSwapEngine,
/// analog zum Desktop-Flow.
/// </summary>
public partial class MobileSwapViewModel : ObservableObject
{
    private readonly IStorageService _storage;
    private readonly NotificationService _notifications;
    private readonly User _user;
    private List<ShiftSwapRequest> _all = new();
    private List<User> _allUsers = new();

    public record MyShiftOption(DateOnly Date, CalendarEntry Entry)
    {
        public string Label => $"{Date:ddd dd.MM.} {Entry.StartTime:hh\\:mm}–{Entry.EndTime:hh\\:mm}";
    }

    public ObservableCollection<ShiftSwapRequest> Incoming { get; } = new();
    public ObservableCollection<ShiftSwapRequest> Outgoing { get; } = new();
    public ObservableCollection<MyShiftOption> MyShifts { get; } = new();
    public ObservableCollection<User> Targets { get; } = new();

    [ObservableProperty] private MyShiftOption? _selectedShift;
    [ObservableProperty] private User? _selectedTarget;
    [ObservableProperty] private string _message = "";
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _isBusy;

    public bool HasIncoming => Incoming.Count > 0;
    public bool HasOutgoing => Outgoing.Count > 0;

    public MobileSwapViewModel(IStorageService storage, NotificationService notifications, User user)
    {
        _storage = storage;
        _notifications = notifications;
        _user = user;
        _ = RefreshAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        try
        {
            _allUsers = await _storage.LoadUsersAsync();
            _all = await _storage.LoadSwapRequestsAsync();

            var openIn  = _all.Where(r => r.Status == SwapStatus.Pending && r.ToUserId   == _user.Id).ToList();
            var openOut = _all.Where(r => r.Status == SwapStatus.Pending && r.FromUserId == _user.Id).ToList();
            Sync(Incoming, openIn);
            Sync(Outgoing, openOut);
            OnPropertyChanged(nameof(HasIncoming));
            OnPropertyChanged(nameof(HasOutgoing));

            // Meine kommenden Schichten der nächsten 21 Tage (nur zukünftige Arbeit).
            MyShifts.Clear();
            var today = DateOnly.FromDateTime(DateTime.Today);
            for (var i = 0; i < 21; i++)
            {
                var d = today.AddDays(i);
                var day = await _storage.LoadDayAsync(d);
                foreach (var e in day.Entries)
                {
                    if (e.UserId != _user.Id) continue;
                    if (e.Type != EntryType.Work) continue;
                    MyShifts.Add(new MyShiftOption(d, e));
                }
            }

            Targets.Clear();
            foreach (var u in _allUsers.Where(u => u.Id != _user.Id).OrderBy(u => u.DisplayName))
                Targets.Add(u);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            LogService.Warn("MobileSwap Refresh: {0}", ex.Message);
        }
    }

    private static void Sync(ObservableCollection<ShiftSwapRequest> target, IReadOnlyList<ShiftSwapRequest> src)
    {
        target.Clear();
        foreach (var r in src) target.Add(r);
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        if (SelectedShift is null || SelectedTarget is null)
        {
            StatusMessage = Localizer.Instance["Mobile_Swap_Missing"];
            return;
        }
        IsBusy = true;
        StatusMessage = "";
        try
        {
            var req = new ShiftSwapRequest
            {
                Mode = SwapMode.GiveAway,
                FromUserId = _user.Id,
                FromUserName = _user.DisplayName,
                FromDate = SelectedShift.Date.ToString("yyyy-MM-dd"),
                FromEntryId = SelectedShift.Entry.Id,
                ToUserId = SelectedTarget.Id,
                ToUserName = string.IsNullOrEmpty(SelectedTarget.DisplayName) ? SelectedTarget.Username : SelectedTarget.DisplayName,
                Message = (Message ?? "").Trim()
            };
            _all.Add(req);
            await _storage.SaveSwapRequestsAsync(_all);
            await _notifications.AddAsync(req.ToUserId, "Notif_SwapOffered",
                req.FromDate, req.FromUserName, FmtDate(req.FromDate));
            StatusMessage = Localizer.Instance["Mobile_Swap_Sent"];
            Message = "";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            LogService.Warn("MobileSwap Create: {0}", ex.Message);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task AcceptAsync(ShiftSwapRequest? req) => await SetStatusAsync(req, SwapStatus.Accepted, "Notif_SwapAccepted");

    [RelayCommand]
    private async Task RejectAsync(ShiftSwapRequest? req) => await SetStatusAsync(req, SwapStatus.Rejected, "Notif_SwapRejected");

    [RelayCommand]
    private async Task WithdrawAsync(ShiftSwapRequest? req) => await SetStatusAsync(req, SwapStatus.Cancelled, "Notif_SwapWithdrawn");

    private async Task SetStatusAsync(ShiftSwapRequest? req, SwapStatus status, string notifyKey)
    {
        if (req is null) return;
        IsBusy = true;
        try
        {
            // Bei Accept die Schicht ownership-mäßig übertragen (analog Desktop-Flow).
            if (status == SwapStatus.Accepted)
            {
                var day = await _storage.LoadDayAsync(DateOnly.Parse(req.FromDate));
                CalendarDay? toDay = null;
                if (req.Mode == SwapMode.Exchange && !string.IsNullOrEmpty(req.ToDate))
                    toDay = req.ToDate == req.FromDate ? day : await _storage.LoadDayAsync(DateOnly.Parse(req.ToDate));
                if (ShiftSwapEngine.Validate(req, day, toDay) is { } err)
                {
                    StatusMessage = Localizer.Instance[err];
                    return;
                }
                ShiftSwapEngine.Apply(req, day, toDay);
                await _storage.SaveDayAsync(day);
                if (toDay != null && !ReferenceEquals(toDay, day))
                    await _storage.SaveDayAsync(toDay);
            }

            var stored = _all.FirstOrDefault(r => r.Id == req.Id);
            if (stored != null)
            {
                stored.Status = status;
                stored.RespondedAt = DateTime.Now;
                await _storage.SaveSwapRequestsAsync(_all);
            }

            // Benachrichtigung an die jeweils andere Seite.
            var otherUser = status == SwapStatus.Cancelled ? req.ToUserId : req.FromUserId;
            await _notifications.AddAsync(otherUser, notifyKey,
                req.FromDate,
                status == SwapStatus.Cancelled ? req.FromUserName : req.ToUserName,
                FmtDate(req.FromDate));

            StatusMessage = Localizer.Instance["Mobile_Swap_Done"];
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            LogService.Warn("MobileSwap SetStatus: {0}", ex.Message);
        }
        finally { IsBusy = false; }
    }

    private static string FmtDate(string iso) => DateOnly.Parse(iso).ToString("dd.MM.yyyy");
}
