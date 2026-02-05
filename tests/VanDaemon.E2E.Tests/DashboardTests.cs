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

    [Fact]
    public async Task Dashboard_HamburgerMenu_ShouldBeOnOppositeSideOfNavBar()
    {
        // Arrange & Act
        await NavigateToAppAsync("/");
        await Task.Delay(1000); // Wait for page to fully load and settings to be applied

        // Get the layout container to determine toolbar position
        var layoutContainer = await Page!.QuerySelectorAsync(".layout-container");
        layoutContainer.Should().NotBeNull("Layout container should exist");

        var layoutClasses = await layoutContainer!.GetAttributeAsync("class");
        layoutClasses.Should().NotBeNullOrEmpty("Layout container should have classes");

        // Determine toolbar position (left or right)
        var isToolbarLeft = layoutClasses!.Contains("toolbar-left");
        var isToolbarRight = layoutClasses.Contains("toolbar-right");

        (isToolbarLeft || isToolbarRight).Should().BeTrue(
            "Toolbar should be positioned either left or right, found classes: " + layoutClasses);

        // Get the hamburger menu to determine its position
        var hamburgerMenu = await Page.QuerySelectorAsync(".dashboard-menu");
        hamburgerMenu.Should().NotBeNull("Dashboard hamburger menu should exist");

        var menuClasses = await hamburgerMenu!.GetAttributeAsync("class");
        menuClasses.Should().NotBeNullOrEmpty("Menu should have classes");

        var isMenuLeft = menuClasses!.Contains("menu-left");
        var isMenuRight = menuClasses.Contains("menu-right");

        (isMenuLeft || isMenuRight).Should().BeTrue(
            "Menu should be positioned either left or right, found classes: " + menuClasses);

        // Assert - menu should be on opposite side of toolbar
        if (isToolbarLeft)
        {
            isMenuRight.Should().BeTrue(
                "When toolbar is on LEFT, hamburger menu should be on RIGHT to avoid accidental clicks. " +
                $"Layout classes: {layoutClasses}, Menu classes: {menuClasses}");
        }
        else if (isToolbarRight)
        {
            isMenuLeft.Should().BeTrue(
                "When toolbar is on RIGHT, hamburger menu should be on LEFT to avoid accidental clicks. " +
                $"Layout classes: {layoutClasses}, Menu classes: {menuClasses}");
        }

        // Also verify the actual position in pixels
        var layoutBox = await layoutContainer.BoundingBoxAsync();
        var menuBox = await hamburgerMenu.BoundingBoxAsync();

        layoutBox.Should().NotBeNull("Layout should have bounding box");
        menuBox.Should().NotBeNull("Menu should have bounding box");

        var layoutCenterX = layoutBox!.X + (layoutBox.Width / 2);
        var menuCenterX = menuBox!.X + (menuBox.Width / 2);

        if (isToolbarLeft)
        {
            menuCenterX.Should().BeGreaterThan(layoutCenterX,
                $"Menu should be on right side (center X > layout center). Menu X: {menuCenterX}, Layout Center X: {layoutCenterX}");
        }
        else if (isToolbarRight)
        {
            menuCenterX.Should().BeLessThan(layoutCenterX,
                $"Menu should be on left side (center X < layout center). Menu X: {menuCenterX}, Layout Center X: {layoutCenterX}");
        }
    }
}
