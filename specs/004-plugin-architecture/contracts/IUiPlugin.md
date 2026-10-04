# Contract: IUiPlugin & IUiPluginRegistry

Namespace: `VanDaemon.Plugins.Ui.Abstractions`

```csharp
namespace VanDaemon.Plugins.Ui.Abstractions;

/// <summary>
/// Tier-2 UI/presentation plugin contract. Loading-mechanism-agnostic: the host renders
/// <see cref="ComponentType"/> via DynamicComponent regardless of how the plugin was loaded.
/// UI plugins MUST NOT access native/hardware/filesystem APIs directly — only via INativeBridge.
/// </summary>
public interface IUiPlugin
{
    /// <summary>Stable unique identity. Duplicate ids are rejected at registration.</summary>
    string Id { get; }

    /// <summary>Human-readable display name.</summary>
    string Name { get; }

    /// <summary>The Blazor component type to render (must implement IComponent).</summary>
    Type ComponentType { get; }

    /// <summary>Optional icon name.</summary>
    string? Icon { get; }

    /// <summary>Sort order for deterministic enumeration (ties broken by Id).</summary>
    int Order { get; }
}
```

```csharp
namespace VanDaemon.Plugins.Ui.Abstractions;

/// <summary>Discovers and enumerates registered UI plugins for the Blazor host.</summary>
public interface IUiPluginRegistry
{
    /// <summary>All registered plugins, ordered by Order then Id.</summary>
    IReadOnlyList<IUiPlugin> Plugins { get; }

    /// <summary>Register a plugin. Throws if a plugin with the same Id is already registered.</summary>
    void Register(IUiPlugin plugin);
}
```

## Behavioral guarantees (testable)

- Registering N distinct plugins → `Plugins.Count == N`, each present, ordered by `Order` then `Id`. (FR-002)
- A fake plugin implementing `IUiPlugin` is discoverable after `Register`. (FR-001)
- Registering a duplicate `Id` throws a clear exception (no silent overwrite). (Edge case)
- The interface references only `System.Type` for the render entry — no loading-mechanism assumption. (FR-003 / SC-004)
