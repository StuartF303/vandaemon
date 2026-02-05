# Design Patterns Reference

## Contents
- Visual Anti-Patterns
- IoT Dashboard Patterns
- Consistency Guidelines
- Accessibility Requirements
- Project-Specific Patterns

## Visual Anti-Patterns

### WARNING: Generic Alert Styling

**The Problem:**
```razor
@* BAD - All alerts look the same *@
<div class="alert">Tank low!</div>
<div class="alert">Settings saved.</div>
<div class="alert">Connection lost!</div>
```

**Why This Breaks:**
- Users can't quickly distinguish severity
- Critical alerts get ignored
- Violates IoT safety requirements

**The Fix:**
```razor
@* GOOD - Severity communicates urgency *@
<MudAlert Severity="Severity.Error">Tank critically low!</MudAlert>
<MudAlert Severity="Severity.Success">Settings saved.</MudAlert>
<MudAlert Severity="Severity.Warning">Connection lost - reconnecting...</MudAlert>
```

### WARNING: Tiny Touch Targets

**The Problem:**
```razor
@* BAD - 24px icon button *@
<MudIconButton Icon="@Icons.Material.Filled.Power" Size="Size.Small" />
```

**Why This Breaks:**
- Unusable in moving vehicle
- Frustrating for users with larger fingers
- Fails WCAG touch target guidelines

**The Fix:**
```razor
@* GOOD - 48px minimum touch target *@
<MudIconButton Icon="@Icons.Material.Filled.Power" 
               Size="Size.Large"
               Class="touch-target-48" />

<style>
    .touch-target-48 { min-width: 48px; min-height: 48px; }
</style>
```

### WARNING: Information Overload on Dashboard

**The Problem:**
```razor
@* BAD - Everything at once *@
<MudGrid>
    @foreach (var tank in allTanks) { <TankCard /> }
    @foreach (var control in allControls) { <ControlCard /> }
    @foreach (var alert in allAlerts) { <AlertCard /> }
    @foreach (var device in allDevices) { <DeviceCard /> }
</MudGrid>
```

**Why This Breaks:**
- Cognitive overload
- No visual hierarchy
- Critical information buried

**The Fix:**
```razor
@* GOOD - Prioritized, scannable layout *@
<MudGrid>
    @* Critical alerts first *@
    <MudItem xs="12">
        <ActiveAlerts Alerts="@criticalAlerts" />
    </MudItem>
    
    @* Primary tanks (fresh water, waste) *@
    <MudItem xs="12" md="6">
        <TankSummary Tanks="@primaryTanks" />
    </MudItem>
    
    @* Quick controls *@
    <MudItem xs="12" md="6">
        <QuickControls Controls="@frequentControls" />
    </MudItem>
</MudGrid>
```

## IoT Dashboard Patterns

### Status-at-a-Glance Pattern

```razor
<MudStack Row="true" Spacing="2" Class="status-bar">
    <StatusChip Label="Water" Value="@freshWater.CurrentLevel" Unit="%" />
    <StatusChip Label="Waste" Value="@wasteWater.CurrentLevel" Unit="%" />
    <StatusChip Label="LPG" Value="@lpg.CurrentLevel" Unit="%" />
    <StatusChip Label="Battery" Value="@battery.Voltage" Unit="V" />
</MudStack>
```

### Optimistic UI for Controls

```razor
@code {
    private async Task ToggleControl(Control control)
    {
        // Immediate visual feedback
        var previousState = control.State;
        control.State = !((bool)control.State);
        StateHasChanged();
        
        try
        {
            await ControlService.SetStateAsync(control.Id, control.State);
        }
        catch
        {
            // Revert on failure
            control.State = previousState;
            StateHasChanged();
            Snackbar.Add("Failed to toggle control", Severity.Error);
        }
    }
}
```

## Consistency Guidelines

### Icon Usage

| Action | Icon | Usage |
|--------|------|-------|
| Refresh | `Icons.Material.Filled.Refresh` | Reload data |
| Settings | `Icons.Material.Filled.Settings` | Configuration |
| Add | `Icons.Material.Filled.Add` | Create new item |
| Delete | `Icons.Material.Filled.Delete` | Remove item |
| Edit | `Icons.Material.Filled.Edit` | Modify item |
| Water | `Icons.Material.Filled.WaterDrop` | Fresh water tank |
| Fuel | `Icons.Material.Filled.LocalGasStation` | LPG/Fuel |
| Power | `Icons.Material.Filled.Power` | On/Off controls |

### Button Hierarchy

```razor
@* Primary action - filled, colored *@
<MudButton Color="Color.Primary" Variant="Variant.Filled">Save</MudButton>

@* Secondary action - outlined *@
<MudButton Color="Color.Primary" Variant="Variant.Outlined">Cancel</MudButton>

@* Tertiary action - text only *@
<MudButton Color="Color.Default" Variant="Variant.Text">Reset</MudButton>

@* Destructive action - error color, confirmation required *@
<MudButton Color="Color.Error" Variant="Variant.Filled" OnClick="ConfirmDelete">
    Delete
</MudButton>
```

## Accessibility Requirements

### Color Contrast

MudBlazor defaults meet WCAG AA. Maintain when customizing:

```css
/* Ensure 4.5:1 contrast for text */
.custom-status-text {
    color: #1b5e20; /* Green 800 on white background */
}
```

### Focus Indicators

Never remove focus indicators:

```css
/* BAD */
*:focus { outline: none; }

/* GOOD - Enhanced focus for touch devices */
.mud-button:focus-visible {
    outline: 2px solid var(--mud-palette-primary);
    outline-offset: 2px;
}
```

### ARIA Labels

```razor
<MudIconButton Icon="@Icons.Material.Filled.Refresh"
               OnClick="RefreshTanks"
               aria-label="Refresh all tank levels" />

<MudProgressLinear Value="@tank.CurrentLevel"
                   aria-label="@($"{tank.Name} level: {tank.CurrentLevel}%")" />
```

## Project-Specific Patterns

### Van Diagram Overlay Positioning

```razor
@* Store overlay positions as percentages for responsive scaling *@
public class OverlayPosition
{
    public string Id { get; set; }
    public double X { get; set; }  // 0-100%
    public double Y { get; set; }  // 0-100%
    public string Label { get; set; }
}

@* Render at percentage positions *@
<div style="position: absolute; left: @(pos.X)%; top: @(pos.Y)%;">
    @* Overlay content *@
</div>
```

### Real-Time Connection Status

```razor
@* Always visible in app bar *@
<MudTooltip Text="@(isConnected ? "Connected to API" : "Disconnected - Reconnecting...")">
    <MudBadge Color="@(isConnected ? Color.Success : Color.Error)" 
              Dot="true" 
              Overlap="true">
        <MudIcon Icon="@(isConnected ? Icons.Material.Filled.Wifi : Icons.Material.Filled.WifiOff)" />
    </MudBadge>
</MudTooltip>