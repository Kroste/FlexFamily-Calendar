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

public enum ReplanAction { TakeOver, MarkHealthy }

/// <summary>Ergebnis des Dialogs: Krankmeldung aufheben oder die ausgefallene Schicht übernehmen lassen.</summary>
public record ReplanResult(ReplanAction Action, DateOnly Date, string SickUserId,
    string? ShiftId = null, User? Replacement = null);

public partial class ReplanViewModel : ViewModelBase
{
    private readonly AiService _ai;
    private readonly string _sickUserId;
    private readonly DateOnly _date;
    private readonly CalendarEntry? _absentShift;
    private readonly IReadOnlyList<ReplanEngine.ReplanCandidate> _candidates;

    public string PersonHeader { get; }
    public string ShiftLabel { get; }
    public bool HasShift => _absentShift != null;
    public ObservableCollection<ReplanCandidateViewModel> Candidates { get; } = new();
    public bool HasCandidates => Candidates.Count > 0;

    [ObservableProperty] private string _aiRecommendation = "";

    public event Action<ReplanResult?>? Closed;

    public ReplanViewModel(AiService ai, string sickUserId, string personName, DateOnly date,
        CalendarEntry? absentShift, IReadOnlyList<ReplanEngine.ReplanCandidate> candidates)
    {
        _ai = ai;
        _sickUserId = sickUserId;
        _date = date;
        _absentShift = absentShift;
        _candidates = candidates;

        PersonHeader = $"{personName} · {date.ToString("dddd, dd.MM.yyyy", CultureInfo.CurrentCulture)}";
        ShiftLabel = absentShift != null ? absentShift.TimeRange : "";
        for (var i = 0; i < candidates.Count; i++)
            Candidates.Add(new ReplanCandidateViewModel((char)('A' + i), candidates[i]));

        if (HasShift && HasCandidates)
            _ = LoadRecommendationAsync();
    }

    private async Task LoadRecommendationAsync()
    {
        AiRecommendation = Localizer.Instance["Replan_AiThinking"];
        var prompt = ReplanEngine.BuildPrompt(_absentShift!, _date, _candidates);
        var answer = await _ai.SuggestAsync(prompt);
        AiRecommendation = string.IsNullOrWhiteSpace(answer)
            ? Localizer.Instance["Replan_AiUnavailable"]
            : answer.Trim();
    }

    [RelayCommand]
    private void MarkHealthy()
    {
        LogService.Debug("Umplanung: gesund melden ({0})", _sickUserId);
        Closed?.Invoke(new ReplanResult(ReplanAction.MarkHealthy, _date, _sickUserId));
    }

    [RelayCommand]
    private void TakeOver(ReplanCandidateViewModel? candidate)
    {
        if (candidate == null || _absentShift == null) return;
        Closed?.Invoke(new ReplanResult(ReplanAction.TakeOver, _date, _sickUserId, _absentShift.Id, candidate.User));
    }

    [RelayCommand]
    private void Cancel() => Closed?.Invoke(null);
}
