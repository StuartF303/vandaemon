using FluentAssertions;
using VanDaemon.Plugins.Ui.Abstractions;
using VanDaemon.Plugins.Ui.Bridge;
using Xunit;

namespace VanDaemon.Plugins.Ui.Tests;

public class StubNativeBridgeTests
{
    // FR-004: the stub satisfies the contract with no native/WebView dependency present.
    [Fact]
    public void Stub_Satisfies_Contract()
    {
        INativeBridge bridge = new StubNativeBridge();
        bridge.Should().BeAssignableTo<INativeBridge>();
    }

    // FR-005: every capability returns a defined default and never throws (off-device).
    [Fact]
    public async Task Stub_Returns_Defined_Defaults()
    {
        INativeBridge bridge = new StubNativeBridge();

        (await bridge.GetReversingStateAsync()).Should().BeFalse();
        (await bridge.GetAccStateAsync()).Should().Be(AccState.Unknown);

        var openDsp = async () => await bridge.OpenDspAsync();
        await openDsp.Should().NotThrowAsync();
    }

    // The wheel-key event is wirable even though the stub never raises it on its own.
    [Fact]
    public void Stub_WheelKeyEvent_IsWirable()
    {
        var stub = new StubNativeBridge();
        WheelKeyEvent? received = null;
        stub.WheelKeyPressed += (_, e) => received = e;

        var evt = new WheelKeyEvent(WheelKey.VolumeUp, DateTimeOffset.UnixEpoch);
        stub.RaiseWheelKey(evt);

        received.Should().Be(evt);
    }
}
