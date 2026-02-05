# Layouts Reference

## Contents
- Page Structure
- Responsive Breakpoints
- Dashboard Layout
- Navigation Patterns
- Spacing System

## Page Structure

VanDaemon uses MudBlazor's layout components with a mobile-first approach.

### Main Layout Pattern

```razor
@* MainLayout.razor *@
@inherits LayoutComponentBase

<MudLayout>
    <MudAppBar Elevation="1">
        <MudIconButton Icon="@Icons.Material.Filled.Menu" 
                       Color="Color.Inherit" 
                       Edge="Edge.Start"
                       OnClick="@ToggleDrawer" />
        <MudText Typo="Typo.h6">VanDaemon</MudText>
        <MudSpacer />
        <ConnectionStatus />
    </MudAppBar>
    
    <MudDrawer @bind-Open="_drawerOpen" 
               ClipMode="DrawerClipMode.Always" 
               Elevation="2">
        <NavMenu />
    </MudDrawer>
    
    <MudMainContent Class="pa-4">
        @Body
    </MudMainContent>
</MudLayout>
```

### Page Container Pattern

```razor
@page "/tanks"

<MudContainer MaxWidth="MaxWidth.Large" Class="py-4">
    <MudText Typo="Typo.h4" Class="mb-4">Tank Monitoring</MudText>
    
    <MudGrid Spacing="3">
        @foreach (var tank in tanks)
        {
            <MudItem xs="12" sm="6" md="4">
                <TankCard Tank="@tank" />
            </MudItem>
        }
    </MudGrid>
</MudContainer>
```

## Responsive Breakpoints

MudBlazor breakpoints align with Material Design:

| Breakpoint | Width | VanDaemon Usage |
|------------|-------|-----------------|
| xs | 0-599px | Single column, full-width cards |
| sm | 600-959px | 2 columns, tablet portrait |
| md | 960-1279px | 3-4 columns, tablet landscape |
| lg | 1280-1919px | Desktop, dashboard with sidebar |
| xl | 1920px+ | Large monitors, expanded diagrams |

### Grid Usage

```razor
@* Responsive card grid *@
<MudGrid Spacing="3">
    <MudItem xs="12" sm="6" md="4" lg="3">
        <TankCard Tank="@freshWater" />
    </MudItem>
    <MudItem xs="12" sm="6" md="4" lg="3">
        <TankCard Tank="@wasteWater" />
    </MudItem>
</MudGrid>
```

## Dashboard Layout

### Van Diagram with Sidebar Controls

```razor
<MudGrid>
    @* Van diagram - takes 2/3 on desktop, full width on mobile *@
    <MudItem xs="12" lg="8">
        <MudPaper Class="pa-4" Elevation="2">
            <VanDiagram Settings="@settings" Overlays="@overlays" />
        </MudPaper>
    </MudItem>
    
    @* Quick controls sidebar *@
    <MudItem xs="12" lg="4">
        <MudStack Spacing="3">
            @foreach (var control in priorityControls)
            {
                <QuickControlCard Control="@control" />
            }
        </MudStack>
    </MudItem>
</MudGrid>
```

## Navigation Patterns

### Drawer Navigation Menu

```razor
@* NavMenu.razor *@
<MudNavMenu>
    <MudNavLink Href="/" Match="NavLinkMatch.All" 
                Icon="@Icons.Material.Filled.Dashboard">
        Dashboard
    </MudNavLink>
    <MudNavLink Href="/tanks" 
                Icon="@Icons.Material.Filled.WaterDrop">
        Tanks
    </MudNavLink>
    <MudNavLink Href="/controls" 
                Icon="@Icons.Material.Filled.ToggleOn">
        Controls
    </MudNavLink>
    <MudNavLink Href="/devices" 
                Icon="@Icons.Material.Filled.Devices">
        Devices
    </MudNavLink>
    <MudNavLink Href="/settings" 
                Icon="@Icons.Material.Filled.Settings">
        Settings
    </MudNavLink>
</MudNavMenu>
```

## Spacing System

MudBlazor uses an 8px spacing unit. Use these classes consistently:

| Class | Value | Usage |
|-------|-------|-------|
| `pa-0` to `pa-16` | 0-128px | Padding all sides |
| `my-2` | 16px | Vertical margin between cards |
| `mb-4` | 32px | Bottom margin after headers |
| `Spacing="3"` | 24px | Grid gap |

### DON'T: Mix Spacing Systems

```razor
@* BAD - Inline styles with arbitrary values *@
<div style="margin: 15px; padding: 22px;">

@* GOOD - MudBlazor utility classes *@
<div class="ma-4 pa-4">