using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using FlexFamilyCalendar.Services.AI;
using System.Collections.ObjectModel;
using System.Globalization;

namespace FlexFamilyCalendar.ViewModels;

/// <summary>Eine Kandidatenzeile im Umplanungs-Dialog (Buchstabe + Name + Auslastung).</summary>
public class ReplanCandidateViewModel
{
    public User User { get; }
    public string Letter { get; }
    public string DisplayName { get; }
    public string Load { get; }

    public ReplanCandidateViewModel(char letter, ReplanEngine.ReplanCandidate c)
    {
        User = c.User;
        Letter = letter.ToString();
        DisplayName = string.IsNullOrEmpty(c.User.DisplayName) ? c.User.Username : c.User.DisplayName;
        var nf = CultureInfo.CurrentCulture;
        var target = c.WeeklyTarget > 0 ? $" / {c.WeeklyTarget.ToString("0.#", nf)}" : "";
        Load = $"{c.WeekWorkedHours.ToString("0.#", nf)}{target} h";
    }
}

/// <summary>Ergebnis des Umplanungs-Dialogs: die übernommene Schicht + der gewählte Ersatz.</summary>
public record ReplanResult(string ShiftId, DateOnly Date, User Replacement);

public partial class ReplanViewModel : ViewModelBase
{
    private readonly AiService _ai;
    private readonly CalendarEntry _absentShift;
    private readonly DateOnly _date;
    private readonly IReadOnlyList<ReplanEngine.ReplanCandidate> _candidates;

    public string ShiftLabel { get; }
    public ObservableCollection<ReplanCandidateViewModel> Candidates { get; } = new();
    public bool HasCandidates => Candidates.Count > 0;

    [ObservableProperty] private string _aiRecommendation = "";
    [ObservableProperty] private bool _aiBusy;

    public event Action<ReplanResult?>? Closed;

    public ReplanViewModel(AiService ai, CalendarEntry absentShift, DateOnly date,
        IReadOnlyList<ReplanEngine.ReplanCandidate> candidates)
    {
        _ai = ai;
        _absentShift = absentShift;
        _date = date;
        _candidates = candidates;

        ShiftLabel = $"{date.ToString("dddd, dd.MM.yyyy", CultureInfo.CurrentCulture)} · {absentShift.TimeRange}";
        for (var i = 0; i < candidates.Count; i++)
            Candidates.Add(new ReplanCandidateViewModel((char)('A' + i), candidates[i]));

        _ = LoadRecommendationAsync();
    }

    private async Task LoadRecommendationAsync()
    {
        if (_candidates.Count == 0) return;
        AiBusy = true;
        AiRecommendation = Localizer.Instance["Replan_AiThinking"];
        var prompt = ReplanEngine.BuildPrompt(_absentShift, _date, _candidates);
        var answer = await _ai.SuggestAsync(prompt);
        AiBusy = false;
        AiRecommendation = string.IsNullOrWhiteSpace(answer)
            ? Localizer.Instance["Replan_AiUnavailable"]
            : answer.Trim();
    }

    [RelayCommand]
    private void TakeOver(ReplanCandidateViewModel? candidate)
    {
        if (candidate == null) return;
        LogService.Debug("Umplanung: {0} übernimmt Schicht {1}", candidate.DisplayName, _absentShift.Id);
        Closed?.Invoke(new ReplanResult(_absentShift.Id, _date, candidate.User));
    }

    [RelayCommand]
    private void Cancel() => Closed?.Invoke(null);
}
