namespace FlexFamilyCalendar.Services;

/// <summary>
/// Zentraler Schalter für UI-Hover-Hinweise. Wird beim Login/Profil-Refresh aus dem
/// User-Objekt aktualisiert. Die Attached Property <see cref="Views.Hint"/> hängt sich an
/// <see cref="EnabledChanged"/>, damit ToolTip.Tip live umgeschaltet werden kann, ohne
/// dass Views neu gebunden werden müssen.
/// </summary>
public static class HintService
{
    private static bool _enabled = true;

    public static bool IsEnabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            _enabled = value;
            EnabledChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    public static event EventHandler? EnabledChanged;
}
