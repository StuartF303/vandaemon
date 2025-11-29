# VanDaemon E2E Tests

End-to-end tests for VanDaemon using Playwright for .NET.

## Overview

This test project uses **Playwright** to test the VanDaemon Blazor WebAssembly UI in real browsers. Tests verify user workflows, real-time updates via SignalR, responsive design, and overall application functionality.

## Prerequisites

1. **.NET 10 SDK** - Required to run tests
2. **Playwright browsers** - Automatically installed during first build
3. **VanDaemon application running** - Both API and Web must be running for tests to pass

## Quick Start

### Option 1: Automated Script (Easiest)

Use the automated PowerShell script that handles everything:

```powershell
# From solution root - runs everything automatically
.\run-e2e-tests.ps1

# Watch tests run in visible browser (debugging)
.\run-e2e-tests.ps1 -Headless $false -SlowMo 500

# Run with Firefox
.\run-e2e-tests.ps1 -Browser firefox
```

The script will:
1. Build the solution
2. Start API and Web UI in the background
3. Wait for services to be ready
4. Run E2E tests
5. Clean up processes when done

### Option 2: Manual Setup

If you prefer manual control:

**Step 1: Install Playwright Browsers (First Time Only)**

```powershell
cd tests\VanDaemon.E2E.Tests
dotnet build
pwsh bin\Debug\net10.0\playwright.ps1 install
```

**Step 2: Start VanDaemon Application**

Terminal 1 - API:
```powershell
cd src\Backend\VanDaemon.Api
dotnet run
```

Terminal 2 - Web UI:
```powershell
cd src\Frontend\VanDaemon.Web
dotnet run
```

Wait for both to display "Now listening on..." messages.

**Step 3: Run E2E Tests**

Terminal 3:
```powershell
# From solution root
dotnet test tests\VanDaemon.E2E.Tests\VanDaemon.E2E.Tests.csproj

# Or run all tests including E2E
dotnet test VanDaemon.sln
```

## Configuration

E2E tests can be configured via environment variables:

| Variable | Default | Description |
|----------|---------|-------------|
| `VANDAEMON_API_URL` | `http://localhost:5000` | Base URL for the API |
| `VANDAEMON_WEB_URL` | `http://localhost:5001` | Base URL for the Web UI |
| `PLAYWRIGHT_HEADLESS` | `true` | Run browsers in headless mode |
| `PLAYWRIGHT_SLOWMO` | `0` | Slow down operations (ms) for debugging |
| `PLAYWRIGHT_BROWSER` | `chromium` | Browser to use (`chromium`, `firefox`, `webkit`) |

### Example: Run Tests with Firefox in Headed Mode

**Windows (PowerShell):**
```powershell
$env:PLAYWRIGHT_BROWSER="firefox"
$env:PLAYWRIGHT_HEADLESS="false"
dotnet test tests/VanDaemon.E2E.Tests/VanDaemon.E2E.Tests.csproj
```

**Linux/Mac (Bash):**
```bash
export PLAYWRIGHT_BROWSER=firefox
export PLAYWRIGHT_HEADLESS=false
dotnet test tests/VanDaemon.E2E.Tests/VanDaemon.E2E.Tests.csproj
```

### Example: Test Against Docker Deployment

**Windows (PowerShell):**
```powershell
$env:VANDAEMON_API_URL="http://localhost:5000"
$env:VANDAEMON_WEB_URL="http://localhost:8080"
dotnet test tests/VanDaemon.E2E.Tests/VanDaemon.E2E.Tests.csproj
```

**Linux/Mac (Bash):**
```bash
export VANDAEMON_API_URL=http://localhost:5000
export VANDAEMON_WEB_URL=http://localhost:8080
dotnet test tests/VanDaemon.E2E.Tests/VanDaemon.E2E.Tests.csproj
```

## Test Structure

```
VanDaemon.E2E.Tests/
├── PlaywrightTestBase.cs      # Base class with setup/teardown
├── TestConfiguration.cs        # Environment variable configuration
├── DashboardTests.cs           # Tests for main dashboard page
├── TanksPageTests.cs           # Tests for tanks monitoring page
└── ControlsPageTests.cs        # Tests for controls management page
```

## Writing New Tests

### 1. Create Test Class

```csharp
using FluentAssertions;
using Microsoft.Playwright;

namespace VanDaemon.E2E.Tests;

public class MyPageTests : PlaywrightTestBase
{
    [Fact]
    public async Task MyPage_ShouldLoad()
    {
        // Arrange & Act
        await NavigateToAppAsync("/mypage");

        // Assert
        var heading = await Page!.QuerySelectorAsync("h1");
        heading.Should().NotBeNull();
    }
}
```

### 2. Use Helper Methods

**PlaywrightTestBase** provides:

- `NavigateToAppAsync(path)` - Navigate to VanDaemon page and wait for Blazor
- `WaitForBlazorAsync()` - Wait for Blazor WASM initialization
- `WaitForSignalRConnectionAsync()` - Wait for WebSocket connection
- `TakeScreenshotAsync(name)` - Capture screenshot for debugging
- `Page`, `Browser`, `Context` - Playwright objects

### 3. Wait for Elements

```csharp
// Wait for element to appear
var button = await Page!.WaitForSelectorAsync(".mud-button");

// Wait for network to be idle
await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

// Wait for custom condition
await Page.WaitForFunctionAsync("() => window.myApp.isReady");
```

### 4. Interact with Elements

```csharp
// Click
await Page!.ClickAsync("button:has-text('Submit')");

// Type text
await Page.FillAsync("input[name='tankName']", "Fresh Water");

// Select option
await Page.SelectOptionAsync("select[name='type']", "FreshWater");

// Check checkbox/toggle
await Page.CheckAsync("input[type='checkbox']");
```

### 5. Assertions with FluentAssertions

```csharp
// Element existence
var element = await Page!.QuerySelectorAsync(".mud-card");
element.Should().NotBeNull();

// Text content
var text = await element.TextContentAsync();
text.Should().Contain("Tank");

// URL navigation
Page.Url.Should().Contain("/tanks");
```

## Test Categories

### Dashboard Tests
- Application loading and initialization
- Van diagram rendering
- Navigation functionality
- SignalR connection
- Theme toggling
- Responsive design

### Tanks Page Tests
- Tank cards display
- Real-time level updates
- Progress indicators
- Tank type labels
- Add/edit tank workflows
- Mobile responsiveness

### Controls Page Tests
- Control cards display
- Toggle switch interactions
- Dimmer slider functionality
- Real-time state updates
- Icon display
- Control type handling

## Debugging Tests

### View Tests in Browser

Run with headed mode to watch tests execute:

```bash
# Windows
$env:PLAYWRIGHT_HEADLESS="false"
$env:PLAYWRIGHT_SLOWMO="500"
dotnet test tests/VanDaemon.E2E.Tests/VanDaemon.E2E.Tests.csproj

# Linux/Mac
export PLAYWRIGHT_HEADLESS=false
export PLAYWRIGHT_SLOWMO=500
dotnet test tests/VanDaemon.E2E.Tests/VanDaemon.E2E.Tests.csproj
```

### Capture Screenshots

Add to your test:

```csharp
await TakeScreenshotAsync("test-name-step1");
```

Screenshots are saved to: `bin/Debug/net10.0/screenshots/`

### Enable Playwright Tracing

```csharp
// In PlaywrightTestBase.InitializeAsync()
await Context.Tracing.StartAsync(new()
{
    Screenshots = true,
    Snapshots = true,
    Sources = true
});

// In PlaywrightTestBase.DisposeAsync()
await Context.Tracing.StopAsync(new()
{
    Path = "trace.zip"
});
```

View trace at: https://trace.playwright.dev

### Check Console Logs

```csharp
var messages = new List<string>();
Page!.Console += (_, msg) => messages.Add($"{msg.Type}: {msg.Text}");

// Run your test...

messages.Should().NotContain(m => m.Contains("error"));
```

## CI/CD Integration

### GitHub Actions Example

```yaml
name: E2E Tests

on: [push, pull_request]

jobs:
  e2e:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '10.0.x'

      - name: Install dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore

      - name: Install Playwright browsers
        run: pwsh tests/VanDaemon.E2E.Tests/bin/Debug/net10.0/playwright.ps1 install

      - name: Start API
        run: dotnet run --project src/Backend/VanDaemon.Api/VanDaemon.Api.csproj &

      - name: Start Web
        run: dotnet run --project src/Frontend/VanDaemon.Web/VanDaemon.Web.csproj &

      - name: Wait for services
        run: sleep 10

      - name: Run E2E tests
        run: dotnet test tests/VanDaemon.E2E.Tests/VanDaemon.E2E.Tests.csproj
```

## Troubleshooting

### Tests Fail with "Application did not load"

**Solution:** Ensure both API and Web are running:
```bash
# Terminal 1
cd src/Backend/VanDaemon.Api && dotnet run

# Terminal 2
cd src/Frontend/VanDaemon.Web && dotnet run
```

### Browsers Not Found

**Solution:** Reinstall Playwright browsers:
```bash
cd tests/VanDaemon.E2E.Tests
pwsh bin/Debug/net10.0/playwright.ps1 install
```

### SignalR Tests Fail

**Solution:** Increase timeout or wait for connection:
```csharp
await WaitForSignalRConnectionAsync();
await Task.Delay(2000); // Wait for initial data
```

### Tests Pass Locally but Fail in CI

**Solution:** Add explicit waits and increase timeouts:
```csharp
// In TestConfiguration.cs, increase timeouts
public static float NavigationTimeout => 60000; // 60 seconds for CI
```

### Element Not Found

**Solution:** Use more robust selectors:
```csharp
// Instead of specific class names
await Page.QuerySelectorAsync(".mud-button-123");

// Use semantic selectors
await Page.QuerySelectorAsync("button:has-text('Submit')");
await Page.QuerySelectorAsync("[data-testid='submit-button']");
```

## Best Practices

1. **Wait for Blazor** - Always call `await WaitForBlazorAsync()` after navigation
2. **Use Semantic Selectors** - Prefer `text=`, `has-text()`, `aria-label` over CSS classes
3. **Add data-testid Attributes** - Add to important elements for reliable selection
4. **Independent Tests** - Each test should be isolated and not depend on others
5. **Clean Up State** - Reset data between tests if needed
6. **Explicit Waits** - Use `WaitForSelectorAsync` instead of `Task.Delay`
7. **Handle Async** - Blazor WASM and SignalR are async - wait for state changes
8. **Screenshot on Failure** - Capture evidence when tests fail
9. **Test Real Scenarios** - Test user workflows, not implementation details
10. **Run in Multiple Browsers** - Test cross-browser compatibility

## Resources

- [Playwright for .NET Documentation](https://playwright.dev/dotnet/)
- [MudBlazor Component Selectors](https://mudblazor.com/)
- [VanDaemon Architecture Guide](../../CLAUDE.md)
- [Blazor Testing Best Practices](https://learn.microsoft.com/en-us/aspnet/core/blazor/test)

## Support

If you encounter issues with E2E tests:

1. Check that both API (port 5000) and Web (port 5001) are running
2. Verify Playwright browsers are installed
3. Review test output and screenshots in `bin/Debug/net10.0/screenshots/`
4. Enable headed mode to watch test execution
5. Check application logs for errors

## Future Enhancements

- [ ] Add visual regression testing with screenshot comparison
- [ ] Add performance testing (page load times, bundle sizes)
- [ ] Test API integration tests with mocked hardware
- [ ] Add accessibility testing (WCAG compliance)
- [ ] Test offline PWA functionality
- [ ] Add mobile device emulation tests
- [ ] Test SignalR reconnection scenarios
- [ ] Add load testing for multiple concurrent users
