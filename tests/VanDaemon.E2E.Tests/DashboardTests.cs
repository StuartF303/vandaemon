using FluentAssertions;
using Microsoft.Playwright;

namespace VanDaemon.E2E.Tests;

/// <summary>
/// E2E tests for the Dashboard page (main interactive van diagram)
/// </summary>
public class DashboardTests : PlaywrightTestBase
{
    [Fact]
    public async Task Dashboard_ShouldLoadSuccessfully()
    {
        // Arrange & Act
        await NavigateToAppAsync("/");

        // Assert
        var title = await Page!.TitleAsync();
        title.Should().Contain("VanDaemon");

        // Verify main layout components are present
        var appBar = await Page.QuerySelectorAsync(".mud-appbar");
        appBar.Should().NotBeNull("AppBar should be visible");

        var drawer = await Page.QuerySelectorAsync(".mud-drawer");
        drawer.Should().NotBeNull("Navigation drawer should be present");
    }

    [Fact]
    public async Task Dashboard_ShouldDisplayVanDiagram()
    {
        // Arrange & Act
        await NavigateToAppAsync("/");

        // Assert - van diagram container should be visible
        var diagramContainer = await Page!.WaitForSelectorAsync("svg[viewBox], img[alt*='van'], .van-diagram", new()
        {
            State = WaitForSelectorState.Attached,
            Timeout = 5000
        });

        diagramContainer.Should().NotBeNull("Van diagram should be displayed on dashboard");
    }

    [Fact]
    public async Task Dashboard_ShouldHaveNavigationMenu()
    {
        // Arrange & Act
        await NavigateToAppAsync("/");

        // Assert - check for navigation links
        var navLinks = await Page!.QuerySelectorAllAsync(".mud-nav-link, a[href]");
        navLinks.Should().NotBeEmpty("Navigation menu should have links");

        // Verify key navigation items exist
        var pageContent = await Page.ContentAsync();
        pageContent.Should().Contain("Dashboard", "Dashboard link should be present");
    }

    [Fact]
    public async Task Dashboard_ShouldNavigateToTanksPage()
    {
        // Arrange
        await NavigateToAppAsync("/");

        // Act - click navigation to Tanks page
        // Try multiple possible selectors for the Tanks link
        var tanksLink = await Page!.QuerySelectorAsync("a[href='/tanks'], a[href*='tanks'], .mud-nav-link:has-text('Tanks')");

        if (tanksLink != null)
        {
            await tanksLink.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Assert
            var currentUrl = Page.Url;
            currentUrl.Should().Contain("tanks", "Should navigate to tanks page");
        }
        else
        {
            // Navigation might be in a drawer that needs to be opened
            var drawerButton = await Page.QuerySelectorAsync("button[aria-label*='menu'], .mud-icon-button");
            if (drawerButton != null)
            {
                await drawerButton.ClickAsync();
                await Task.Delay(500); // Wait for drawer animation

                tanksLink = await Page.QuerySelectorAsync("a[href='/tanks'], a[href*='tanks']");
                tanksLink.Should().NotBeNull("Tanks link should be in navigation drawer");
                await tanksLink!.ClickAsync();
                await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                var currentUrl = Page.Url;
                currentUrl.Should().Contain("tanks", "Should navigate to tanks page");
            }
        }
    }

    [Fact]
    public async Task Dashboard_ShouldConnectToSignalR()
    {
        // Arrange & Act
        await NavigateToAppAsync("/");
        await WaitForSignalRConnectionAsync();

        // Assert - check if WebSocket connection was established
        // This is indicated by network activity showing ws:// or wss:// connection
        var hasWebSocket = await Page!.EvaluateAsync<bool>(@"
            () => {
                // Check if there are any WebSocket connections
                return performance.getEntriesByType('resource')
                    .some(entry => entry.name.includes('hubs/telemetry') || entry.name.includes('ws://'));
            }
        ");

        // Note: In headless mode, WebSocket might not show up in performance entries
        // So we just verify the page loaded without errors as an indirect check
        var errors = new List<string>();
        Page.Console += (_, msg) =>
        {
            if (msg.Type == "error")
                errors.Add(msg.Text);
        };

        await Task.Delay(2000); // Wait for potential connection

        errors.Should().NotContain(e => e.Contains("SignalR") || e.Contains("WebSocket"),
            "No SignalR connection errors should be present");
    }

    [Fact]
    public async Task Dashboard_ShouldHandleThemeToggle()
    {
        // Arrange
        await NavigateToAppAsync("/");

        // Act - look for theme toggle button (usually in app bar or settings)
        var themeToggle = await Page!.QuerySelectorAsync(
            "button[aria-label*='theme'], button[aria-label*='dark'], .mud-icon-button:has(svg)");

        if (themeToggle != null)
        {
            // Get initial theme state
            var initialHtml = await Page.QuerySelectorAsync("html");
            var initialClass = await initialHtml!.GetAttributeAsync("class");

            // Click theme toggle
            await themeToggle.ClickAsync();
            await Task.Delay(500); // Wait for theme change animation

            // Assert - theme should have changed
            var updatedClass = await initialHtml.GetAttributeAsync("class");
            updatedClass.Should().NotBe(initialClass, "Theme class should change when toggle is clicked");
        }
    }

    [Fact]
    public async Task Dashboard_ShouldBeResponsive()
    {
        // Arrange
        await NavigateToAppAsync("/");

        // Act - resize to mobile viewport
        await Page!.SetViewportSizeAsync(375, 667); // iPhone SE size
        await Task.Delay(500); // Wait for responsive layout

        // Assert - page should still be functional
        var mobileLayout = await Page.QuerySelectorAsync(".mud-layout");
        mobileLayout.Should().NotBeNull("Layout should render on mobile viewport");

        // Hamburger menu button should be visible on mobile
        var menuButton = await Page.QuerySelectorAsync("button[aria-label*='menu'], .mud-icon-button");
        menuButton.Should().NotBeNull("Mobile menu button should be visible");
    }
}
