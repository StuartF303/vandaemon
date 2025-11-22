using VanDaemon.Core.Entities;

namespace VanDaemon.Web.Services;

public class SettingsStateService
{
    private bool _isDarkMode = false;

    public event Action? SettingsChanged;
    public event Action<bool>? DarkModeChanged;

    public bool IsDarkMode
    {
        get => _isDarkMode;
        set
        {
            if (_isDarkMode != value)
            {
                _isDarkMode = value;
                DarkModeChanged?.Invoke(_isDarkMode);
            }
        }
    }

    public void NotifySettingsChanged()
    {
        SettingsChanged?.Invoke();
    }
}
