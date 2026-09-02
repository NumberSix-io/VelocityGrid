using VelocityGrid.Managed;

namespace VelocityGrid.PackageTests.CSharp;

public static class PackageSmoke
{
    public static VelocityGridControl Create()
    {
        var grid = new VelocityGridControl
        {
            DataProvider = new SyntheticDataProvider(1_000),
            RowHeight = 24
        };
        grid.SetColumns(new[] { new VelocityGridColumn("symbol", "Symbol") });
        grid.NotifyDataChanged(1_100, VelocityGridDataChangeKind.Append);
        grid.InvalidateRows(0, 1);
        grid.Refresh();
        return grid;
    }
}
