# Quickstart: Plugin Architecture & Two-Tier Split

## Build & test (off-device, the loop's exit condition)

```bash
dotnet build VanDaemon.sln
dotnet test tests/VanDaemon.Plugins.Ui.Tests/VanDaemon.Plugins.Ui.Tests.csproj
# or run the whole suite:
dotnet test VanDaemon.sln
```

Green `dotnet build` + `dotnet test` against FR-001..FR-009 = done (Class A, playbook §2).

## Author a UI plugin (Tier-2)

1. Create a Blazor component (`.razor`) for the UI. It may inject `IVanDaemonApiClient` and
   `INativeBridge` — **nothing else native**. No `System.IO`, no hardware plugin references.
2. Create an `IUiPlugin` descriptor pointing `ComponentType` at your component:

   ```csharp
   public sealed class MyTileUiPlugin : IUiPlugin
   {
       public string Id => "my-tile";
       public string Name => "My Tile";
       public Type ComponentType => typeof(MyTile);
       public string? Icon => Icons.Material.Filled.Dashboard;
       public int Order => 10;
   }
   ```

3. Register it (compiled-in) in `VanDaemon.Web/Program.cs`:

   ```csharp
   builder.Services.AddVanDaemonUiPlugins();   // registry + stub bridge + api client + reference plugin
   builder.Services.AddSingleton<IUiPlugin, MyTileUiPlugin>();
   ```

## Render registered plugins

`UiPluginHost.razor` enumerates `IUiPluginRegistry.Plugins` and renders each via
`<DynamicComponent Type="plugin.ComponentType" />`. Drop `<UiPluginHost />` into a page/layout.

## Reach a native capability

```csharp
@inject INativeBridge Bridge
...
var reversing = await Bridge.GetReversingStateAsync();   // off-device stub → false
await Bridge.OpenDspAsync();                              // off-device stub → no-op
```

Off-device this uses `StubNativeBridge`; on the head unit the launcher feature will supply a real
transport implementation — **no change to plugin code** (transport-agnostic contract).

## Boundaries (do not cross)

- No hardware/native/filesystem access in UI plugins — bridge only (§VI.2). A test
  (`TwoTierIsolationTests`) fails the build if the UI assembly references a hardware-tier/native assembly.
- Do not edit Tier-1 `ISensorPlugin`/`IControlPlugin` (out of scope).
- No real bridge transport, no on-device wiring here (deferred to the launcher feature).
