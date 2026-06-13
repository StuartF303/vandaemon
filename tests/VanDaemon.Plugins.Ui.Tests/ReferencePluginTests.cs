using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor.Services;
using VanDaemon.Plugins.Ui.Abstractions;
using VanDaemon.Plugins.Ui.Api;
using VanDaemon.Plugins.Ui.Bridge;
using VanDaemon.Plugins.Ui.ReferencePlugin;
using Xunit;

namespace VanDaemon.Plugins.Ui.Tests;

public class ReferencePluginTests : TestContext
{
    private void RegisterSeam(IReadOnlyList<TankDto> tanks)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();

        var apiMock = new Mock<IVanDaemonApiClient>();
        apiMock.Setup(c => c.GetTanksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tanks);
        Services.AddSingleton(apiMock.Object);

        // Native capability is supplied ONLY via the bridge stub — no native services in the container.
        Services.AddSingleton<INativeBridge>(new StubNativeBridge());
    }

    // FR-006: the reference plugin renders using only the contract + mocked API client + stub bridge.
    [Fact]
    public void Renders_With_StubBridge_And_MockedApi()
    {
        RegisterSeam(new List<TankDto> { new() { Name = "Fresh Water", CurrentLevel = 72 } });

        var cut = RenderComponent<SystemStatusTile>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid=status-tank]").TextContent.Should().Contain("Fresh Water");
            cut.Find("[data-testid=status-reversing]").TextContent.Should().Contain("False");
        });
    }

    // FR-006 / §VI.2: rendering succeeds with ONLY the bridge stub + mocked API present — the plugin
    // reaches native state only through INativeBridge (no native services registered).
    [Fact]
    public void ReferencePlugin_RendersUsingOnlyBridgeAndApi()
    {
        RegisterSeam(new List<TankDto>());

        var act = () => RenderComponent<SystemStatusTile>();

        act.Should().NotThrow();
    }
}
