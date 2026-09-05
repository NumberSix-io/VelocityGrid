# VelocityGrid

VelocityGrid is an experimental, read-only WinUI 3 data grid for very large, remote, or rapidly changing datasets. It requests only the current viewport and a small prefetch window, keeps a bounded native cache, and renders cells with Direct2D/DirectWrite instead of creating a XAML element per cell.

The primary distribution is NuGet: an idiomatic C# WinUI facade, a WPF XAML Island host, and a native package for C++/WinRT. Viewport calculations, scrolling, caching, formatting, hit testing, and rendering remain native.

## Why VelocityGrid?

- Millions of logical rows without a matching `ObservableCollection`.
- Cancellable, viewport-driven page requests.
- Batched page completions and live updates—never one ABI call per cell.
- Cache memory bounded independently of logical row count.
- Caller-controlled foreground, background, and built-in icon formatting.
- Repeatable scrolling, cache, GC, and update measurements in the sample.

VelocityGrid is intentionally not an editable spreadsheet or a replacement for every commercial DataGrid. See [current limitations](#current-limitations).

## Status

The planned rendering, viewport, cache, provider, column/selection, performance, streaming-update, formatting, and initial accessibility/hardening phases are implemented. The repository is ready for evaluation and contribution, but the manual gates in the [1.0 release checklist](docs/release-checklist.md) remain before a production release.

## Prerequisites

- Windows 10 build 19041 or later; Windows 11 recommended.
- Visual Studio 2022 or later with **Desktop development with C++**, **.NET desktop development**, **Universal Windows Platform development**, and Windows App SDK/C++/WinRT tooling.
- Windows SDK 10.0.19041.0 or a compatible later SDK.
- .NET 8 SDK.
- x64 for the documented development and benchmark path.

The solution restores Windows App SDK 2.4 and transitive native packages from NuGet.

## Get started

1. Clone the repository.
2. Open `VelocityGrid/VelocityGrid.slnx` in Visual Studio.
3. Select **Debug | x64**.
4. Set `VelocityGrid.Sample.Basic` as the startup project.
5. Build and run the packaged sample.

The sample must be launched through Visual Studio or its installed Start-menu entry. Running its generated `.exe` directly is unsupported because the application requires package identity.

From a Visual Studio Developer PowerShell prompt:

```powershell
msbuild VelocityGrid\VelocityGrid.slnx /restore /m `
  /p:Configuration=Debug /p:Platform=x64

msbuild VelocityGrid\VelocityGrid.slnx /m `
  /p:Configuration=Release /p:Platform=x64
```

Use `/t:Restore,Build` if native packages have not been restored.

## Install from NuGet

Select a concrete application architecture (`x64`, `x86`, or `ARM64`); VelocityGrid is not an AnyCPU runtime component.

- C# WinUI 3: install `VelocityGrid.WinUI`.
- WPF: install `VelocityGrid.Wpf`; it transitively installs the C# facade and native runtime.
- C++/WinRT WinUI 3: install `VelocityGrid.Native.WinUI`; it supplies WinMD metadata, projection headers, and the matching native DLL.

Package consumers do not add project references, run C#/WinRT, or copy native files manually. See the complete [NuGet distribution and consumption guide](docs/nuget-distribution.md).

For an unpackaged C# WinUI executable, use the normal Microsoft host declarations, including an application manifest and `WindowsAppSDKSelfContained=true`. This keeps WinUI, composition, and the Interactive Experiences input binaries on one tested runtime graph; the complete project fragment is in the consumption guide. Packaged applications continue to use their package-declared Windows App Runtime dependency.

## Use from C# WinUI

```xml
<Window
    ...
    xmlns:velocity="using:VelocityGrid.Managed">
    <velocity:VelocityGridControl
        x:Name="TradesGrid"
        AutomationProperties.Name="Live trades" />
</Window>
```

```csharp
TradesGrid.RowHeight = 24;
TradesGrid.SetColumns(new[]
{
    new VelocityGridColumn("trade.symbol", "Symbol", 140),
    new VelocityGridColumn("trade.price", "Price", 110, VelocityGridTextAlignment.Right),
    new VelocityGridColumn("trade.status", "Status", 120, VelocityGridTextAlignment.Center)
});

TradesGrid.DataProvider = new TradeProvider();
TradesGrid.DataError += (_, e) => ShowProviderError(e.Exception);
```

Providers return one flat row-major page. `context.Columns` is the immutable column snapshot for that particular request, so a provider can safely map application keys after a column chooser reorders or hides fields:

```csharp
public sealed class TradeProvider : IVelocityGridDataProvider
{
    public long RowCount => 10_000_000;

    public async ValueTask<VelocityGridPage> GetRowsAsync(
        VelocityGridRange range,
        VelocityGridFetchContext context,
        CancellationToken cancellationToken)
    {
        var values = new string[range.RowCount * context.ColumnCount];
        var formats = new VelocityGridCellFormat[values.Length];
        await LoadDisplayValuesAsync(values, range, context.Columns, cancellationToken);
        return new VelocityGridPage(range.StartRow, range.RowCount, values, formats);
    }
}
```

Apply live changes in batches:

```csharp
TradesGrid.ApplyUpdates(new[]
{
    new VelocityGridCellUpdate(
        rowIndex: 999,
        columnIndex: 1,
        value: "-100.00",
        format: new VelocityGridCellFormat(
            VelocityGridColor.Red,
            VelocityGridColor.LightRed,
            VelocityGridIcon.DownArrow))
});
```

The grid applies exactly the visual state supplied by the caller. For a temporary background, send the coloured update and later send another update with `VelocityGridColor.None` as the background. Colours and icons have no semantic meaning to the grid; it does not infer direction or run formatting timers.

When the dataset extent or ordering changes, update the provider first and notify the grid explicitly:

```csharp
TradesGrid.NotifyDataChanged(provider.RowCount, VelocityGridDataChangeKind.Append);  // tail growth
TradesGrid.NotifyDataChanged(provider.RowCount, VelocityGridDataChangeKind.TrimEnd); // tail removal
TradesGrid.NotifyDataChanged(provider.RowCount, VelocityGridDataChangeKind.Reset);   // reorder/filter/arbitrary changes
TradesGrid.Refresh(resetScrollPosition: true);                                      // same-count sort/filter; return to top
TradesGrid.InvalidateRows(startRow: 500, rowCount: 25);                              // reload only changed rows
```

See [API/configuration](docs/api-reference.md), [providers](docs/provider-guide.md), [formatting](docs/cell-formatting.md), and [streaming updates](docs/streaming-updates.md) for full contracts and examples.

## Use from WPF

```xml
<Window ... xmlns:vg="clr-namespace:VelocityGrid.Wpf;assembly=VelocityGrid.Wpf">
    <vg:VelocityGridHost x:Name="TradesGrid" RowHeight="24" />
</Window>
```

```csharp
TradesGrid.DataProvider = new TradeProvider();
TradesGrid.Columns = columns;
```

`VelocityGridHost` owns the `DesktopWindowXamlSource` and HWND lifecycle. Its `Grid` property exposes the underlying managed control after `GridReady` for advanced operations.

## Use from C++/WinRT

```cpp
#include <winrt/VelocityGrid_Native.h>

winrt::VelocityGrid_Native::VelocityGrid grid;
grid.RowHeight(24.0);
Content(grid.View());
```

The native ABI is page/batch oriented. C++ applications handle `PageRequested`, then call `CompletePage` once per page. See [NuGet distribution and consumption](docs/nuget-distribution.md#c-winui-3).

## Test and benchmark

Build **Debug | x64**, then run `VelocityGrid.Managed.Tests` and `VelocityGrid.Native.Tests` through Visual Studio Test Explorer. These are packaged WinUI test applications, so `dotnet test` is not the supported path.

The basic sample provides:

- **Run Phase 6 benchmarks**: sequential scroll, random jump, cache revisit, and managed-GC stress.
- **Start market stream**: sparse caller-formatted changes; scroll while it runs to combine page churn and live updates.
- **Stop**: clears pending temporary backgrounds and leaves metrics visible.

Use Release x64 for comparable performance results. Debug builds are for diagnosis. See [performance.md](docs/performance.md).

## Architecture

```text
C# application/provider
        |
VelocityGrid.Managed      thin WinUI control and batched adapter
        |
VelocityGrid.Native       viewport, request policy, LRU cache, selection
        |
Direct2D / DirectWrite    immediate-mode headers and cells
        |
SwapChainPanel            small WinUI visual tree and scroll controls
```

- `VelocityGrid.Native`: the complete C++/WinRT WinUI control—surface, scrollbars, wheel/pointer/keyboard input, hit testing, selection, viewport, scheduler, bounded cache, renderer, diagnostics, theming, and resource recovery.
- Wheel and touchpad input use the same composition interaction model as WinUI's `ScrollPresenter`. A fixed-size floating interaction window is rebased around the current logical offset after inertia, retaining sub-DIP input precision across ten million 64-bit-addressed rows without creating a physical scroll canvas.
- `VelocityGrid.Managed`: a thin provider/API adapter, cancellation bridge, automation metadata, and theme propagation; it does not overlay or forward input to the native control.
- `VelocityGrid.Wpf`: WPF `HwndHost` and WinUI XAML Island lifecycle adapter.
- `VelocityGrid.*.Packaging`: independently versioned NuGet package definitions.
- `PackageTests`: source-independent C# WinUI, WPF, and C++/WinRT package consumers.
- `VelocityGrid.Sample.Basic`: provider, columns, benchmarks, caller formatting, and live-market examples.
- `VelocityGrid.Native.Tests`: viewport, scheduler, cache, and native lifecycle tests.
- `VelocityGrid.Managed.Tests`: provider, API, automation, validation, and control tests.

The [design plan](docs/VelocityGrid_Design_and_Development_Plan.md) explains the engineering thesis. Feature documents describe the current implementation.

## Configuration summary

| Option | Purpose | Constraint |
|---|---|---|
| `DataProvider` | Supplies cancellable viewport pages | One value per `context.Columns` entry per row |
| `RowCount` | Logical dataset size | Normally taken from provider |
| `RowHeight` | Fixed row height in DIPs | Minimum 8 |
| `SetColumns(...)` | Key, header, width, alignment | Unique keys; at least one; width ≥ 32 DIPs |
| `Refresh(...)` | Full same-snapshot cache reload | Can preserve position or return to row zero |
| `InvalidateRows(...)` | Targeted provider reload | Range must be inside the dataset |
| `ScrollToRow(...)` | Random logical jump | Clamped to dataset |
| `ApplyUpdates(...)` | Batched cached-cell changes | Uncached changes ignored |
| `SelectionChanged` | Logical cell selection | Single cell |
| `DataError` | Provider failure | Host owns recovery UI |
| `PerformanceMetrics` | Cache/request/render counters | Snapshot; explicitly reset |

## Current limitations

- Read-only; no in-cell editing or CRUD contract.
- Fixed row height; configured columns are horizontally virtualized.
- Single-cell selection; no ranges, clipboard export, or built-in sorting/filter UI.
- Provider-owned sorting/filtering and authoritative values for uncached pages.
- Built-in palette/icons only; no arbitrary templates, controls, brushes, or images in the fast path.
- The automation surface identifies the grid and announces selection but does not materialize millions of virtual cell peers.
- The development package identity/publisher must be replaced before distribution.

## Troubleshooting

- **Cells remain “Loading”:** assign a provider and return exactly `range.RowCount * context.ColumnCount` values. Subscribe to `DataError`.
- **Sample executable will not start:** launch/deploy the packaged project; do not run the `.exe` directly.
- **Native DLL missing:** select x64, x86, or ARM64 and restore the appropriate VelocityGrid package; do not use AnyCPU for the executable.
- **C++ package targets missing:** restore NuGet and install the C++/WinRT/Windows App SDK workloads.
- **Pointer wheel does not affect any control in an unpackaged app:** verify the executable uses its standard `app.manifest` and set `WindowsAppSDKSelfContained` to `true`; this deploys the matching WinUI and Interactive Experiences input stack together.
- **Release opens Debug:** the development identity may remain registered to Debug; launch the desired Visual Studio configuration or replace the registration.
- **FPS is near 60:** presentation is display-synchronized; compare duration, cache/requests, working set, and latency rather than FPS alone.

See [CONTRIBUTING.md](CONTRIBUTING.md), [SECURITY.md](SECURITY.md), and the [release checklist](docs/release-checklist.md).

## Licence

VelocityGrid is licensed under the MIT License. See [LICENSE](LICENSE).
