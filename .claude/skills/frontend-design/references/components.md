# Components Reference

## Contents
- Touch-Optimized Controls
- Status Cards
- Data Tables
- Dialogs and Forms
- SVG Diagram Integration

## Touch-Optimized Controls

### Minimum Touch Target: 48px

```razor
@* GOOD - Large switch for touch *@
<MudSwitch T="bool" 
           @bind-Value="control.IsOn"
           Color="Color.Primary"
           Class="mud-switch-large"
           Label="@control.Name" />

@* GOOD - Touch-friendly button *@
<MudIconButton Icon="@Icons.Material.Filled.Refresh"
               Size="Size.Large"
               OnClick="RefreshTanks"
               aria-label="Refresh tank levels" />
```

### Dimmer Control Pattern

```razor
<MudCard Class="pa-4">
    <MudCardContent>
        <MudText Typo="Typo.h6">@control.Name</MudText>
        <MudSlider T="int" 
                   @bind-Value="dimmerValue"
                   Min="0" Max="255"
                   Step="1"
                   Color="Color.Primary"
                   Class="my-4">
            <MudText Typo="Typo.body2">@((dimmerValue / 255.0 * 100).ToString("F0"))%</MudText>
        </MudSlider>
    </MudCardContent>
    <MudCardActions>
        <MudButton OnClick="ApplyDimmer" Color="Color.Primary">Apply</MudButton>
    </MudCardActions>
</MudCard>
```

## Status Cards

### Tank Status Card

```razor
<MudCard Elevation="2" Class="my-2">
    <MudCardContent>
        <div class="d-flex justify-space-between align-center">
            <MudText Typo="Typo.h6">@tank.Name</MudText>
            <MudIcon Icon="@GetTankIcon(tank.Type)" 
                     Color="@GetStatusColor(tank.CurrentLevel)" />
        </div>
        <MudProgressLinear Color="@GetStatusColor(tank.CurrentLevel)"
                           Value="@tank.CurrentLevel"
                           Rounded="true"
                           Size="Size.Large"
                           Class="my-2" />
        <MudText Typo="Typo.body2" Color="Color.Secondary">
            @tank.CurrentLevel.ToString("F0")% of @tank.Capacity L
        </MudText>
    </MudCardContent>
</MudCard>

@code {
    private string GetTankIcon(TankType type) => type switch
    {
        TankType.FreshWater => Icons.Material.Filled.WaterDrop,
        TankType.WasteWater => Icons.Material.Filled.Delete,
        TankType.LPG => Icons.Material.Filled.LocalGasStation,
        TankType.Fuel => Icons.Material.Filled.LocalGasStation,
        _ => Icons.Material.Filled.Storage
    };
}
```

## Data Tables

### Dense Table for Device Lists

```razor
<MudTable Items="@devices" Dense="true" Hover="true" Striped="true">
    <HeaderContent>
        <MudTh>Name</MudTh>
        <MudTh>Status</MudTh>
        <MudTh>Last Update</MudTh>
    </HeaderContent>
    <RowTemplate>
        <MudTd DataLabel="Name">@context.Name</MudTd>
        <MudTd DataLabel="Status">
            <MudChip Size="Size.Small" Color="@(context.IsOnline ? Color.Success : Color.Error)">
                @(context.IsOnline ? "Online" : "Offline")
            </MudChip>
        </MudTd>
        <MudTd DataLabel="Last Update">@context.LastSeen.ToString("HH:mm:ss")</MudTd>
    </RowTemplate>
</MudTable>
```

## Dialogs and Forms

### Settings Form Pattern

```razor
<MudDialog>
    <DialogContent>
        <MudForm @ref="form" @bind-IsValid="isValid">
            <MudTextField @bind-Value="settings.VanModel"
                          Label="Van Model"
                          Required="true"
                          RequiredError="Van model is required" />
            <MudSelect @bind-Value="settings.VanDiagram" Label="Van Diagram">
                @foreach (var diagram in availableDiagrams)
                {
                    <MudSelectItem Value="@diagram">@diagram</MudSelectItem>
                }
            </MudSelect>
            <MudNumericField @bind-Value="settings.LowLevelThreshold"
                             Label="Low Level Alert (%)"
                             Min="0" Max="100" />
        </MudForm>
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="Cancel">Cancel</MudButton>
        <MudButton Color="Color.Primary" 
                   Disabled="@(!isValid)"
                   OnClick="Save">Save</MudButton>
    </DialogActions>
</MudDialog>
```

## SVG Diagram Integration

### Draggable Overlay Container

```razor
<div class="diagram-wrapper" @ref="diagramContainer">
    <img src="diagrams/@currentDiagram" 
         alt="Van Layout" 
         class="van-diagram" />
    
    @foreach (var overlay in overlays)
    {
        <div class="overlay-badge @(editMode ? "draggable" : "")"
             style="left: @(overlay.X)%; top: @(overlay.Y)%;"
             @ondragend="@(e => OnOverlayDrop(overlay, e))">
            <MudBadge Content="@overlay.DisplayValue" 
                      Color="@overlay.StatusColor"
                      Overlap="true">
                <MudIcon Icon="@overlay.Icon" Size="Size.Medium" />
            </MudBadge>
        </div>
    }
</div>

<style>
    .diagram-wrapper { position: relative; width: 100%; }
    .van-diagram { width: 100%; height: auto; }
    .overlay-badge { position: absolute; transform: translate(-50%, -50%); }
    .overlay-badge.draggable { cursor: move; }
</style>