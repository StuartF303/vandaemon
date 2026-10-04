using Bunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using VanDaemon.Plugins.Ui.Abstractions;
using VanDaemon.Plugins.Ui.Bridge;
using Xunit;

namespace VanDaemon.Plugins.Ui.Tests;

/// <summary>
/// FR-002 + fail-safe G5 (008-native-bridge-transport): the JS-interop bridge maps each
/// <see cref="INativeBridge"/> operation onto the <c>vandaemonBridge.*</c> transport shim
/// (reversing → bool, ACC → string name, openDsp → no-op, wheel-key → push into .NET), and every
/// call fails safe — a bridge fault (e.g. a call before native readiness) is swallowed and yields the
/// contract's defined default rather than throwing. The <see cref="NativeBridgeFactory"/> and the
/// tile resilience are covered elsewhere; this locks the bridge's own request/response and push
/// mapping, which is exercised end-to-end only by the on-device Class C round-trip (005 SC-007).
/// </summary>
public sealed class JsInteropNativeBridgeTests : TestContext
{
    private const string ShimGetReversing = "vandaemonBridge.getReversingState";
    private const string ShimGetAcc = "vandaemonBridge.getAccState";
    private const string ShimOpenDsp = "vandaemonBridge.openDsp";
    private const string ShimRegisterWheelKey = "vandaemonBridge.registerWheelKey";
    private const string ShimUnregisterWheelKey = "vandaemonBridge.unregisterWheelKey";

    public JsInteropNativeBridgeTests()
    {
        // Loose so the fire-and-forget constructor register + best-effort teardown auto-resolve;
        // individual tests still configure the specific returns/exceptions they assert on.
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private JsInteropNativeBridge CreateBridge() =>
        new(JSInterop.JSRuntime, Mock.Of<ILogger<JsInteropNativeBridge>>());

    // ---- request/response mapping (FR-002) -------------------------------------------------------

    [Fact]
    public async Task GetReversingStateAsync_ReturnsNativeBoolean()
    {
        JSInterop.Setup<bool>(ShimGetReversing).SetResult(true);
        await using var bridge = CreateBridge();

        var reversing = await bridge.GetReversingStateAsync();

        reversing.Should().BeTrue();
    }

    [Theory]
    [InlineData("On", AccState.On)]
    [InlineData("Off", AccState.Off)]
    [InlineData("Unknown", AccState.Unknown)]
    [InlineData("on", AccState.On)]           // shim names cross the wire; parse is case-insensitive
    public async Task GetAccStateAsync_ParsesNativeStringName(string wireName, AccState expected)
    {
        JSInterop.Setup<string>(ShimGetAcc).SetResult(wireName);
        await using var bridge = CreateBridge();

        var state = await bridge.GetAccStateAsync();

        state.Should().Be(expected);
    }

    [Fact]
    public async Task GetAccStateAsync_WhenNativeReturnsUnmappedName_FallsBackToUnknown()
    {
        JSInterop.Setup<string>(ShimGetAcc).SetResult("SomethingReverseEngineeredLater");
        await using var bridge = CreateBridge();

        var state = await bridge.GetAccStateAsync();

        state.Should().Be(AccState.Unknown);
    }

    [Fact]
    public async Task OpenDspAsync_InvokesTheOpenDspShim()
    {
        JSInterop.SetupVoid(ShimOpenDsp).SetVoidResult();
        await using var bridge = CreateBridge();

        await bridge.OpenDspAsync();

        JSInterop.VerifyInvoke(ShimOpenDsp);
    }

    // ---- fail-safe (contract G5): a bridge fault must never surface to the caller ----------------

    [Fact]
    public async Task GetReversingStateAsync_WhenShimThrows_ReturnsSafeDefaultFalse()
    {
        JSInterop.Setup<bool>(ShimGetReversing).SetException(new JSException("native not ready"));
        await using var bridge = CreateBridge();

        var reversing = await bridge.GetReversingStateAsync();

        reversing.Should().BeFalse();
    }

    [Fact]
    public async Task GetAccStateAsync_WhenShimThrows_ReturnsUnknown()
    {
        JSInterop.Setup<string>(ShimGetAcc).SetException(new JSException("native not ready"));
        await using var bridge = CreateBridge();

        var state = await bridge.GetAccStateAsync();

        state.Should().Be(AccState.Unknown);
    }

    [Fact]
    public async Task OpenDspAsync_WhenShimThrows_DoesNotThrow()
    {
        JSInterop.SetupVoid(ShimOpenDsp).SetException(new JSException("native not ready"));
        await using var bridge = CreateBridge();

        var act = async () => await bridge.OpenDspAsync();

        await act.Should().NotThrowAsync();
    }

    // ---- native -> UI wheel-key push (FR-002), parsed leniently ----------------------------------

    [Fact]
    public async Task OnWheelKey_WithValidPayload_RaisesWheelKeyPressedWithParsedValues()
    {
        await using var bridge = CreateBridge();
        WheelKeyEvent? received = null;
        bridge.WheelKeyPressed += (_, e) => received = e;

        bridge.OnWheelKey("VolumeUp", "2026-07-19T10:00:00Z");

        received.Should().NotBeNull();
        received!.Key.Should().Be(WheelKey.VolumeUp);
        received.TimestampUtc.Offset.Should().Be(TimeSpan.Zero);
        received.TimestampUtc.UtcDateTime.Should().Be(new DateTime(2026, 7, 19, 10, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task OnWheelKey_IsCaseInsensitiveOnKeyName()
    {
        await using var bridge = CreateBridge();
        WheelKeyEvent? received = null;
        bridge.WheelKeyPressed += (_, e) => received = e;

        bridge.OnWheelKey("volumedown", "2026-07-19T10:00:00Z");

        received!.Key.Should().Be(WheelKey.VolumeDown);
    }

    [Fact]
    public async Task OnWheelKey_NormalisesOffsetTimestampsToUtc()
    {
        await using var bridge = CreateBridge();
        WheelKeyEvent? received = null;
        bridge.WheelKeyPressed += (_, e) => received = e;

        // 10:00 at +02:00 is 08:00 UTC — the C# side stores UTC (DateTimeStyles.AdjustToUniversal).
        bridge.OnWheelKey("Next", "2026-07-19T10:00:00+02:00");

        received!.TimestampUtc.Offset.Should().Be(TimeSpan.Zero);
        received.TimestampUtc.UtcDateTime.Should().Be(new DateTime(2026, 7, 19, 8, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task OnWheelKey_WithUnmappedKeyName_FallsBackToUnknown()
    {
        await using var bridge = CreateBridge();
        WheelKeyEvent? received = null;
        bridge.WheelKeyPressed += (_, e) => received = e;

        bridge.OnWheelKey("SomeKeyNotYetMapped", "2026-07-19T10:00:00Z");

        received!.Key.Should().Be(WheelKey.Unknown);
    }

    [Fact]
    public async Task OnWheelKey_WithUnparseableTimestamp_FallsBackToMinValue()
    {
        await using var bridge = CreateBridge();
        WheelKeyEvent? received = null;
        bridge.WheelKeyPressed += (_, e) => received = e;

        bridge.OnWheelKey("VolumeUp", "not-a-timestamp");

        received!.Key.Should().Be(WheelKey.VolumeUp);
        received.TimestampUtc.Should().Be(DateTimeOffset.MinValue);
    }

    // ---- lifecycle: register on construct, unregister on dispose ---------------------------------

    [Fact]
    public async Task Construction_RegistersTheNativeToUiWheelKeyCallback()
    {
        await using var bridge = CreateBridge();

        JSInterop.VerifyInvoke(ShimRegisterWheelKey);
    }

    [Fact]
    public async Task DisposeAsync_UnregistersTheWheelKeyCallback()
    {
        var bridge = CreateBridge();

        await bridge.DisposeAsync();

        JSInterop.VerifyInvoke(ShimUnregisterWheelKey);
    }

    [Fact]
    public async Task DisposeAsync_WhenUnregisterShimThrows_DoesNotThrow()
    {
        JSInterop.SetupVoid(ShimUnregisterWheelKey).SetException(new JSException("context gone"));
        var bridge = CreateBridge();

        var act = async () => await bridge.DisposeAsync();

        await act.Should().NotThrowAsync();
    }
}
