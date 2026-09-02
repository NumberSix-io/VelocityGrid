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
        grid.NotifyDataChanged(1_100, VelocityGridDataChangeKind.Append);
        return grid;
    }
}
