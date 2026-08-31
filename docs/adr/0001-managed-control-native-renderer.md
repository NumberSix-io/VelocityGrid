# ADR-0001: Managed control shell with native renderer

Status: Accepted for the rendering spike  
Date: 31 August 2026

## Context

The manually created native project uses the classic C++/WinRT Windows Runtime Component template. Its metadata merge pipeline does not support directly deriving the authored runtime class from a Windows App SDK XAML class reliably. C#/.NET also requires a generated C#/WinRT projection rather than consuming the WinMD directly.

## Decision

Keep viewport calculations, scrolling state, synthetic data generation, Direct3D, Direct2D, DirectWrite, swap-chain presentation, and diagnostics in `VelocityGrid.Native`. Expose the native-created visual root through the WinRT boundary. Provide the public C# `VelocityGridControl` as a thin `UserControl` in `VelocityGrid.Managed`.

## Consequences

The performance-critical body remains native and no per-cell ABI calls are introduced. C# consumers receive an idiomatic WinUI control. Native C++ consumers temporarily use the native host object rather than a directly derived XAML custom control. A later template/toolchain spike may replace this shell without changing the viewport or rendering core.
