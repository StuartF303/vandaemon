# Playwright Workflows Reference

## Contents
- Running E2E Tests
- Writing New Tests
- Debugging Failures
- CI/CD Integration

## Running E2E Tests

### Local Development Workflow

```powershell
# 1. Ensure API and Web are built
dotnet build VanDaemon.sln

# 2. Run full E2E suite with auto-started servers
./run-e2e-tests.ps1

# 3. Run specific test class
dotnet test tests/VanDaemon.E2E.Tests --filter "ClassName=DashboardTests"

# 4. Run single test
dotnet test tests/VanDaemon.E2E.Tests --filter "FullyQualifiedName~Dashboard_ShowsTankLevels"
```

### Debug Mode with Visible Browser

```powershell
# Headed mode with 500ms between actions
./run-e2e-tests.ps1 -Headless $false -SlowMo 500

# Generate trace for failed tests
$env:PWDEBUG = "1"
dotnet test tests/VanDaemon.E2E.Tests
```

Copy this checklist and track progress:
- [ ] Build solution: `dotnet build VanDaemon.sln`
- [ ] Start API: `cd src/Backend/VanDaemon.Api && dotnet run`
- [ ] Start Web: `cd src/Frontend/VanDaemon.Web && dotnet run`
- [ ] Verify health: `curl http://localhost:5000/health`
- [ ] Run tests: `dotnet test tests/VanDaemon.E2E.Tests`

## Writing New Tests

### Test File Structure

```csharp
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace VanDaemon.E2E.Tests;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class SettingsTests : PageTest
{
    private const string WebUrl = "http://localhost:5001";
    private const string ApiUrl = "http://localhost:5000";
    
    [SetUp]
    public async Task Setup()
    {
        await Page.GotoAsync($"{WebUrl}/settings");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }
    
    [Test]
    public async Task CanChangeVanModel()
    {
        // Arrange
        var vanModelSelect = Page.GetByLabel("Van Model");
        
        // Act
        await vanModelSelect.ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = "Ford Transit" }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        
        // Assert
        await Expect(Page.Locator(".mud-snackbar")).ToContainTextAsync("Settings saved");
    }
    
    [TearDown]
    public async Task Teardown()
    {
        // Reset to default settings via API
        using var client = new HttpClient();
        await client.PutAsJsonAsync($"{ApiUrl}/api/settings", new { VanModel = "Sprinter LWB" });
    }
}
```

### Page Object Pattern for VanDaemon

```csharp
public class DashboardPage
{
    private readonly IPage _page;
    
    public DashboardPage(IPage page) => _page = page;
    
    public ILocator TankCard(string tankName) => 
        _page.Locator($"[data-testid='tank-card-{tankName}']");
    
    public ILocator TankLevel(string tankName) =>
        TankCard(tankName).Locator("[data-testid='tank-level']");
    
    public ILocator ConnectionStatus =>
        _page.Locator("[data-testid='connection-status']");
    
    public async Task WaitForConnected()
    {
        await Expect(ConnectionStatus).ToHaveAttributeAsync("data-connected", "true", 
            new() { Timeout = 15000 });
    }
    
    public async Task NavigateAsync()
    {
        await _page.GotoAsync("http://localhost:5001");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForConnected();
    }
}
```

### Testing Form Submission

```csharp
[Test]
public async Task CanCreateNewTank()
{
    // Navigate to tanks management
    await Page.GotoAsync($"{WebUrl}/tanks");
    await Page.GetByRole(AriaRole.Button, new() { Name = "Add Tank" }).ClickAsync();
    
    // Fill form
    await Page.GetByLabel("Tank Name").FillAsync("Auxiliary Water");
    await Page.GetByLabel("Tank Type").ClickAsync();
    await Page.GetByRole(AriaRole.Option, new() { Name = "Fresh Water" }).ClickAsync();
    await Page.GetByLabel("Capacity (Liters)").FillAsync("50");
    
    // Submit
    await Page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
    
    // Verify created
    await Expect(Page.Locator(".mud-table")).ToContainTextAsync("Auxiliary Water");
}
```

## Debugging Failures

### Trace Viewer

```powershell
# Enable tracing
$env:PLAYWRIGHT_TRACING = "on"
dotnet test tests/VanDaemon.E2E.Tests

# View trace file
npx playwright show-trace test-results/trace.zip
```

### Screenshot on Failure

```csharp
[TearDown]
public async Task CaptureFailure()
{
    if (TestContext.CurrentContext.Result.Outcome.Status == TestStatus.Failed)
    {
        var testName = TestContext.CurrentContext.Test.Name;
        await Page.ScreenshotAsync(new() 
        { 
            Path = $"test-results/screenshots/{testName}.png",
            FullPage = true 
        });
    }
}
```

### Console Log Collection

```csharp
[SetUp]
public void SetupConsoleCapture()
{
    Page.Console += (_, msg) =>
    {
        if (msg.Type == "error")
        {
            TestContext.WriteLine($"Browser Error: {msg.Text}");
        }
    };
}
```

## CI/CD Integration

### GitHub Actions Workflow

```yaml
# .github/workflows/e2e-tests.yml
name: E2E Tests

on:
  push:
    branches: [main]
  pull_request:

jobs:
  e2e:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      
      - name: Install Playwright
        run: |
          dotnet build tests/VanDaemon.E2E.Tests
          pwsh tests/VanDaemon.E2E.Tests/bin/Debug/net10.0/playwright.ps1 install --with-deps chromium
      
      - name: Start Services
        run: |
          dotnet run --project src/Backend/VanDaemon.Api &
          dotnet run --project src/Frontend/VanDaemon.Web &
          sleep 10
      
      - name: Run E2E Tests
        run: dotnet test tests/VanDaemon.E2E.Tests --logger "trx;LogFileName=results.trx"
      
      - name: Upload Test Results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: e2e-results
          path: |
            tests/VanDaemon.E2E.Tests/TestResults/
            test-results/screenshots/
```

### Docker Compose Test Environment

```yaml
# docker-compose.test.yml
services:
  api:
    build:
      context: .
      dockerfile: docker/Dockerfile.api
    ports:
      - "5000:80"
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:80/health"]
      interval: 5s
      timeout: 3s
      retries: 10

  web:
    build:
      context: .
      dockerfile: docker/Dockerfile.web
    ports:
      - "5001:80"
    depends_on:
      api:
        condition: service_healthy

  e2e:
    build:
      context: .
      dockerfile: docker/Dockerfile.e2e
    depends_on:
      - web
    environment:
      - API_URL=http://api:80
      - WEB_URL=http://web:80
```

1. Start test environment: `docker compose -f docker-compose.test.yml up -d`
2. Validate services: `curl http://localhost:5000/health`
3. If health check fails, check logs: `docker compose logs api`
4. Only proceed when all services report healthy
5. Run tests: `docker compose -f docker-compose.test.yml run e2e`