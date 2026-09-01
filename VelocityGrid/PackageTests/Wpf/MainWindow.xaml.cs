using System.Windows;
using VelocityGrid.Managed;

namespace VelocityGrid.PackageTests.Wpf;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        GridHost.DataProvider = new SyntheticDataProvider(1_000_000);
    }
}
