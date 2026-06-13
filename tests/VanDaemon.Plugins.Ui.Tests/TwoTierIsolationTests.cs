using FluentAssertions;
using VanDaemon.Plugins.Ui.Bridge;
using Xunit;

namespace VanDaemon.Plugins.Ui.Tests;

/// <summary>
/// Structural guard for the two-tier split (Constitution §I.3/§VI.2, FR-008/FR-009): the Tier-2 UI
/// assembly must not reference the Tier-1 hardware plugin tier or any hardware/native driver library.
/// </summary>
public class TwoTierIsolationTests
{
    // The hardware tier + concrete hardware plugins + their native driver libs. None may be a
    // reference of the UI assembly.
    private static readonly string[] ForbiddenAssemblies =
    {
        "VanDaemon.Plugins.Abstractions",     // Tier-1 hardware abstractions
        "VanDaemon.Plugins.Modbus",
        "VanDaemon.Plugins.I2C",
        "VanDaemon.Plugins.Victron",
        "VanDaemon.Plugins.Simulated",
        "VanDaemon.Plugins.MqttLedDimmer",
        "FluentModbus",                       // Modbus native driver
        "MQTTnet"                             // MQTT native driver
    };

    [Fact]
    public void UiAssembly_References_NoHardwareOrNativeTier()
    {
        var uiAssembly = typeof(StubNativeBridge).Assembly;

        var referenced = uiAssembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .Where(n => n is not null)
            .Select(n => n!)
            .ToList();

        referenced.Should().NotIntersectWith(ForbiddenAssemblies,
            "Tier-2 UI plugins must reach native/hardware capability only through INativeBridge (Constitution §VI.2)");
    }

    [Fact]
    public void UiAssembly_DoesReference_UiAbstractions()
    {
        // Sanity: it depends on the Tier-2 contract project (not the hardware one).
        var referenced = typeof(StubNativeBridge).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name);

        referenced.Should().Contain("VanDaemon.Plugins.Ui.Abstractions");
    }
}
