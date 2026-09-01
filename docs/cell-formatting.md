# Cell formatting

VelocityGrid is a read-only display grid. Providers may attach a compact `VelocityGridCellFormat` to every page value, and streaming updates may replace a value and its format in the same batch.

Formatting uses neutral identifiers with no application meaning attached. Foreground and background both use `VelocityGridColor`; icons use `VelocityGridIcon`. The bounded catalogues keep each cached field to one byte and avoid brushes, images, templates, or managed callbacks in the render path.

`VelocityGridColor` contains `None` plus 25 colours: `Black`, `White`, `DarkGray`, `Gray`, `LightGray`, `DarkRed`, `Red`, `LightRed`, `Orange`, `Amber`, `Yellow`, `Lime`, `DarkGreen`, `Green`, `LightGreen`, `Teal`, `Cyan`, `DarkBlue`, `Blue`, `LightBlue`, `Indigo`, `Violet`, `Purple`, `Pink`, and `Brown`. For a foreground, `None` uses the grid's normal theme text colour. For a background, `None` performs no cell fill.

`VelocityGridIcon` contains `None` plus `UpArrow`, `DownArrow`, `LeftArrow`, `RightArrow`, `UpTriangle`, `DownTriangle`, `Check`, `Cross`, `Warning`, `Information`, `Star`, `Circle`, `Square`, `Diamond`, `Plus`, `Minus`, `Play`, `Pause`, `Stop`, `Clock`, `Flag`, `Heart`, `Lightning`, `Bell`, `Lock`, `Unlock`, `Search`, and `Edit`.

```csharp
var format = new VelocityGridCellFormat(
    VelocityGridColor.Green,
    VelocityGridColor.LightGreen,
    VelocityGridIcon.UpArrow);

grid.ApplyUpdates(new[] { new VelocityGridCellUpdate(row, column, value, format) });
```

Page formats cross the ABI as parallel byte arrays in the same coarse-grained completion as display values. The native cache stores the compact format and the renderer resolves palette colours and icons without managed callbacks.

The grid applies exactly the state supplied by the caller. It does not infer colours, run formatting timers, or clear a background automatically. A caller that wants a 500 ms price flash sends one update with a coloured background, waits 500 ms in application/provider code, then sends another update with the resting background. This keeps timing and visual policy outside the grid and allows the same generic update API to support many domains.

If another value arrives before a scheduled clear, replace that clear and retain the newest value/format. Do not allow an old delayed update to restore stale content. The basic sample keeps one pending clear per `(row, column)` to demonstrate this.

Applications should not encode meaning with colour alone; pair important colour states with text or an icon where accessibility requires it.
