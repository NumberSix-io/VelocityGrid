using VelocityGrid.Managed;

namespace VelocityGrid.PackageTests.CSharp;

public static class PackageSmoke
{
    public static VelocityGridControl Create() => new()
    {
        DataProvider = new SyntheticDataProvider(1_000),
        RowHeight = 24
    };
}
