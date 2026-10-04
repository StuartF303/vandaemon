using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using VanDaemon.Plugins.Ui.Abstractions;
using VanDaemon.Plugins.Ui.Hosting;
using Xunit;

namespace VanDaemon.Plugins.Ui.Tests;

public class UiPluginRegistryTests
{
    private static UiPluginRegistry CreateRegistry(params IUiPlugin[] plugins) =>
        new(plugins, NullLogger<UiPluginRegistry>.Instance);

    // FR-001: a fake plugin implementing the contract is discoverable by the registry.
    [Fact]
    public void FakePlugin_IsDiscoverable()
    {
        var plugin = new FakeUiPlugin("alpha", "Alpha");

        var registry = CreateRegistry(plugin);

        registry.Plugins.Should().ContainSingle()
            .Which.Id.Should().Be("alpha");
    }

    // FR-002: given N registered plugins, the host reports N and yields each, ordered.
    [Fact]
    public void Enumerates_All_Registered_InOrder()
    {
        var a = new FakeUiPlugin("a", "A", order: 2);
        var b = new FakeUiPlugin("b", "B", order: 1);
        var c = new FakeUiPlugin("c", "C", order: 1);

        var registry = CreateRegistry(a, b, c);

        registry.Plugins.Should().HaveCount(3);
        // ordered by Order then Id: b(1), c(1), a(2)
        registry.Plugins.Select(p => p.Id).Should().ContainInOrder("b", "c", "a");
    }

    // FR-003: the contract is loading-mechanism-agnostic — the render entry is a plain Type,
    // so it carries no compiled-in/manifest/lazy/sideloaded assumption.
    [Fact]
    public void Contract_RenderEntry_IsLoaderAgnosticType()
    {
        var componentTypeProperty = typeof(IUiPlugin).GetProperty(nameof(IUiPlugin.ComponentType));

        componentTypeProperty.Should().NotBeNull();
        componentTypeProperty!.PropertyType.Should().Be(typeof(Type),
            "the render entry must be a plain System.Type so the contract assumes no loading mechanism");
    }

    // Edge case: duplicate ids are rejected (no silent overwrite).
    [Fact]
    public void Register_DuplicateId_Throws()
    {
        var registry = CreateRegistry(new FakeUiPlugin("dup", "First"));

        var act = () => registry.Register(new FakeUiPlugin("dup", "Second"));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*dup*");
        registry.Plugins.Should().ContainSingle();
    }

    private sealed class FakeUiPlugin : IUiPlugin
    {
        public FakeUiPlugin(string id, string name, int order = 0)
        {
            Id = id;
            Name = name;
            Order = order;
        }

        public string Id { get; }
        public string Name { get; }
        public Type ComponentType => typeof(object); // registry does not validate component type
        public string? Icon => null;
        public int Order { get; }
    }
}
