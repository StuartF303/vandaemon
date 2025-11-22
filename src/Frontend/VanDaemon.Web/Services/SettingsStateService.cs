using VanDaemon.Core.Entities;
using Microsoft.JSInterop;

namespace VanDaemon.Web.Services;

public class SettingsStateService
{
    private bool _isDarkMode = false;
    private IJSRuntime? _jsRuntime;

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
                ApplyThemeAsync().ConfigureAwait(false);
            }
        }
    }

    public void SetJSRuntime(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    private async Task ApplyThemeAsync()
    {
        if (_jsRuntime != null)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("eval",
                    $"document.documentElement.setAttribute('data-theme', '{(_isDarkMode ? "dark" : "light")}')");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error applying theme: {ex.Message}");
            }
        }
    }

    public void NotifySettingsChanged()
    {
        SettingsChanged?.Invoke();
    }
}
