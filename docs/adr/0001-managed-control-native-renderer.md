# ADR-0001: Native WinUI control with managed API adapter

Status: Accepted
Date: 4 September 2026

## Context

VelocityGrid must behave consistently when consumed from C++/WinRT, C# WinUI, or the WPF XAML Island host. Input routed through a language-specific host creates split ownership, fragile hit testing, and different behavior between consumers.

## Decision

`VelocityGrid_Native.VelocityGrid` is a C++/WinRT WinUI `UserControl` and is the complete visual and interaction boundary. It owns the render surface, vertical and horizontal scrollbars, focus, keyboard input, pointer input, wheel routing, hit testing, selection, viewport state, Direct3D/Direct2D/DirectWrite rendering, and diagnostics.

The native root visual is a `VisualInteractionSource` owned by an `InteractionTracker`, following the public composition architecture used by WinUI's `ScrollPresenter`. Pointer-wheel and precision-touchpad redirection are enabled on that source, and the tracker owner converts position changes directly into native viewport offsets. Wheel input anywhere in the control—including over either scrollbar—is therefore processed by one C++ interaction implementation without host-specific HWND hooks, transparent overlays, or managed input forwarding.

`InteractionTracker` exposes single-precision positions, whereas VelocityGrid exposes 64-bit row indices and double-precision logical offsets. The tracker therefore operates inside a fixed 1,048,576-DIP floating window. The native control retains a double-precision origin, maps local tracker movement to the logical offset, and rebases the window only after interaction inertia becomes idle or after a programmatic scroll. This preserves natural DIP-sized wheel motion and sub-row precision at the end of a ten-million-row dataset without allocating a corresponding physical canvas.

`VelocityGrid.Managed.VelocityGridControl` is a thin public API and data-provider adapter. It hosts the native control and performs batched data marshaling, cancellation, automation metadata, and theme propagation. It does not place input overlays over the native control, install window hooks, or forward pointer and keyboard events.

## Consequences

All consumers exercise the same native interaction implementation, and no per-cell ABI calls are introduced. Future interaction features—such as richer hit testing, hover state, column resizing, drag selection, and context actions—extend the native control rather than adding consumer-specific interception layers.

Unpackaged WinUI executables must make their Windows App SDK deployment model explicit. The reference C# sample uses a standard application manifest and self-contained Windows App SDK deployment so WinUI and its Interactive Experiences input components are version-aligned. This is a host responsibility and does not move any VelocityGrid interaction into managed code.

The managed type remains useful as the idiomatic C# provider surface, but it must not become a second UI implementation. The native `View` property is retained only as a compatibility alias for hosts written against the earlier ABI; it returns the native control itself rather than exposing its private visual root.
