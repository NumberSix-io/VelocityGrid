# VelocityGrid.Native

Native C++20/C++/WinRT runtime component containing the performance-critical grid implementation.

Key areas:

- `Viewport`: fixed-height logical viewport and scroll clamping.
- `Data`: page representation and synthetic asynchronous scheduler.
- `Cache`: bounded eight-page LRU cache and in-place cached updates.
- `VelocityGrid.idl`: coarse-grained WinRT boundary consumed by the managed projection.
- `VelocityGrid.cpp`: WinUI chrome, request policy, selection, Direct2D/DirectWrite rendering, diagnostics, theming, and device recovery.

Build the solution rather than this project in isolation so projection generation, tests, and sample staging use the same platform/configuration. IDL changes regenerate C++/WinRT and C#/WinRT outputs during the build; edit the IDL and implementation, not generated ABI code.

The native runtime class exposes a created `UIElement` rather than deriving directly from a Windows App SDK XAML type. The rationale is documented in [ADR-0001](../../docs/adr/0001-managed-control-native-renderer.md).

Hot-path invariants: no per-cell managed callbacks, no XAML body cells, bounded cache state, and no provider I/O while drawing.
