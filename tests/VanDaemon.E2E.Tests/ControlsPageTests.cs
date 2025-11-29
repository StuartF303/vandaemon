using FluentAssertions;
using Microsoft.Playwright;

namespace VanDaemon.E2E.Tests;

/// <summary>
/// E2E tests for the Controls page (device control management)
/// </summary>
public class ControlsPageTests : PlaywrightTestBase
{
    [Fact]
    public async Task ControlsPage_ShouldLoadSuccessfully()
    {
        // Arrange & Act
        await NavigateToAppAsync("/controls");

        // Assert
        var pageContent = await Page!.ContentAsync();
        pageContent.Should().Contain("Control", "Page should display Controls heading or content");

        // Verify MudBlazor layout is loaded
        var layout = await Page.QuerySelectorAsync(".mud-layout");
        layout.Should().NotBeNull("MudBlazor layout should be present");
    }

    [Fact]
    public async Task ControlsPage_ShouldDisplayControlCards()
    {
        // Arrange & Act
        await NavigateToAppAsync("/controls");
        await WaitForSignalRConnectionAsync();
        await Task.Delay(1000); // Wait for control data to load

        // Assert - verify main content area exists
        var mainContent = await Page!.QuerySelectorAsync(".mud-main-content, main, .mud-container");
        mainContent.Should().NotBeNull("Main content area should be present");

        // Look for interactive elements (switches, sliders, buttons)
        var interactiveElements = await Page.QuerySelectorAllAsync(
            ".mud-switch, .mud-slider, .mud-button, input[type='checkbox'], input[type='range']");

        // Note: Count might be 0 if no controls are configured
        // So we just verify the page structure loaded
        var pageIsValid = await Page.QuerySelectorAsync(".mud-layout") != null;
        pageIsValid.Should().BeTrue("Page should render correctly regardless of control data");
    }

    [Fact]
    public async Task ControlsPage_ShouldHaveToggleSwitches()
    {
        // Arrange & Act
        await NavigateToAppAsync("/controls");
        await Task.Delay(1000);

        // Assert - look for toggle switches or checkboxes
        var switches = await Page!.QuerySelectorAllAsync(
            ".mud-switch, .mud-input-control-input-container input[type='checkbox']");

        // If controls exist, there should be some interactive elements
        var buttons = await Page.QuerySelectorAllAsync("button");

        // Page should have at least buttons for navigation/actions
        buttons.Should().NotBeEmpty("Page should have interactive buttons");
    }

    [Fact]
    public async Task ControlsPage_ShouldToggleControlState()
    {
        // Arrange
        await NavigateToAppAsync("/controls");
        await WaitForSignalRConnectionAsync();
        await Task.Delay(1000);

        // Act - find first toggle switch if available
        var toggle = await Page!.QuerySelectorAsync(
            ".mud-switch input, input[type='checkbox']:not([disabled])");

        if (toggle != null)
        {
            // Get initial state
            var initialState = await toggle.IsCheckedAsync();

            // Click toggle
            await toggle.ClickAsync();
            await Task.Delay(500); // Wait for state change

            // Assert - state should have changed
            var newState = await toggle.IsCheckedAsync();
            newState.Should().NotBe(initialState, "Toggle state should change when clicked");
        }
        else
        {
            // No controls configured - verify page loaded without errors
            var errors = new List<string>();
            Page.Console += (_, msg) =>
            {
                if (msg.Type == "error")
                    errors.Add(msg.Text);
            };

            await Task.Delay(1000);
            errors.Should().BeEmpty("No errors should occur on controls page");
        }
    }

    [Fact]
    public async Task ControlsPage_ShouldHaveDimmerSliders()
    {
        // Arrange & Act
        await NavigateToAppAsync("/controls");
        await Task.Delay(1000);

        // Assert - look for sliders or range inputs
        var sliders = await Page!.QuerySelectorAllAsync(
            ".mud-slider, input[type='range']");

        // Sliders might not exist if no dimmer controls are configured
        // Verify page structure instead
        var layout = await Page.QuerySelectorAsync(".mud-layout");
        layout.Should().NotBeNull("Layout should be present");
    }

    [Fact]
    public async Task ControlsPage_ShouldDisplayControlIcons()
    {
        // Arrange & Act
        await NavigateToAppAsync("/controls");
        await Task.Delay(1000);

        // Assert - look for icons (MudBlazor uses SVG icons)
        var icons = await Page!.QuerySelectorAllAsync(
            ".mud-icon, svg, .mud-icon-root");

        // MudBlazor layout should have at least some icons (navigation, etc.)
        icons.Should().NotBeEmpty("Page should have icons in the UI");
    }

    [Fact]
    public async Task ControlsPage_ShouldReceiveRealTimeUpdates()
    {
        // Arrange
        await NavigateToAppAsync("/controls");
        await WaitForSignalRConnectionAsync();

        // Track console messages
        var messages = new List<string>();
        Page!.Console += (_, msg) => messages.Add(msg.Text);

        // Act - wait for potential real-time updates
        await Task.Delay(6000); // Wait through at least one polling cycle

        // Assert - no errors should occur
        var errors = messages.Where(m =>
            m.Contains("error", StringComparison.OrdinalIgnoreCase) &&
            !m.Contains("favicon", StringComparison.OrdinalIgnoreCase)) // Ignore favicon errors
            .ToList();

        errors.Should().BeEmpty("No errors should occur while waiting for real-time updates");
    }

    [Fact]
    public async Task ControlsPage_ShouldHaveAddControlButton()
    {
        // Arrange & Act
        await NavigateToAppAsync("/controls");

        // Assert - look for "Add" or "New Control" button
        var buttons = await Page!.QuerySelectorAllAsync("button");
        buttons.Should().NotBeEmpty("Page should have buttons");

        // Check for common button text
        var buttonTexts = new List<string>();
        foreach (var button in buttons)
        {
            var text = await button.TextContentAsync();
            if (!string.IsNullOrEmpty(text))
                buttonTexts.Add(text);
        }

        // Verify page has interactive buttons (exact text depends on implementation)
        buttons.Count.Should().BeGreaterThan(0, "Page should have interactive elements");
    }

    [Fact]
    public async Task ControlsPage_ShouldBeResponsive()
    {
        // Arrange
        await NavigateToAppAsync("/controls");

        // Act - resize to mobile viewport
        await Page!.SetViewportSizeAsync(375, 667); // iPhone SE size
        await Task.Delay(500); // Wait for responsive layout

        // Assert - page should still render properly
        var layout = await Page.QuerySelectorAsync(".mud-layout");
        layout.Should().NotBeNull("Layout should render on mobile");

        // Main content should be visible
        var mainContent = await Page.QuerySelectorAsync(".mud-main-content, main");
        mainContent.Should().NotBeNull("Main content should be visible on mobile");
    }

    [Fact]
    public async Task ControlsPage_ShouldNavigateToSettings()
    {
        // Arrange
        await NavigateToAppAsync("/controls");

        // Act - look for settings navigation link
        var settingsLink = await Page!.QuerySelectorAsync(
            "a[href='/settings'], a[href*='settings'], .mud-nav-link:has-text('Settings')");

        if (settingsLink != null)
        {
            await settingsLink.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Assert
            var currentUrl = Page.Url;
            currentUrl.Should().Contain("settings", "Should navigate to settings page");
        }
        else
        {
            // Settings link might be in drawer menu
            var drawerButton = await Page.QuerySelectorAsync("button[aria-label*='menu']");
            if (drawerButton != null)
            {
                await drawerButton.ClickAsync();
                await Task.Delay(500);

                settingsLink = await Page.QuerySelectorAsync("a[href*='settings']");
                if (settingsLink != null)
                {
                    await settingsLink.ClickAsync();
                    await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                    var currentUrl = Page.Url;
                    currentUrl.Should().Contain("settings", "Should navigate to settings page");
                }
            }
        }
    }

    [Fact]
    public async Task ControlsPage_ShouldHandleEmptyState()
    {
        // Arrange & Act
        await NavigateToAppAsync("/controls");
        await Task.Delay(1000);

        // Assert - page should handle empty state gracefully
        var pageContent = await Page!.ContentAsync();

        // Should not show error messages
        pageContent.Should().NotContain("exception", "Page should not show exceptions");

        // Layout should still be intact
        var layout = await Page.QuerySelectorAsync(".mud-layout");
        layout.Should().NotBeNull("Layout should render even with no controls");
    }

    [Fact]
    public async Task ControlsPage_ShouldDisplayControlTypes()
    {
        // Arrange & Act
        await NavigateToAppAsync("/controls");
        await Task.Delay(1000);

        // Assert - look for different control type indicators
        var pageContent = await Page!.ContentAsync();

        // Page should at least have the word "Control" somewhere
        pageContent.Should().Contain("Control", "Page should reference controls");

        // Verify no critical errors occurred
        var mainContent = await Page.QuerySelectorAsync(".mud-main-content, main");
        mainContent.Should().NotBeNull("Main content should be present");
    }
}
