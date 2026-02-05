# Motion Reference

## Contents
- Loading States
- Transition Patterns
- Real-Time Updates
- Skeleton Loaders
- Performance Considerations

## Loading States

### Progress Indicators

```razor
@* Full-page loading *@
@if (isLoading)
{
    <MudProgressCircular Color="Color.Primary" Indeterminate="true" />
}

@* Button loading state *@
<MudButton Color="Color.Primary"
           Disabled="@isSubmitting"
           OnClick="Submit">
    @if (isSubmitting)
    {
        <MudProgressCircular Size="Size.Small" Indeterminate="true" Class="mr-2" />
    }
    Save Settings
</MudButton>
```

### Tank Level Animation

```razor
@* Animated progress bar for tank levels *@
<MudProgressLinear Color="@GetTankColor(tank.CurrentLevel)"
                   Value="@tank.CurrentLevel"
                   Rounded="true"
                   Size="Size.Large"
                   Class="tank-progress" />

<style>
    .tank-progress .mud-progress-linear-bar {
        transition: width 0.3s ease-out;
    }
</style>
```

## Transition Patterns

### Page Transitions

MudBlazor doesn't include built-in page transitions. Use CSS for simple fades:

```css
/* wwwroot/css/app.css */
.page-transition {
    animation: fadeIn 0.2s ease-in;
}

@keyframes fadeIn {
    from { opacity: 0; }
    to { opacity: 1; }
}
```

```razor
<div class="page-transition">
    @* Page content *@
</div>
```

### Control State Transitions

```css
/* Smooth color transitions for status changes */
.status-indicator {
    transition: background-color 0.2s ease, color 0.2s ease;
}

/* Switch toggle animation is built into MudBlazor */
.mud-switch .mud-switch-thumb {
    transition: left 0.2s cubic-bezier(0.4, 0, 0.2, 1);
}
```

## Real-Time Updates

### SignalR Update Animation

```razor
@code {
    private Dictionary<Guid, bool> recentlyUpdated = new();

    private async Task OnTankLevelUpdated(Guid tankId, double level)
    {
        // Mark as recently updated for highlight
        recentlyUpdated[tankId] = true;
        StateHasChanged();
        
        // Remove highlight after animation
        await Task.Delay(500);
        recentlyUpdated[tankId] = false;
        StateHasChanged();
    }
}

<MudCard Class="@(recentlyUpdated.GetValueOrDefault(tank.Id) ? "highlight-update" : "")">
    @* Tank content *@
</MudCard>

<style>
    .highlight-update {
        animation: pulse 0.5s ease;
    }
    
    @keyframes pulse {
        0%, 100% { box-shadow: none; }
        50% { box-shadow: 0 0 0 4px rgba(76, 175, 80, 0.3); }
    }
</style>
```

## Skeleton Loaders

### Card Skeleton While Loading

```razor
@if (isLoading)
{
    <MudCard>
        <MudCardContent>
            <MudSkeleton Width="60%" Height="24px" Class="mb-2" />
            <MudSkeleton Width="100%" Height="20px" Class="mb-2" />
            <MudSkeleton Width="40%" Height="16px" />
        </MudCardContent>
    </MudCard>
}
else
{
    <TankCard Tank="@tank" />
}
```

### Table Skeleton

```razor
<MudTable Items="@(isLoading ? new List<object>(5) : actualData)" Loading="@isLoading">
    <HeaderContent>
        <MudTh>Name</MudTh>
        <MudTh>Status</MudTh>
    </HeaderContent>
    <RowTemplate>
        @if (isLoading)
        {
            <MudTd><MudSkeleton Width="80%" /></MudTd>
            <MudTd><MudSkeleton Width="60%" /></MudTd>
        }
        else
        {
            <MudTd>@context.Name</MudTd>
            <MudTd>@context.Status</MudTd>
        }
    </RowTemplate>
</MudTable>
```

## Performance Considerations

### WARNING: Avoid Excessive Re-renders

**The Problem:**
```razor
@* BAD - Re-renders entire list on any update *@
@foreach (var tank in tanks)
{
    <div style="background: @(tank.CurrentLevel < 20 ? "red" : "green")">
        @tank.Name: @tank.CurrentLevel%
    </div>
}
```

**Why This Breaks:**
- Every SignalR update triggers full component re-render
- CSS transitions won't work because DOM is recreated
- Poor performance with many items

**The Fix:**
```razor
@* GOOD - Isolated component updates *@
@foreach (var tank in tanks)
{
    <TankDisplay @key="tank.Id" Tank="@tank" />
}

@* TankDisplay.razor - only re-renders when Tank changes *@
@code {
    [Parameter] public Tank Tank { get; set; } = null!;
    
    protected override bool ShouldRender() => true; // Let Blazor diff
}
```

### Animation Frame Budget

Target 16ms per frame (60fps). Avoid:
- Complex CSS filters during transitions
- Multiple simultaneous animations
- Layout-triggering properties (width, height) in animations

Use `transform` and `opacity` for performant animations:

```css
/* GOOD - GPU-accelerated */
.animate-in {
    transform: translateY(0);
    opacity: 1;
    transition: transform 0.2s, opacity 0.2s;
}

/* BAD - Triggers layout */
.animate-in-bad {
    margin-top: 0;
    height: auto;
    transition: margin-top 0.2s, height 0.2s;
}