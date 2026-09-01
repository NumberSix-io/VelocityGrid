# Cell formatting

VelocityGrid is a read-only display grid. Providers may attach a compact `VelocityGridCellFormat` to every page value, and streaming updates may replace a value and its format in the same batch.

The initial bounded formatting vocabulary contains foreground and background palette identifiers and a fixed icon catalogue (`Up`, `Down`, `Warning`, and `Information`). It intentionally does not accept arbitrary brushes, images, templates, or per-cell callbacks.

```csharp
var format = new VelocityGridCellFormat(
    VelocityGridForeground.Positive,
    VelocityGridBackground.Positive,
    VelocityGridIcon.Up);

grid.ApplyUpdates(new[] { new VelocityGridCellUpdate(row, column, value, format) });
```

Page formats cross the ABI as parallel byte arrays in the same coarse-grained completion as display values. The native cache stores the compact format and the renderer resolves palette colours and icons without managed callbacks.

The grid applies exactly the state supplied by the caller. It does not infer colours, run formatting timers, or clear a background automatically. A caller that wants a 500 ms price flash sends one update with a coloured background, waits 500 ms in application/provider code, then sends another update with the resting background. This keeps timing and visual policy outside the grid and allows the same generic update API to support many domains.
