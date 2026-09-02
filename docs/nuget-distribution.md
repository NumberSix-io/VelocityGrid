# NuGet distribution and consumption

VelocityGrid publishes three packages with one version:

| Package | Audience | Contents |
|---|---|---|
| `VelocityGrid.Native.WinUI` | C++/WinRT and transitive runtime use | WinMD, public projection headers, x86/x64/ARM64 DLLs and PRI files, Windows App SDK/C++/WinRT dependencies |
| `VelocityGrid.WinUI` | C# WinUI 3 | Thin managed control/provider API plus a dependency on the native package |
| `VelocityGrid.Wpf` | .NET 8 WPF | WPF `HwndHost` adapter plus a dependency on the C# package |

All executable consumers must select x64, x86, or ARM64. Package targets reject unsupported runtime identifiers rather than allowing a runtime `BadImageFormatException`.

## C# WinUI 3

```xml
<PropertyGroup>
  <TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
  <UseWinUI>true</UseWinUI>
  <PlatformTarget>x64</PlatformTarget>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="VelocityGrid.WinUI" Version="0.1.0-preview.1" />
</ItemGroup>
```

Add `VelocityGridControl` in WinUI XAML with `xmlns:vg="using:VelocityGrid.Managed"`, configure columns, and assign an `IVelocityGridDataProvider`. The application never references `VelocityGrid_Native` directly.

## WPF

Target `.NET 8` and a concrete Windows architecture, then install `VelocityGrid.Wpf`. Add `xmlns:vg="clr-namespace:VelocityGrid.Wpf;assembly=VelocityGrid.Wpf"` and place `vg:VelocityGridHost` in WPF XAML. Set `DataProvider`, `Columns`, and `RowHeight` normally. The host initializes and tears down `WindowsXamlManager`, `DesktopWindowXamlSource`, and the island child HWND.

The adapter is intentionally small. Use `GridReady` or the nullable `Grid` property when an advanced operation is not forwarded by the WPF host.

For an unpackaged WPF executable, the package defaults `WindowsPackageType` to `None` so the Windows App SDK bootstrap initializer runs automatically. An application that already declares a packaging model keeps its own setting.

## C++ WinUI 3

Install only `VelocityGrid.Native.WinUI`. The package brings compatible Windows App SDK and C++/WinRT build dependencies, and adds its projection header directory automatically:

```cpp
#include <winrt/VelocityGrid_Native.h>

winrt::VelocityGrid_Native::VelocityGrid grid;
grid.SetColumns(headers, widths, alignments);
grid.PageRequested([](int64_t start, int32_t count, uint64_t request, uint64_t generation)
{
    // Fetch/flatten count * configuredColumnCount values, then call CompletePage.
});
window.Content(grid.View());
```

The ABI deliberately uses WinRT-compatible primitives and parallel arrays. C++ callers can use it directly without loading the managed facade.

## Build local packages

From repository root in PowerShell:

```powershell
./build/Build-Packages.ps1
```

Override the immutable package version when required:

```powershell
./build/Build-Packages.ps1 -Version 0.1.0-preview.1
```

The script builds three native architectures, the managed and WPF adapters, creates packages under `VelocityGrid/artifacts/packages`, and compiles three independent package consumers. These consumers reference only the local feed, never source projects.

Managed WinUI and WPF packages also produce portable `.snupkg` symbol packages. The native C++ Windows PDB is retained as a build artifact rather than uploaded to NuGet.org, whose symbol server accepts only managed portable PDBs.

## Publishing

`.github/workflows/packages.yml` performs the same build on pull requests and uploads packages as workflow artifacts. A `vMAJOR.MINOR.PATCH[-suffix]` tag strips the leading `v` and publishes all `.nupkg` files when the repository has a `NUGET_API_KEY` secret.

Before tagging:

1. Reserve/confirm all three IDs on nuget.org.
2. Run the manual tests in the release checklist for x64 and at least build ARM64/x86.
3. Inspect package contents and dependency versions.
4. Test packaged and unpackaged WinUI deployment plus the runnable WPF package consumer.
5. Treat uploaded versions as immutable; increment the version for every correction.

The packaging follows Microsoft's recommended C#/WinRT model: distribute the architecture-neutral projection with architecture-specific implementation DLLs. It also ships generated C++ projection headers to support C++ consumers without per-project projection configuration.
