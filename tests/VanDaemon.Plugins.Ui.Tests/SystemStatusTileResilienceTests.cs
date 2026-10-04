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

/// <summary>
/// FR-007 / SC-002 (008-native-bridge-transport): on a head unit with no VanDaemon backend reachable,
/// the reference tile must render a clear offline line, never surface an unhandled exception. An
/// unguarded fetch would trip Blazor's error boundary, which the head-unit install guide reads as
/// "WebView too old" — a false SC-006 failure. These tests lock that guard in place.
/// </summary>
public class SystemStatusTileResilienceTests : TestContext
{
    [Fact]
    public void WhenApiThrows_RendersOfflineLine_AndDoesNotThrow()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();

        var apiMock = new Mock<IVanDaemonApiClient>();
        apiMock.Setup(c => c.GetTanksAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("no controller reachable"));
        Services.AddSingleton(apiMock.Object);
        Services.AddSingleton<INativeBridge>(new StubNativeBridge());

        var act = () => RenderComponent<SystemStatusTile>();

        var cut = act.Should().NotThrow().Subject;
        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid=status-offline]").TextContent.Should().Contain("offline");
            // The bridge value still renders — the offline state is scoped to the API fetch.
            cut.Find("[data-testid=status-reversing]").TextContent.Should().Contain("False");
        });
    }
}
