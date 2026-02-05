# Blazor Workflows Reference

## Contents
- Creating New Pages
- Adding Real-Time Features
- Component Communication
- Testing Components

## Creating New Pages

### Workflow Checklist

Copy this checklist and track progress:
- [ ] Create .razor file in `src/Frontend/VanDaemon.Web/Pages/`
- [ ] Add `@page` directive with route
- [ ] Inject required services (`HttpClient`, `HubConnection`, etc.)
- [ ] Implement `OnInitializedAsync` for data loading
- [ ] Add to navigation in `Shared/NavMenu.razor`
- [ ] Handle loading and error states

### Step-by-Step Example

1. **Create the page file**

```razor
<!-- Pages/Electrical.razor -->
@page "/electrical"
@inject HttpClient Http

<PageTitle>Electrical System</PageTitle>

<MudText Typo="Typo.h4" Class="mb-4">Electrical System</MudText>

@if (_system is null)
{
    <MudProgressCircular Indeterminate="true" />
}
else
{
    <MudSimpleTable>
        <thead>
            <tr>
                <th>Device</th>
                <th>Status</th>
            </tr>
        </thead>
        <tbody>
            @foreach (var device in _system.Devices)
            {
                <tr>
                    <td>@device.Name</td>
                    <td>@device.Status</td>
                </tr>
            }
        </tbody>
    </MudSimpleTable>
}

@code {
    private ElectricalSystem? _system;

    protected override async Task OnInitializedAsync()
    {
        _system = await Http.GetFromJsonAsync<ElectricalSystem>("api/electrical");
    }
}
```

2. **Add navigation link**

```razor
<!-- Shared/NavMenu.razor -->
<MudNavLink Href="/electrical" Icon="@Icons.Material.Filled.ElectricalServices">
    Electrical
</MudNavLink>
```

3. **Validate**
   - Run `dotnet build src/Frontend/VanDaemon.Web`
   - Navigate to `/electrical` in browser
   - Verify data loads correctly

## Adding Real-Time Features

### Workflow Checklist

Copy this checklist and track progress:
- [ ] Inject `HubConnection` in component
- [ ] Implement `IAsyncDisposable`
- [ ] Register event handlers in `OnInitializedAsync`
- [ ] Call `SubscribeTo{Group}()` on hub
- [ ] Use `InvokeAsync(StateHasChanged)` in handlers
- [ ] Clean up handlers in `DisposeAsync`

### Complete Implementation

```razor
@page "/controls"
@inject HttpClient Http
@inject HubConnection HubConnection
@implements IAsyncDisposable

<PageTitle>Controls</PageTitle>

@foreach (var control in _controls ?? Enumerable.Empty<Control>())
{
    <MudSwitch T="bool" 
               Checked="@((bool)control.State)"
               CheckedChanged="@(v => ToggleControl(control, v))"
               Label="@control.Name" />
}

@code {
    private List<Control>? _controls;

    protected override async Task OnInitializedAsync()
    {
        // 1. Load initial data
        _controls = await Http.GetFromJsonAsync<List<Control>>("api/controls");

        // 2. Register handler
        HubConnection.On<Guid, object, string>("ControlStateChanged", 
            (id, state, name) =>
            {
                var control = _controls?.FirstOrDefault(c => c.Id == id);
                if (control is not null)
                {
                    control.State = state;
                    InvokeAsync(StateHasChanged);
                }
            });

        // 3. Subscribe to group
        await HubConnection.InvokeAsync("SubscribeToControls");
    }

    private async Task ToggleControl(Control control, bool newState)
    {
        await Http.PostAsJsonAsync($"api/controls/{control.Id}/state", newState);
    }

    public async ValueTask DisposeAsync()
    {
        HubConnection.Remove("ControlStateChanged");
    }
}
```

### Validation Loop

1. Make changes to component
2. Validate: `dotnet build src/Frontend/VanDaemon.Web`
3. If build fails, fix errors and repeat step 2
4. Test in browser with DevTools Network tab open
5. Verify WebSocket messages in Network → WS tab

## Component Communication

### Parent to Child (Parameters)

```razor
<!-- Parent: Dashboard -->
<TankCard Tank="@tank" OnRefresh="@HandleRefresh" />

<!-- Child: TankCard.razor -->
@code {
    [Parameter, EditorRequired]
    public Tank Tank { get; set; } = default!;

    [Parameter]
    public EventCallback OnRefresh { get; set; }

    private async Task RefreshClicked()
    {
        await OnRefresh.InvokeAsync();
    }
}
```

### Child to Parent (EventCallback)

```razor
<!-- Child emits event -->
<MudButton OnClick="@(() => OnTankSelected.InvokeAsync(Tank))">
    Select
</MudButton>

@code {
    [Parameter]
    public EventCallback<Tank> OnTankSelected { get; set; }
}

<!-- Parent handles event -->
<TankCard Tank="@tank" OnTankSelected="@HandleTankSelected" />

@code {
    private void HandleTankSelected(Tank tank)
    {
        _selectedTank = tank;
    }
}
```

### Cascading Values for Shared State

```razor
<!-- App.razor or layout -->
<CascadingValue Value="@_settings">
    @Body
</CascadingValue>

<!-- Any descendant component -->
@code {
    [CascadingParameter]
    public SystemConfiguration Settings { get; set; } = default!;
}
```

## Testing Components

See the **playwright** skill for E2E testing patterns.

### Manual Testing Workflow

1. Start API: `cd src/Backend/VanDaemon.Api && dotnet run`
2. Start Web: `cd src/Frontend/VanDaemon.Web && dotnet run`
3. Open browser to `http://localhost:5001`
4. Open DevTools (F12)
5. Check Console for errors
6. Check Network tab for failed requests
7. Check WS tab for SignalR messages

### E2E Test Example

```csharp
// tests/VanDaemon.E2E.Tests/DashboardTests.cs
[Fact]
public async Task Dashboard_ShowsTankLevels()
{
    await Page.GotoAsync("http://localhost:5001/");
    
    // Wait for data to load
    await Page.WaitForSelectorAsync("[data-testid='tank-card']");
    
    // Verify tanks displayed
    var tankCards = await Page.QuerySelectorAllAsync("[data-testid='tank-card']");
    Assert.True(tankCards.Count > 0);
}