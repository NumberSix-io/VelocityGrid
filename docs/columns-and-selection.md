# Columns and selection

`VelocityGridControl.SetColumns` accepts between one and ten `VelocityGridColumn` objects. Each column defines a header, width in device-independent pixels, and left, centre, or right text alignment. The current page payload retains ten values per row; a configured subset controls which of those values is displayed.

Column metadata crosses the WinRT boundary as three arrays and is copied into native layout state. The renderer computes logical column bounds once per draw iteration, skips columns wholly outside the horizontal viewport, and clips partially visible columns through the Direct2D target. Headers share the same bounds and horizontal offset as body cells but remain fixed vertically.

Clicking a body cell maps the pointer position through the current horizontal offset, header height, row height, and leading-row offset. Selection is stored as logical row and column indexes and rendered without creating XAML cell elements. `SelectedRow`, `SelectedColumn`, and `SelectionChanged` expose that state to managed applications.

After pointer focus, the arrow keys move by one cell, Home and End move to the first and last configured columns, and Page Up and Page Down move by approximately one viewport. Navigation automatically adjusts both scroll offsets to keep the selected cell visible.
