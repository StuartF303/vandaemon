namespace VanDaemon.Plugins.Ui.Abstractions;

/// <summary>
/// Tier-2 UI / presentation plugin contract. The contract is intentionally
/// <b>loading-mechanism-agnostic</b>: the host renders <see cref="ComponentType"/> regardless of how
/// the plugin was loaded (compiled-in today; manifest/lazy/sideloaded later), so the contract never
/// changes when the loading mechanism does.
///
/// UI plugins MUST NOT access native, hardware, or filesystem APIs directly. The only path to native
/// capability is <see cref="INativeBridge"/> (Constitution §VI.2).
/// </summary>
public interface IUiPlugin
{
    /// <summary>Stable unique identity. Duplicate ids are rejected at registration.</summary>
    string Id { get; }

    /// <summary>Human-readable display name.</summary>
    string Name { get; }

    /// <summary>
    /// The Blazor component type the host renders (expected to implement
    /// <c>Microsoft.AspNetCore.Components.IComponent</c>). Expressed as <see cref="Type"/> so this
    /// abstraction carries no Blazor or loading-mechanism dependency.
    /// </summary>
    Type ComponentType { get; }

    /// <summary>Optional icon name (e.g. a MudBlazor/material icon).</summary>
    string? Icon { get; }

    /// <summary>Sort order for deterministic enumeration; ties are broken by <see cref="Id"/>.</summary>
    int Order { get; }
}
