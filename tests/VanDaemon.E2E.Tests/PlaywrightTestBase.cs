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

        // Capture browser console + uncaught page errors so a Blazor-load failure is diagnosable
        // (otherwise a WASM boot error is invisible — the test just times out waiting for the layout).
        Page.Console += (_, msg) => _browserLog.Add($"[console:{msg.Type}] {msg.Text}");
        Page.PageError += (_, err) => _browserLog.Add($"[pageerror] {err}");

        // Set default timeouts
        Page.SetDefaultNavigationTimeout(TestConfiguration.NavigationTimeout);
        Page.SetDefaultTimeout(TestConfiguration.ElementTimeout);
    }

    /// <summary>Browser console + page-error messages captured for failure diagnostics.</summary>
    private readonly List<string> _browserLog = new();

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
            // Wait for the app's root layout to render (indicates Blazor WASM has booted). This app
            // uses a custom grid layout (MainLayout's ".layout-container"), NOT MudBlazor's <MudLayout>,
            // so ".mud-layout" never appears — waiting for it was the cause of every E2E timeout.
            await Page!.WaitForSelectorAsync(".layout-container", new()
            {
                Timeout = TestConfiguration.BlazorInitTimeout
            });
        }
        catch (TimeoutException)
        {
            // Dump what the browser actually saw so the failure is diagnosable, not just a timeout.
            var appHtml = await SafeInnerHtmlAsync("#app");
            var errorUi = await SafeInnerHtmlAsync("#blazor-error-ui");
            Console.WriteLine("===== Blazor load diagnostics =====");
            Console.WriteLine($"URL: {Page!.Url}");
            Console.WriteLine($"#app innerHTML: {appHtml}");
            Console.WriteLine($"#blazor-error-ui innerHTML: {errorUi}");
            Console.WriteLine("Browser log:");
            foreach (var line in _browserLog)
                Console.WriteLine("  " + line);
            Console.WriteLine("===================================");

            throw new TimeoutException(
                $"Blazor application did not load within {TestConfiguration.BlazorInitTimeout}ms. " +
                "Make sure the VanDaemon web application is running at " + TestConfiguration.WebBaseUrl);
        }
    }

    private async Task<string> SafeInnerHtmlAsync(string selector)
    {
        try
        {
            return await Page!.InnerHTMLAsync(selector);
        }
        catch (Exception ex)
        {
            return $"<unavailable: {ex.GetType().Name}>";
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
