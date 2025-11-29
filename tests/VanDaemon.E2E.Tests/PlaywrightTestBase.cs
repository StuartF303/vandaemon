using Microsoft.Playwright;

namespace VanDaemon.E2E.Tests;

/// <summary>
/// Base class for Playwright E2E tests with setup/teardown
/// </summary>
public abstract class PlaywrightTestBase : IAsyncLifetime
{
    protected IPlaywright? Playwright { get; private set; }
    protected IBrowser? Browser { get; private set; }
    protected IBrowserContext? Context { get; private set; }
    protected IPage? Page { get; private set; }

    /// <summary>
    /// Initialize Playwright, browser, and page before each test
    /// </summary>
    public async Task InitializeAsync()
    {
        // Create Playwright instance
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();

        // Launch browser based on configuration
        Browser = TestConfiguration.Browser switch
        {
            "firefox" => await Playwright.Firefox.LaunchAsync(new()
            {
                Headless = TestConfiguration.Headless,
                SlowMo = TestConfiguration.SlowMo
            }),
            "webkit" => await Playwright.Webkit.LaunchAsync(new()
            {
                Headless = TestConfiguration.Headless,
                SlowMo = TestConfiguration.SlowMo
            }),
            _ => await Playwright.Chromium.LaunchAsync(new()
            {
                Headless = TestConfiguration.Headless,
                SlowMo = TestConfiguration.SlowMo
            })
        };

        // Create browser context with viewport settings
        Context = await Browser.NewContextAsync(new()
        {
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
            IgnoreHTTPSErrors = true
        });

        // Create page
        Page = await Context.NewPageAsync();

        // Set default timeouts
        Page.SetDefaultNavigationTimeout(TestConfiguration.NavigationTimeout);
        Page.SetDefaultTimeout(TestConfiguration.ElementTimeout);
    }

    /// <summary>
    /// Clean up Playwright resources after each test
    /// </summary>
    public async Task DisposeAsync()
    {
        if (Page != null)
            await Page.CloseAsync();

        if (Context != null)
            await Context.CloseAsync();

        if (Browser != null)
            await Browser.CloseAsync();

        Playwright?.Dispose();
    }

    /// <summary>
    /// Navigate to the VanDaemon web application
    /// </summary>
    protected async Task NavigateToAppAsync(string path = "/")
    {
        var url = $"{TestConfiguration.WebBaseUrl}{path}";
        await Page!.GotoAsync(url);
        await WaitForBlazorAsync();
    }

    /// <summary>
    /// Wait for Blazor WASM to finish loading
    /// Waits for the Blazor reconnect UI to disappear (indicates app is ready)
    /// </summary>
    protected async Task WaitForBlazorAsync()
    {
        try
        {
            // Wait for MudBlazor components to be loaded (indicates Blazor is ready)
            await Page!.WaitForSelectorAsync(".mud-layout", new()
            {
                Timeout = TestConfiguration.BlazorInitTimeout
            });
        }
        catch (TimeoutException)
        {
            throw new TimeoutException(
                $"Blazor application did not load within {TestConfiguration.BlazorInitTimeout}ms. " +
                "Make sure the VanDaemon web application is running at " + TestConfiguration.WebBaseUrl);
        }
    }

    /// <summary>
    /// Wait for SignalR connection to be established
    /// </summary>
    protected async Task WaitForSignalRConnectionAsync()
    {
        // Wait for network to be idle (SignalR WebSocket connection established)
        await Page!.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Take a screenshot for debugging (saves to test output directory)
    /// </summary>
    protected async Task TakeScreenshotAsync(string name)
    {
        var screenshotPath = Path.Combine(
            AppContext.BaseDirectory,
            "screenshots",
            $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}.png");

        Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath)!);
        await Page!.ScreenshotAsync(new() { Path = screenshotPath, FullPage = true });
    }
}
