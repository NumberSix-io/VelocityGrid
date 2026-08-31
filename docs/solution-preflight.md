# Solution preflight

Date: 31 August 2026  
Primary configuration: Debug | x64

## Result

The initial preflight found the intended five-project architecture, but failed configuration validation. The smallest corrections needed to preserve that architecture have now been applied.

| Check | Initial state | Resolution |
|---|---|---|
| Five expected projects | Passed | Preserved unchanged |
| Project languages and template families | Passed | Preserved unchanged |
| Windows SDK compatibility | Mixed 19041/26100 targets | Standardized the component and .NET projection on 10.0.19041 |
| Windows App SDK compatibility | Native component lacked WinUI references | Added the required aggregate/transitive native metadata and targets |
| Primary CPU configuration | Ambiguous Any CPU/ARM mappings | Reduced solution platforms to x64, x86, and ARM64 with explicit Win32 mapping |
| C++20 | Missing in the component and conditional in tests | Enabled C++20 and conformance mode in both projects |
| Native WinRT component | Passed, but based on the older UWP WRC template | Retained the runtime component and enabled desktop compatibility |
| Packaged C# sample | Passed | Connected to the managed wrapper and native component |
| Managed WinUI library | Passed | Added C#/WinRT projection generation |
| Test compatibility | Templates passed but references were missing | Added managed and native production-project references |
| Project dependency graph | Failed: no references | Added native → managed → sample/test build dependencies |
| Baseline build | Initially blocked by sandbox SDK discovery, then exposed configuration issues | Corrected and revalidated through MSBuild |

## Interoperability note

The older Windows Runtime Component template cannot reliably merge a runtime class that directly derives from a Windows App SDK XAML class. The native component therefore exposes a native-created `UIElement`, while `VelocityGrid.Managed` supplies the public C# `UserControl`. Viewport state, scrolling, graphics resources, row synthesis, and rendering remain native. This is recorded in ADR-0001 and can be revisited when the native project is migrated to a current Windows App SDK component template.
