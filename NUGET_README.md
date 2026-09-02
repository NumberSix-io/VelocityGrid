# VelocityGrid

VelocityGrid is a read-only, high-performance Windows data grid for very large, remote, or rapidly changing datasets. Viewport calculation, caching, scrolling, and Direct2D rendering remain native while C# applications use a thin managed API.

## Choose a package

- **C# WinUI 3:** `VelocityGrid.WinUI`
- **WPF on .NET 8:** `VelocityGrid.Wpf`
- **C++/WinRT WinUI 3:** `VelocityGrid.Native.WinUI`

Use a concrete Windows architecture: `x64`, `x86`, or `ARM64`.

## C# WinUI 3

```xml
<PackageReference Include="VelocityGrid.WinUI" Version="0.1.0-preview.1" />
```

```xml
<Window ... xmlns:vg="using:VelocityGrid.Managed">
    <vg:VelocityGridControl x:Name="TradesGrid" />
</Window>
```

Configure columns and supply pages through `IVelocityGridDataProvider`. The managed package installs the correct native runtime transitively.

Columns can carry stable application keys for column chooser projections. Provider requests receive the exact immutable column snapshot. For changing datasets, use `NotifyDataChanged`, `Refresh`, or targeted `InvalidateRows`; the caller remains responsible for updating its provider snapshot before notifying the grid.

## WPF

```xml
<PackageReference Include="VelocityGrid.Wpf" Version="0.1.0-preview.1" />
```

```xml
<Window ... xmlns:vg="clr-namespace:VelocityGrid.Wpf;assembly=VelocityGrid.Wpf">
    <vg:VelocityGridHost x:Name="TradesGrid" RowHeight="24" />
</Window>
```

The host owns the WinUI XAML Island lifecycle and automatically enables Windows App SDK initialization for unpackaged WPF executables.

## C++/WinRT

Install `VelocityGrid.Native.WinUI`, then use the supplied projection headers:

```cpp
#include <winrt/VelocityGrid_Native.h>

winrt::VelocityGrid_Native::VelocityGrid grid;
grid.RowHeight(24.0);
Content(grid.View());
```

## Documentation

- [Complete setup and usage](https://github.com/deanchalk/VelocityGrid#readme)
- [Managed API and configuration](https://github.com/deanchalk/VelocityGrid/blob/main/docs/api-reference.md)
- [Provider implementation](https://github.com/deanchalk/VelocityGrid/blob/main/docs/provider-guide.md)
- [Cell formatting](https://github.com/deanchalk/VelocityGrid/blob/main/docs/cell-formatting.md)
- [NuGet distribution details](https://github.com/deanchalk/VelocityGrid/blob/main/docs/nuget-distribution.md)
- [Issues and support](https://github.com/deanchalk/VelocityGrid/issues)

VelocityGrid is an early preview. Please report compatibility and performance findings through GitHub Issues.
