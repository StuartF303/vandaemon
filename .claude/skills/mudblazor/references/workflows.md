# MudBlazor Workflows Reference

## Contents
- Dialog Workflows
- Form Validation
- Theme Configuration
- Snackbar Notifications
- Loading States

## Dialog Workflows

### Confirmation Dialog

```razor
@inject IDialogService DialogService

@code {
    private async Task DeleteTank(Tank tank)
    {
        var parameters = new DialogParameters
        {
            ["ContentText"] = $"Delete tank '{tank.Name}'? This cannot be undone.",
            ["ButtonText"] = "Delete",
            ["Color"] = Color.Error
        };
        
        var dialog = await DialogService.ShowAsync<ConfirmDialog>("Confirm Delete", parameters);
        var result = await dialog.Result;
        
        if (!result.Canceled)
        {
            await Http.DeleteAsync($"api/tanks/{tank.Id}");
            await LoadTanks();
        }
    }
}
```

### Custom Dialog Component

```razor
<!-- Dialogs/EditControlDialog.razor -->
@inject HttpClient Http

<MudDialog>
    <TitleContent>
        <MudText Typo="Typo.h6">Edit @Control.Name</MudText>
    </TitleContent>
    <DialogContent>
        <MudTextField @bind-Value="Control.Name" Label="Name" />
        <MudSelect @bind-Value="Control.Type" Label="Type">
            <MudSelectItem Value="ControlType.Toggle">Toggle</MudSelectItem>
            <MudSelectItem Value="ControlType.Dimmer">Dimmer</MudSelectItem>
        </MudSelect>
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="Cancel">Cancel</MudButton>
        <MudButton Color="Color.Primary" OnClick="Save">Save</MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] MudDialogInstance MudDialog { get; set; }
    [Parameter] public Control Control { get; set; }
    
    private void Cancel() => MudDialog.Cancel();
    private void Save() => MudDialog.Close(DialogResult.Ok(Control));
}
```

## Form Validation

### Validation Workflow

Copy this checklist and track progress:
- [ ] Add `@ref="form"` to MudForm
- [ ] Add `@bind-IsValid="isValid"` to MudForm
- [ ] Add validation attributes to each field
- [ ] Disable submit button with `Disabled="@(!isValid)"`
- [ ] Call `form.Validate()` before submission

```razor
<MudForm @ref="form" @bind-IsValid="@isValid" @bind-Errors="@errors">
    <MudTextField @bind-Value="tank.Name"
                  Label="Tank Name"
                  Required="true"
                  RequiredError="Name is required"
                  Validation="@(new Func<string, string>(ValidateName))" />
    
    <MudNumericField @bind-Value="tank.Capacity"
                     Label="Capacity (L)"
                     Min="1" Max="1000"
                     Required="true" />
    
    <MudButton Disabled="@(!isValid)" OnClick="Submit">Save</MudButton>
</MudForm>

@code {
    private MudForm form;
    private bool isValid;
    private string[] errors = { };
    
    private string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Name is required";
        if (name.Length < 3)
            return "Name must be at least 3 characters";
        return null;  // Valid
    }
    
    private async Task Submit()
    {
        await form.Validate();
        if (isValid)
        {
            await Http.PostAsJsonAsync("api/tanks", tank);
        }
    }
}
```

## Theme Configuration

### Dark/Light Mode Toggle

```razor
<!-- In MainLayout.razor -->
@inject SettingsStateService SettingsState

<MudThemeProvider @ref="@_mudThemeProvider" 
                  @bind-IsDarkMode="@_isDarkMode"
                  Theme="@_theme" />

@code {
    private MudThemeProvider _mudThemeProvider;
    private bool _isDarkMode;
    private MudTheme _theme = new()
    {
        Palette = new PaletteLight()
        {
            Primary = "#1976D2",
            Secondary = "#424242",
            AppbarBackground = "#1976D2"
        },
        PaletteDark = new PaletteDark()
        {
            Primary = "#90CAF9",
            Secondary = "#B0BEC5",
            AppbarBackground = "#1E1E1E"
        }
    };
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _isDarkMode = await _mudThemeProvider.GetSystemPreference();
            StateHasChanged();
        }
    }
}
```

## Snackbar Notifications

### Notification Workflow

```razor
@inject ISnackbar Snackbar

@code {
    private async Task SaveSettings()
    {
        try
        {
            await Http.PutAsJsonAsync("api/settings", settings);
            Snackbar.Add("Settings saved successfully", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Failed to save: {ex.Message}", Severity.Error);
        }
    }
    
    // For alerts from SignalR
    private void OnAlertReceived(Alert alert)
    {
        var severity = alert.Severity switch
        {
            AlertSeverity.Critical => Severity.Error,
            AlertSeverity.Warning => Severity.Warning,
            AlertSeverity.Info => Severity.Info,
            _ => Severity.Normal
        };
        
        Snackbar.Add(alert.Message, severity, config =>
        {
            config.RequireInteraction = alert.Severity == AlertSeverity.Critical;
            config.ShowCloseIcon = true;
        });
    }
}
```

## Loading States

### Skeleton Loading Pattern

```razor
@if (isLoading)
{
    <MudSkeleton SkeletonType="SkeletonType.Rectangle" Height="200px" />
    <MudSkeleton SkeletonType="SkeletonType.Text" />
    <MudSkeleton SkeletonType="SkeletonType.Text" Width="60%" />
}
else
{
    <MudCard>
        <MudCardContent>
            <MudText>@tank.Name</MudText>
        </MudCardContent>
    </MudCard>
}
```

### Progress Overlay

```razor
<MudOverlay @bind-Visible="isSaving" DarkBackground="true">
    <MudProgressCircular Color="Color.Primary" Indeterminate="true" />
</MudOverlay>

@code {
    private bool isSaving;
    
    private async Task Save()
    {
        isSaving = true;
        try
        {
            await Http.PutAsJsonAsync("api/settings", settings);
        }
        finally
        {
            isSaving = false;
        }
    }
}
```

### Iteration Pattern for API Calls

1. Show loading state
2. Make API call
3. If error, show snackbar and keep loading state
4. If success, hide loading and update UI
5. Repeat on user retry

```razor
@code {
    private bool isLoading = true;
    private string errorMessage;
    
    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }
    
    private async Task LoadData()
    {
        isLoading = true;
        errorMessage = null;
        
        try
        {
            tanks = await Http.GetFromJsonAsync<List<Tank>>("api/tanks");
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            Snackbar.Add("Failed to load tanks", Severity.Error);
        }
        finally
        {
            isLoading = false;
        }
    }
}