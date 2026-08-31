# VelocityGrid

VelocityGrid is an experimental, viewport-driven WinUI data grid for very large logical datasets. Its rendering and scrolling hot path is implemented in C++20 with C++/WinRT, Direct2D, and DirectWrite; C# applications consume it through a thin WinUI wrapper.

The current implementation displays ten million synthetic rows without materializing them. It includes fixed-height viewport calculations, logical random access, mouse-wheel and scrollbar navigation, ten synthetic columns, and a delayed native data pipeline with directional prefetch, bounded LRU caching, request cancellation, generation-based stale-result suppression, and live diagnostics.

## Requirements

- Windows 10 version 2004 or later
- Visual Studio 2022 or later with Desktop development with C++, Universal Windows Platform development, and Windows App SDK tooling
- Windows 10 SDK 10.0.19041.0
- .NET 8 SDK

## Build

Open `VelocityGrid/VelocityGrid.slnx`, select `Debug | x64`, set `VelocityGrid.Sample.Basic` as the startup project, and build the solution.

```powershell
msbuild VelocityGrid/VelocityGrid.slnx /restore /m /p:Configuration=Debug /p:Platform=x64
```

The packaged native and managed test applications can be run through Visual Studio Test Explorer.

`VelocityGrid.Sample.Basic` is a packaged WinUI application and should be launched from Visual Studio (or from its installed Start menu entry). Running its generated `.exe` directly does not provide package identity and is not a supported launch path.

## Architecture

- `VelocityGrid.Native`: viewport engine, WinRT boundary, logical scrolling, and Direct2D/DirectWrite renderer.
- `VelocityGrid.Managed`: C#/WinRT projection and idiomatic WinUI `VelocityGridControl` wrapper.
- `VelocityGrid.Sample.Basic`: packaged C# WinUI sample.
- `VelocityGrid.Native.Tests`: native viewport and integration tests.
- `VelocityGrid.Managed.Tests`: managed wrapper and future adapter tests.
- `docs`: design plan, preflight findings, and architecture decisions.

The managed package exposes `IVelocityGridDataProvider`, with synthetic in-memory and delayed remote-provider examples. Fetches and cancellations cross the ABI once per page; cached cell rendering remains native. The streaming-update contract remains a future phase.

## Status

The native rendering, cache/request scheduler, and managed-provider adapter phases are implemented. This is not yet a production-ready grid; see `docs/VelocityGrid_Design_and_Development_Plan.md` for the roadmap.

## Licence

VelocityGrid is licensed under the MIT License. See `LICENSE`.
