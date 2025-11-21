using VanDaemon.Core.Entities;

namespace VanDaemon.Web.Services;

public class SettingsStateService
{
    public event Action? SettingsChanged;

    public void NotifySettingsChanged()
    {
        SettingsChanged?.Invoke();
    }
}
