# Accessibility and high contrast

VelocityGrid exposes a DataGrid automation identity with a stable accessible name, read-only keyboard guidance, and a polite live region. Selection changes publish the logical one-based row and column header without creating an automation or XAML object for every logical cell. Provider failures are announced through the same live region and are also exposed through the managed `DataError` event.

Keyboard selection supports arrow keys, Home, End, Page Up, and Page Down. Pointer selection transfers keyboard focus to the grid, and the native focus rectangle follows the WinUI control's actual focus state.

The managed control observes WinUI theme changes and Windows high-contrast changes. It sends a compact visual-theme mode to the native renderer. In high contrast, provider-supplied foreground/background palettes are suppressed in favour of system-legible black, white, and yellow rendering; data values and icons remain available.

The grid deliberately exposes one bounded automation surface rather than materialising peers for millions of rows. A later accessibility validation pass may add virtualized item/grid providers if testing with Narrator and other screen readers shows that cell-level navigation is required.

## Host guidance

- Set `AutomationProperties.Name` to describe the dataset, as the basic sample does.
- Subscribe to `DataError` to present application-specific recovery UI.
- Do not encode meaning using colour alone; combine semantic colours with an icon or text.
- Validate the host application with Narrator, keyboard-only input, light/dark themes, and Windows contrast themes.
