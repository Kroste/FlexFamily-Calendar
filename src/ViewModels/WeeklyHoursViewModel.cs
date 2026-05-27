using System.Globalization;

namespace FlexFamilyCalendar.ViewModels;

/// <summary>Eine Zeile im Wochenstunden-Panel: Ist/Soll als Balken.</summary>
public class WeeklyHoursViewModel
{
    public string DisplayName { get; }
    public double Actual { get; }
    public double Target { get; }

    public WeeklyHoursViewModel(string displayName, double actual, double target)
    {
        DisplayName = displayName;
        Actual = actual;
        Target = target;
    }

    public string Summary
        => $"{Actual.ToString("0.#", CultureInfo.CurrentCulture)} / {Target.ToString("0.#", CultureInfo.CurrentCulture)} h";

    public string Difference
    {
        get
        {
            var diff = Actual - Target;
            var sign = diff >= 0 ? "+" : "−";
            return $"{sign}{Math.Abs(diff).ToString("0.#", CultureInfo.CurrentCulture)} h";
        }
    }

    /// <summary>Grün solange im Soll, orange bei Überschreitung.</summary>
    public string BarColor => Actual > Target ? "#E67E22" : "#27AE60";
}
