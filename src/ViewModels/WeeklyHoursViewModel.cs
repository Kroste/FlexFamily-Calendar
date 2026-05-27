using FlexFamilyCalendar.Localization;
using System.Globalization;

namespace FlexFamilyCalendar.ViewModels;

/// <summary>Eine Zeile im Wochenstunden-Panel: Ist/Soll als Balken (+ optionale Arbeitszeit-Limit-Warnung).</summary>
public class WeeklyHoursViewModel
{
    public string DisplayName { get; }
    public double Actual { get; }
    public double Target { get; }
    public double WorkedHours { get; }
    public double MaxWeeklyHours { get; }

    public WeeklyHoursViewModel(string displayName, double actual, double target,
        double workedHours = 0, double maxWeeklyHours = 0)
    {
        DisplayName = displayName;
        Actual = actual;
        Target = target;
        WorkedHours = workedHours;
        MaxWeeklyHours = maxWeeklyHours;
    }

    /// <summary>Hat die Person ein Wochenstunden-Soll? Ohne Soll nur Ist-Anzeige.</summary>
    public bool HasTarget => Target > 0;

    /// <summary>Überschreitet die gearbeitete Zeit das gesetzliche Wochenlimit?</summary>
    public bool IsOverLimit => MaxWeeklyHours > 0 && WorkedHours > MaxWeeklyHours;

    public string LimitWarning => IsOverLimit
        ? $"⚠ {Localizer.Instance["Cal_OverWeeklyLimit"]}: {H(WorkedHours)} / {H(MaxWeeklyHours)} h"
        : "";

    private string H(double v) => v.ToString("0.#", CultureInfo.CurrentCulture);

    public string Summary
        => HasTarget ? $"{H(Actual)} / {H(Target)} h" : $"{H(Actual)} h";

    public string Difference
    {
        get
        {
            var diff = Actual - Target;
            var sign = diff >= 0 ? "+" : "−";
            return $"{sign}{H(Math.Abs(diff))} h";
        }
    }

    /// <summary>Grün solange im Soll, orange bei Überschreitung; neutral ohne Soll.</summary>
    public string BarColor => !HasTarget ? "#7F8C8D" : Actual > Target ? "#E67E22" : "#27AE60";
}
