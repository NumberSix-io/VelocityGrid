# Columns and selection

`VelocityGridControl.SetColumns` accepts any positive number of `VelocityGridColumn` objects. Each column defines a stable application key, header, width in device-independent pixels, and left, centre, or right text alignment. Every requested page contains one value for each currently configured column.

Column metadata crosses the WinRT boundary as three arrays and is copied into native layout state. The renderer computes logical column bounds once per draw iteration, skips columns wholly outside the horizontal viewport, and clips partially visible columns through the Direct2D target. Headers share the same bounds and horizontal offset as body cells but remain fixed vertically.

Clicking a body cell maps the pointer position through the current horizontal offset, header height, row height, and leading-row offset. Selection is stored as logical row and column indexes and rendered without creating XAML cell elements. `SelectedRow`, `SelectedColumn`, and `SelectionChanged` expose that state to managed applications.

After pointer focus, the arrow keys move by one cell, Home and End move to the first and last configured columns, and Page Up and Page Down move by approximately one viewport. Navigation automatically adjusts both scroll offsets to keep the selected cell visible.

Keys must be non-empty and unique within the configured snapshot. The compatibility constructor uses the header as the key. Widths must be finite and at least 32 DIPs; the default is 130 DIPs. Column configuration is a snapshot: change visibility, order, headers, or widths by calling `SetColumns` again. The call cancels old requests, clears pages built with the old row shape, and updates the horizontal viewport. Interactive resize/reorder, sort gestures, ranges, and multi-selection are not implemented by the grid; applications can provide those controls and submit the resulting snapshot.

Row and column indexes in public events are zero-based. Accessibility announcements convert rows to a one-based human-readable number and use the configured header.
