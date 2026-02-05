# MudBlazor Patterns Reference

## Contents
- Component Selection
- Layout Patterns
- Data Display
- Form Controls
- Anti-Patterns

## Component Selection

### Dashboard Cards

```razor
<MudCard Elevation="2" Class="ma-2">
    <MudCardHeader>
        <CardHeaderContent>
            <MudText Typo="Typo.h6">@tank.Name</MudText>
        </CardHeaderContent>
        <CardHeaderActions>
            <MudIconButton Icon="@Icons.Material.Filled.Refresh" 
                           OnClick="RefreshTank" />
        </CardHeaderActions>
    </MudCardHeader>
    <MudCardContent>
        <MudProgressLinear Value="@tank.CurrentLevel" 
                           Color="@GetTankColor(tank)" 
                           Size="Size.Large" />
        <MudText Typo="Typo.body2">@tank.CurrentLevel.ToString("F1")%</MudText>
    </MudCardContent>
</MudCard>
```

### Touch-Optimized Buttons

```razor
<!-- GOOD - Large touch targets for van use -->
<MudButton Variant="Variant.Filled"
           Color="Color.Primary"
           Size="Size.Large"
           FullWidth="true"
           StartIcon="@Icons.Material.Filled.Power"
           OnClick="TogglePump">
    Water Pump
</MudButton>

<!-- BAD - Too small for touch interfaces -->
<MudButton Size="Size.Small" OnClick="TogglePump">Pump</MudButton>
```

## Layout Patterns

### Responsive Container

```razor
<MudContainer MaxWidth="MaxWidth.Large" Class="pa-4">
    <MudGrid Spacing="3">
        <!-- xs: mobile (0-600px), sm: tablet (600-960px), md: desktop -->
        <MudItem xs="12" sm="6" md="4">
            <TankCard Tank="@freshWater" />
        </MudItem>
        <MudItem xs="12" sm="6" md="4">
            <TankCard Tank="@wasteWater" />
        </MudItem>
    </MudGrid>
</MudContainer>
```

### App Bar with Navigation

```razor
<MudAppBar Elevation="1">
    <MudIconButton Icon="@Icons.Material.Filled.Menu"
                   Color="Color.Inherit"
                   OnClick="ToggleDrawer" />
    <MudText Typo="Typo.h6">VanDaemon</MudText>
    <MudSpacer />
    <MudBadge Color="@connectionColor" Dot="true" Overlap="true">
        <MudIcon Icon="@Icons.Material.Filled.Wifi" />
    </MudBadge>
</MudAppBar>
```

## Data Display

### Progress with Color Thresholds

```razor
@code {
    private Color GetTankColor(Tank tank)
    {
        if (tank.AlertWhenOver)
            return tank.CurrentLevel > tank.AlertLevel ? Color.Error : Color.Success;
        else
            return tank.CurrentLevel < tank.AlertLevel ? Color.Warning : Color.Success;
    }
}

<MudProgressLinear Value="@tank.CurrentLevel"
                   Color="@GetTankColor(tank)"
                   Size="Size.Large"
                   Rounded="true" />
```

### Data Table with Actions

```razor
<MudTable Items="@controls" Hover="true" Striped="true">
    <HeaderContent>
        <MudTh>Name</MudTh>
        <MudTh>Type</MudTh>
        <MudTh>State</MudTh>
        <MudTh>Actions</MudTh>
    </HeaderContent>
    <RowTemplate>
        <MudTd>@context.Name</MudTd>
        <MudTd>@context.Type</MudTd>
        <MudTd>
            @if (context.Type == ControlType.Toggle)
            {
                <MudSwitch @bind-Value="@((bool)context.State)" 
                           Color="Color.Success" />
            }
        </MudTd>
        <MudTd>
            <MudIconButton Icon="@Icons.Material.Filled.Edit"
                           OnClick="() => EditControl(context)" />
        </MudTd>
    </RowTemplate>
</MudTable>
```

## Form Controls

### Settings Form

```razor
<MudForm @ref="form" @bind-IsValid="@isValid">
    <MudSelect @bind-Value="settings.VanModel" 
               Label="Van Model" 
               Required="true"
               RequiredError="Van model is required">
        <MudSelectItem Value="@("Mercedes Sprinter LWB")">Sprinter LWB</MudSelectItem>
        <MudSelectItem Value="@("Ford Transit")">Transit</MudSelectItem>
    </MudSelect>
    
    <MudNumericField @bind-Value="settings.LowLevelThreshold"
                     Label="Low Level Alert (%)"
                     Min="0" Max="100"
                     Variant="Variant.Outlined" />
    
    <MudButton Variant="Variant.Filled"
               Color="Color.Primary"
               Disabled="@(!isValid)"
               OnClick="SaveSettings">
        Save
    </MudButton>
</MudForm>
```

## Anti-Patterns

### WARNING: Inline Styles

**The Problem:**

```razor
<!-- BAD - Inline styles break theming -->
<MudCard style="background-color: #1976d2; padding: 16px;">
```

**Why This Breaks:**
1. Ignores MudBlazor's theme system
2. Won't respond to dark/light mode changes
3. Inconsistent with Material Design specifications

**The Fix:**

```razor
<!-- GOOD - Use MudBlazor classes and theme colors -->
<MudCard Class="pa-4" Style="background-color: var(--mud-palette-primary);">

<!-- BETTER - Let component handle it -->
<MudPaper Class="pa-4" Elevation="2">
```

### WARNING: Missing StateHasChanged in SignalR Callbacks

**The Problem:**

```razor
hubConnection.On<Guid, double>("TankLevelUpdated", (id, level) =>
{
    tankLevel = level;  // UI won't update!
});
```

**Why This Breaks:**
1. SignalR callbacks run on a different thread
2. Blazor won't detect the state change
3. UI remains stale until next user interaction

**The Fix:**

```razor
hubConnection.On<Guid, double>("TankLevelUpdated", (id, level) =>
{
    tankLevel = level;
    InvokeAsync(StateHasChanged);  // Force UI update on UI thread
});
```

### WARNING: Small Touch Targets

**When You Might Be Tempted:** Desktop-first design, space constraints

**The Fix:** Always use `Size.Large` for interactive elements in VanDaemon:

```razor
<MudSwitch Size="Size.Large" />
<MudSlider Size="Size.Large" />
<MudButton Size="Size.Large" />
<MudIconButton Size="Size.Large" />