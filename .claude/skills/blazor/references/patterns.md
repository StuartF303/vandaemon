# Blazor Patterns Reference

## Contents
- Component Lifecycle
- State Management
- SignalR Integration
- HTTP API Calls
- Anti-Patterns

## Component Lifecycle

### OnInitializedAsync for Data Loading

```razor
@code {
    private List<Control>? _controls;
    private bool _isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _controls = await Http.GetFromJsonAsync<List<Control>>("api/controls");
        }
        finally
        {
            _isLoading = false;
        }
    }
}
```

### OnParametersSetAsync for Parameter Changes

```razor
@code {
    [Parameter]
    public Guid TankId { get; set; }

    private Tank? _tank;

    protected override async Task OnParametersSetAsync()
    {
        // Called when TankId parameter changes
        _tank = await Http.GetFromJsonAsync<Tank>($"api/tanks/{TankId}");
    }
}
```

## State Management

### Singleton State Service Pattern

VanDaemon uses `SettingsStateService` as a singleton for shared state:

```csharp
// Services/SettingsStateService.cs
public class SettingsStateService
{
    public event Action? OnChange;
    
    private SystemConfiguration _settings = new();
    
    public SystemConfiguration Settings
    {
        get => _settings;
        set
        {
            _settings = value;
            OnChange?.Invoke();
        }
    }
}
```

```razor
@inject SettingsStateService SettingsState
@implements IDisposable

@code {
    protected override void OnInitialized()
    {
        SettingsState.OnChange += StateHasChanged;
    }

    public void Dispose()
    {
        SettingsState.OnChange -= StateHasChanged;
    }
}
```

### WARNING: Direct Field Mutation Without StateHasChanged

**The Problem:**

```razor
// BAD - UI won't update
HubConnection.On<Guid, double>("TankLevelUpdated", (id, level) =>
{
    var tank = _tanks.First(t => t.Id == id);
    tank.CurrentLevel = level;  // UI stays stale
});
```

**Why This Breaks:**
1. SignalR callbacks run on a different thread than Blazor's render context
2. Blazor has no way to know the data changed
3. User sees outdated values until page refresh

**The Fix:**

```razor
// GOOD - Proper UI update
HubConnection.On<Guid, double>("TankLevelUpdated", (id, level) =>
{
    var tank = _tanks.First(t => t.Id == id);
    tank.CurrentLevel = level;
    InvokeAsync(StateHasChanged);  // Marshal to render thread
});
```

## SignalR Integration

### Subscription Pattern from Dashboard

```razor
@inject HubConnection HubConnection

@code {
    private readonly List<IDisposable> _subscriptions = new();

    protected override async Task OnInitializedAsync()
    {
        _subscriptions.Add(HubConnection.On<Guid, double, string>(
            "TankLevelUpdated", HandleTankUpdate));
        
        _subscriptions.Add(HubConnection.On<Guid, object, string>(
            "ControlStateChanged", HandleControlUpdate));

        await HubConnection.InvokeAsync("SubscribeToTanks");
        await HubConnection.InvokeAsync("SubscribeToControls");
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var sub in _subscriptions)
            sub.Dispose();
    }
}
```

### WARNING: Forgetting to Subscribe to Groups

**The Problem:**

```razor
// BAD - Never receives updates
protected override async Task OnInitializedAsync()
{
    HubConnection.On<Guid, double>("TankLevelUpdated", HandleUpdate);
    // Missing: await HubConnection.InvokeAsync("SubscribeToTanks");
}
```

**Why This Breaks:**
- SignalR uses group-based broadcasting
- Handler registered but client not in the "tanks" group
- Updates silently never arrive

**The Fix:**

```razor
// GOOD - Subscribe to group after registering handler
HubConnection.On<Guid, double>("TankLevelUpdated", HandleUpdate);
await HubConnection.InvokeAsync("SubscribeToTanks");
```

## HTTP API Calls

### POST with State Update

```razor
@code {
    private async Task ToggleControl(Control control)
    {
        var newState = !(bool)control.State;
        
        // Optimistic update
        control.State = newState;
        StateHasChanged();

        try
        {
            await Http.PostAsJsonAsync($"api/controls/{control.Id}/state", newState);
        }
        catch
        {
            // Rollback on failure
            control.State = !newState;
            StateHasChanged();
        }
    }
}
```

### WARNING: Blocking Calls in Async Context

**The Problem:**

```razor
// BAD - Blocks the render thread
protected override void OnInitialized()
{
    _tanks = Http.GetFromJsonAsync<List<Tank>>("api/tanks").Result;
}
```

**Why This Breaks:**
1. `.Result` blocks synchronously
2. Blazor WASM is single-threaded
3. UI freezes completely during fetch

**The Fix:**

```razor
// GOOD - Use async lifecycle method
protected override async Task OnInitializedAsync()
{
    _tanks = await Http.GetFromJsonAsync<List<Tank>>("api/tanks");
}
```

## Anti-Patterns

### WARNING: Logic in Razor Markup

**The Problem:**

```razor
<!-- BAD - Complex logic in markup -->
@foreach (var tank in _tanks.Where(t => t.IsActive && t.CurrentLevel < 20)
    .OrderBy(t => t.CurrentLevel))
{
    <MudAlert Severity="Severity.Warning">@tank.Name is low</MudAlert>
}
```

**The Fix:**

```razor
<!-- GOOD - Logic in @code block -->
@foreach (var tank in LowTanks)
{
    <MudAlert Severity="Severity.Warning">@tank.Name is low</MudAlert>
}

@code {
    private IEnumerable<Tank> LowTanks => _tanks?
        .Where(t => t.IsActive && t.CurrentLevel < 20)
        .OrderBy(t => t.CurrentLevel) ?? Enumerable.Empty<Tank>();
}