# Aesthetics Reference

## Contents
- Color System
- Typography
- Status Semantics
- Dark Mode Support
- Visual Identity

## Color System

VanDaemon uses MudBlazor's Material Design palette with semantic overrides for IoT status indication.

### Primary Palette

```css
/* wwwroot/css/app.css */
:root {
    --mud-palette-primary: #594AE2;        /* MudBlazor default purple */
    --mud-palette-primary-darken: #4232B2;
    --mud-palette-secondary: #FF4081;
    --mud-palette-background: #FFFFFF;
    --mud-palette-surface: #FFFFFF;
}
```

### Status Colors (Tank/Control States)

| Status | Color | MudBlazor | Usage |
|--------|-------|-----------|-------|
| Normal/OK | Green | `Color.Success` | Tank 30-100%, control active |
| Warning | Amber | `Color.Warning` | Tank 15-30%, connection issues |
| Critical | Red | `Color.Error` | Tank <15%, hardware failure |
| Inactive | Grey | `Color.Default` | Disabled controls, offline devices |
| Info | Blue | `Color.Info` | Informational alerts |

### DO: Use Semantic Colors for State

```razor
@* GOOD - Color communicates meaning *@
<MudAlert Severity="@GetAlertSeverity(alert)">
    @alert.Message
</MudAlert>

@code {
    private Severity GetAlertSeverity(Alert alert) => alert.Severity switch
    {
        AlertSeverity.Critical => Severity.Error,
        AlertSeverity.Warning => Severity.Warning,
        AlertSeverity.Info => Severity.Info,
        _ => Severity.Normal
    };
}
```

### DON'T: Hardcode Colors

```razor
@* BAD - Magic colors without semantic meaning *@
<MudChip Style="background-color: #ff0000;">Low Water</MudChip>

@* GOOD - Use MudBlazor Color system *@
<MudChip Color="Color.Error">Low Water</MudChip>
```

## Typography

MudBlazor uses Roboto by default. VanDaemon maintains this for consistency with Material Design.

### Type Scale Usage

| Typo | Usage in VanDaemon |
|------|-------------------|
| `Typo.h4` | Page titles (Dashboard, Settings) |
| `Typo.h6` | Card headers, section titles |
| `Typo.body1` | Primary content, descriptions |
| `Typo.body2` | Secondary text, captions on overlays |
| `Typo.caption` | Timestamps, metadata |

```razor
@* Page header pattern *@
<MudText Typo="Typo.h4" Class="mb-4">Dashboard</MudText>

@* Card with proper hierarchy *@
<MudCard>
    <MudCardContent>
        <MudText Typo="Typo.h6">Fresh Water Tank</MudText>
        <MudText Typo="Typo.body1">@tank.CurrentLevel.ToString("F0")%</MudText>
        <MudText Typo="Typo.caption" Color="Color.Secondary">
            Updated: @tank.LastUpdated.ToLocalTime().ToString("HH:mm")
        </MudText>
    </MudCardContent>
</MudCard>
```

## Status Semantics

### Tank Alert Thresholds

```csharp
// Consumables (water, LPG, fuel) - alert when LOW
tank.AlertWhenOver = false;
tank.AlertLevel = 15.0; // Alert below 15%

// Waste tanks - alert when HIGH
tank.AlertWhenOver = true;
tank.AlertLevel = 85.0; // Alert above 85%
```

### Visual Feedback Timing

Per constitution requirements:
- Control response: <200ms visual feedback
- Real-time updates: <500ms end-to-end
- Use optimistic UI updates for immediate response

## Dark Mode Support

MudBlazor supports dark mode via `MudThemeProvider`. Currently not implemented but planned.

```razor
@* Future implementation pattern *@
<MudThemeProvider @bind-IsDarkMode="_isDarkMode" />

@code {
    private bool _isDarkMode = false;
}
```

## Visual Identity

VanDaemon's design is functional IoT, not decorative:
- High contrast for outdoor/bright light readability
- Large touch targets for use while driving (parked) or in motion
- Clear status indication over aesthetic flourishes
- SVG diagrams show actual van layout for spatial context