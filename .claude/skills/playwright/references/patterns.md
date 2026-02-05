# Playwright Patterns Reference

## Contents
- Locator Strategies for Blazor
- MudBlazor Component Patterns
- SignalR Real-Time Testing
- Anti-Patterns

## Locator Strategies for Blazor

### Data-TestId Pattern (Recommended)

```csharp
// In Blazor component
<MudText data-testid="tank-level-@tank.Id">@tank.CurrentLevel%</MudText>

// In test
var tankLevel = Page.GetByTestId($"tank-level-{tankId}");
await Expect(tankLevel).ToContainTextAsync("75%");
```

### Semantic Locators for MudBlazor

```csharp
// GOOD - Uses role and accessible name
var saveButton = Page.GetByRole(AriaRole.Button, new() { Name = "Save Settings" });

// GOOD - Uses label association
var vanModelSelect = Page.GetByLabel("Van Model");

// AVOID - Brittle CSS selectors
var button = Page.Locator(".mud-button-root.mud-button-filled");
```

### Handling Blazor Re-renders

```csharp
// GOOD - Locator auto-waits and re-queries
var dashboardContent = Page.Locator(".dashboard-container");
await dashboardContent.WaitForAsync(new() { State = WaitForSelectorState.Visible });

// BAD - ElementHandle becomes stale after re-render
var element = await Page.QuerySelectorAsync(".dashboard-container");
// Element may be detached after SignalR update
```

## MudBlazor Component Patterns

### Testing MudSelect Dropdown

```csharp
public async Task SelectVanModel(string modelName)
{
    // Open dropdown
    var select = Page.Locator(".mud-select").Filter(new() { HasText = "Van Model" });
    await select.ClickAsync();
    
    // Wait for popover and select option
    var option = Page.Locator(".mud-popover-open .mud-list-item").Filter(new() { HasText = modelName });
    await option.ClickAsync();
    
    // Verify selection
    await Expect(select).ToContainTextAsync(modelName);
}
```

### Testing MudSlider

```csharp
public async Task SetDimmerLevel(string controlName, int percentage)
{
    var sliderContainer = Page.Locator($"[data-testid='control-{controlName}']");
    var slider = sliderContainer.Locator("input[type='range']");
    
    // Set value directly - more reliable than drag
    await slider.FillAsync(percentage.ToString());
    
    // Verify label updated
    await Expect(sliderContainer.Locator(".mud-slider-value")).ToContainTextAsync($"{percentage}%");
}
```

### Testing MudTable with Pagination

```csharp
public async Task VerifyAlertTable()
{
    var table = Page.Locator(".mud-table");
    
    // Check row count
    var rows = table.Locator("tbody tr");
    await Expect(rows).ToHaveCountAsync(10); // Default page size
    
    // Navigate to next page
    await Page.GetByRole(AriaRole.Button, new() { Name = "Go to next page" }).ClickAsync();
    
    // Wait for table refresh
    await table.Locator("tbody").WaitForAsync();
}
```

## SignalR Real-Time Testing

### Waiting for Hub Connection

```csharp
public async Task WaitForSignalRConnected()
{
    // VanDaemon shows green badge when connected
    var connectionBadge = Page.Locator("[data-testid='connection-status']");
    await Expect(connectionBadge).ToHaveClassAsync(new Regex(".*connected.*"), new() { Timeout = 15000 });
}
```

### Testing Tank Level Updates

```csharp
[Fact]
public async Task TankLevel_UpdatesInRealTime()
{
    await Page.GotoAsync("http://localhost:5001");
    await WaitForSignalRConnected();
    
    var freshWaterLevel = Page.GetByTestId("tank-fresh-water");
    var initialLevel = await freshWaterLevel.TextContentAsync();
    
    // Trigger refresh via API
    var client = new HttpClient();
    await client.PostAsync("http://localhost:5000/api/tanks/refresh", null);
    
    // Wait for SignalR to push update (may be same or different value)
    await Page.WaitForFunctionAsync($@"() => {{
        const el = document.querySelector('[data-testid=""tank-fresh-water""]');
        return el && el.getAttribute('data-updated') === 'true';
    }}", new() { Timeout = 10000 });
}
```

## Anti-Patterns

### WARNING: Hardcoded Waits

**The Problem:**

```csharp
// BAD - Arbitrary sleep
await Page.GotoAsync(url);
await Task.Delay(3000); // "Wait for page to load"
await Page.ClickAsync(".submit-button");
```

**Why This Breaks:**
1. Slow environments need more time, fast environments waste time
2. Tests become flaky on CI servers
3. No feedback on what you're actually waiting for

**The Fix:**

```csharp
// GOOD - Wait for specific condition
await Page.GotoAsync(url);
await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
await Page.Locator(".submit-button").WaitForAsync();
await Page.ClickAsync(".submit-button");
```

### WARNING: Testing Implementation Details

**The Problem:**

```csharp
// BAD - Testing internal MudBlazor CSS structure
var tank = Page.Locator(".mud-card .mud-card-content .mud-typography-body1:nth-child(2)");
```

**Why This Breaks:**
1. MudBlazor updates may change internal structure
2. Test fails without any actual bug in application
3. Impossible to understand what's being tested

**The Fix:**

```csharp
// GOOD - Test user-visible behavior with data-testid
var tank = Page.GetByTestId("tank-fresh-water-level");

// Or use semantic locators
var tank = Page.GetByRole(AriaRole.Status, new() { Name = "Fresh Water Level" });
```

### WARNING: Missing Async Assertions

**The Problem:**

```csharp
// BAD - Synchronous check doesn't wait
var text = await Page.Locator(".status").TextContentAsync();
Assert.Equal("Connected", text); // May fail during transition
```

**Why This Breaks:**
1. Blazor may still be rendering
2. SignalR connection may be establishing
3. Race condition between test and app

**The Fix:**

```csharp
// GOOD - Expect auto-retries until timeout
await Expect(Page.Locator(".status")).ToHaveTextAsync("Connected");