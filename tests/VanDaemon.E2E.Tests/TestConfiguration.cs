namespace VanDaemon.E2E.Tests;

/// <summary>
/// Configuration settings for E2E tests
/// </summary>
public static class TestConfiguration
{
    /// <summary>
    /// Base URL for the API (default: http://localhost:5000)
    /// Override with environment variable: VANDAEMON_API_URL
    /// </summary>
    public static string ApiBaseUrl =>
        Environment.GetEnvironmentVariable("VANDAEMON_API_URL") ?? "http://localhost:5000";

    /// <summary>
    /// Base URL for the Web UI (default: http://localhost:5001)
    /// Override with environment variable: VANDAEMON_WEB_URL
    /// </summary>
    public static string WebBaseUrl =>
        Environment.GetEnvironmentVariable("VANDAEMON_WEB_URL") ?? "http://localhost:5001";

    /// <summary>
    /// Default timeout for page navigation (milliseconds)
    /// </summary>
    public static float NavigationTimeout => 30000;

    /// <summary>
    /// Default timeout for waiting for elements (milliseconds)
    /// </summary>
    public static float ElementTimeout => 10000;

    /// <summary>
    /// Timeout for Blazor WASM to initialize (milliseconds)
    /// </summary>
    public static float BlazorInitTimeout => 45000; // Increased from 15s to 45s for WASM startup

    /// <summary>
    /// Whether to run tests in headless mode (default: true)
    /// Override with environment variable: PLAYWRIGHT_HEADLESS=false
    /// </summary>
    public static bool Headless =>
        Environment.GetEnvironmentVariable("PLAYWRIGHT_HEADLESS")?.ToLower() != "false";

    /// <summary>
    /// Whether to slow down operations for debugging (default: 0ms)
    /// Override with environment variable: PLAYWRIGHT_SLOWMO=500
    /// </summary>
    public static float SlowMo =>
        float.TryParse(Environment.GetEnvironmentVariable("PLAYWRIGHT_SLOWMO"), out var slowMo)
            ? slowMo
            : 0;

    /// <summary>
    /// Browser to use for tests (default: chromium)
    /// Options: chromium, firefox, webkit
    /// Override with environment variable: PLAYWRIGHT_BROWSER=firefox
    /// </summary>
    public static string Browser =>
        Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSER")?.ToLower() ?? "chromium";
}
