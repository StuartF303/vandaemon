using VanDaemon.Core.Entities;
using VanDaemon.Core.Enums;
using Microsoft.JSInterop;

namespace VanDaemon.Web.Services;

public class SettingsStateService
{
    private bool _isDarkMode = false;
    private IJSRuntime? _jsRuntime;
    private ThemeMode _currentThemeMode = ThemeMode.Manual;
    private Theme _manualTheme = Theme.Light;
    private bool _headlightsOn = false;
    private DotNetObjectReference<SettingsStateService>? _dotNetReference;

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

    /// <summary>
    /// Initialize theme system with configuration and start listening for browser/headlight changes
    /// </summary>
    public async Task InitializeThemeModeAsync(ThemeMode mode, Theme manualTheme, bool headlightsOn = false)
    {
        _currentThemeMode = mode;
        _manualTheme = manualTheme;
        _headlightsOn = headlightsOn;

        // Apply theme based on mode
        await UpdateThemeBasedOnModeAsync();

        // Set up browser dark mode listener if in BrowserAuto mode
        if (mode == ThemeMode.BrowserAuto && _jsRuntime != null)
        {
            await StartBrowserDarkModeListenerAsync();
        }
    }

    /// <summary>
    /// Update headlights status (called from telemetry)
    /// </summary>
    public async Task UpdateHeadlightsStatusAsync(bool headlightsOn)
    {
        if (_headlightsOn != headlightsOn)
        {
            _headlightsOn = headlightsOn;

            // Only update theme if in HeadlightsAuto mode
            if (_currentThemeMode == ThemeMode.HeadlightsAuto)
            {
                await UpdateThemeBasedOnModeAsync();
            }
        }
    }

    /// <summary>
    /// Update theme mode and manual theme preference
    /// </summary>
    public async Task UpdateThemeModeAsync(ThemeMode mode, Theme manualTheme)
    {
        bool modeChanged = _currentThemeMode != mode;
        _currentThemeMode = mode;
        _manualTheme = manualTheme;

        // Stop browser listener if switching away from BrowserAuto
        if (modeChanged && mode != ThemeMode.BrowserAuto && _jsRuntime != null)
        {
            await StopBrowserDarkModeListenerAsync();
        }

        // Start browser listener if switching to BrowserAuto
        if (modeChanged && mode == ThemeMode.BrowserAuto && _jsRuntime != null)
        {
            await StartBrowserDarkModeListenerAsync();
        }

        // Apply theme based on new mode
        await UpdateThemeBasedOnModeAsync();
    }

    /// <summary>
    /// Called from JavaScript when browser dark mode preference changes
    /// </summary>
    [JSInvokable]
    public async Task OnBrowserDarkModeChanged(bool isDark)
    {
        Console.WriteLine($"Browser dark mode changed: {isDark}");

        // Only update if in BrowserAuto mode
        if (_currentThemeMode == ThemeMode.BrowserAuto)
        {
            IsDarkMode = isDark;
        }
    }

    private async Task UpdateThemeBasedOnModeAsync()
    {
        bool shouldBeDark = _currentThemeMode switch
        {
            ThemeMode.Manual => _manualTheme == Theme.Dark,
            ThemeMode.BrowserAuto => await GetBrowserDarkModePreferenceAsync(),
            ThemeMode.HeadlightsAuto => _headlightsOn, // Dark when headlights on
            _ => false
        };

        IsDarkMode = shouldBeDark;
    }

    private async Task<bool> GetBrowserDarkModePreferenceAsync()
    {
        if (_jsRuntime == null) return false;

        try
        {
            // Check if browser supports matchMedia and prefers dark mode
            var result = await _jsRuntime.InvokeAsync<bool>("vandaemonCheckDarkMode");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting browser dark mode preference: {ex.Message}");
            return false;
        }
    }

    private async Task StartBrowserDarkModeListenerAsync()
    {
        if (_jsRuntime == null) return;

        try
        {
            // Create .NET reference for callbacks
            _dotNetReference = DotNetObjectReference.Create(this);

            // Set up JavaScript listener for dark mode changes
            await _jsRuntime.InvokeVoidAsync("vandaemonStartDarkModeListener", _dotNetReference);

            Console.WriteLine("Started browser dark mode listener");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error starting browser dark mode listener: {ex.Message}");
        }
    }

    private async Task StopBrowserDarkModeListenerAsync()
    {
        if (_jsRuntime == null) return;

        try
        {
            await _jsRuntime.InvokeVoidAsync("vandaemonStopDarkModeListener");

            _dotNetReference?.Dispose();
            _dotNetReference = null;

            Console.WriteLine("Stopped browser dark mode listener");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error stopping browser dark mode listener: {ex.Message}");
        }
    }

    private async Task ApplyThemeAsync()
    {
        if (_jsRuntime != null)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("vandaemonSetTheme", _isDarkMode ? "dark" : "light");
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
