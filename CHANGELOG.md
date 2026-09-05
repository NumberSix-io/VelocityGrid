# Changelog

## Unreleased

## [0.1.0-preview.7] - 2026-09-05

- Defer managed native-view creation until after parent XAML parsing completes.
- Add an explicit default-interface creation export for unpackaged managed hosts.
- Preserve pre-load row, column, provider, and row-height configuration.
- Route native wheel and touchpad scrolling through a `ScrollPresenter`-style composition interaction source.
- Preserve sub-row scrolling precision across ten million rows with a bounded floating-origin interaction window.
- Clip partially visible rows to the data viewport so they cannot paint over the fixed header.
- Copy the architecture-matched native runtime beside C++ package consumers and merge registration-free activation metadata.
- Document the standard manifest and self-contained Windows App SDK requirements for unpackaged WinUI hosts.
- Add ten-million-row precision and bounded-working-set regression tests.

## [0.1.0-preview.6] - 2026-09-03

- Fix WinUI control construction after adding managed mouse-wheel routing.

## [0.1.0-preview.5] - 2026-09-03

- Fix mouse-wheel scrolling when the WinUI grid is hosted through the managed C# `UserControl` wrapper.

All notable changes to VelocityGrid are documented here. Versions follow Semantic Versioning.

## [0.1.0-preview.4] - 2026-09-03

### Fixed

- Embedded registration-free WinRT activation metadata into native C++ application manifests.

## [0.1.0-preview.3] - 2026-09-03

### Fixed

- Attached WinUI 3 XAML Islands through the supported `WindowId` hosting API instead of the legacy UWP native interface.
- Registered the native WinRT activation factory in managed host processes and built the native component against the desktop Visual C++ runtime.

## [0.1.0-preview.2] - 2026-09-03

### Fixed

- Enabled Windows App SDK bootstrap initialization reliably for unpackaged WPF consumers.
- Added automatic WinUI `DispatcherQueue` creation and lifetime management to the WPF host.
- Declared explicit project platforms so Visual Studio maps solution configurations correctly.

## [0.1.0-preview.1] - 2026-09-01

First public preview.

### Added

- Native C++/WinRT WinUI 3 grid with viewport-driven data virtualization.
- Thin managed API for C# WinUI applications.
- WPF XAML Island host with automatic Windows App SDK initialization.
- Caller-controlled foreground, background, and built-in icon formatting.
- Batched live updates, selection, keyboard navigation, accessibility, and performance metrics.
- Explicit append, end-trim, and full-reset notifications for changing datasets.
- Stable keyed column snapshots, refresh scroll policy, and targeted row-range invalidation.
- x86, x64, and ARM64 native runtime assets.
- Package-only C# WinUI, WPF, and C++/WinRT validation consumers.

[0.1.0-preview.1]: https://github.com/NumberSix-io/VelocityGrid/releases/tag/v0.1.0-preview.1
[0.1.0-preview.2]: https://github.com/NumberSix-io/VelocityGrid/releases/tag/v0.1.0-preview.2
[0.1.0-preview.3]: https://github.com/NumberSix-io/VelocityGrid/releases/tag/v0.1.0-preview.3
[0.1.0-preview.4]: https://github.com/NumberSix-io/VelocityGrid/releases/tag/v0.1.0-preview.4
[0.1.0-preview.5]: https://github.com/NumberSix-io/VelocityGrid/releases/tag/v0.1.0-preview.5
[0.1.0-preview.6]: https://github.com/NumberSix-io/VelocityGrid/releases/tag/v0.1.0-preview.6
[0.1.0-preview.7]: https://github.com/NumberSix-io/VelocityGrid/releases/tag/v0.1.0-preview.7
