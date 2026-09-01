using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace VelocityGrid.Managed.Tests
{
    [TestClass]
    public partial class UnitTest1
    {
        [UITestMethod]
        public void NativePropertiesRoundTripThroughManagedControl()
        {
            var grid = new VelocityGrid.Managed.VelocityGridControl
            {
                RowCount = 10_000_000,
                RowHeight = 24
            };

            Assert.AreEqual(10_000_000, grid.RowCount);
            Assert.AreEqual(24, grid.RowHeight);
            Assert.IsNotNull(grid.Content);
        }

        [TestMethod]
        public async Task SyntheticProviderReturnsOneFlatPageBatch()
        {
            var provider = new VelocityGrid.Managed.SyntheticDataProvider(1_000);
            var page = await provider.GetRowsAsync(
                new VelocityGrid.Managed.VelocityGridRange(128, 2),
                new VelocityGrid.Managed.VelocityGridFetchContext(7, 3),
                CancellationToken.None);

            Assert.AreEqual(128, page.StartRow);
            Assert.AreEqual(2, page.RowCount);
            Assert.AreEqual(20, page.Values.Length);
            Assert.AreEqual("R128  C1", page.Values[0]);
            Assert.AreEqual("R129  C10", page.Values[19]);
        }

        [TestMethod]
        public async Task RemoteProviderHonorsCancellation()
        {
            var provider = new VelocityGrid.Managed.SimulatedRemoteDataProvider(
                new VelocityGrid.Managed.SyntheticDataProvider(1_000),
                TimeSpan.FromSeconds(5));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () =>
                await provider.GetRowsAsync(
                    new VelocityGrid.Managed.VelocityGridRange(0, 1),
                    new VelocityGrid.Managed.VelocityGridFetchContext(1, 1),
                    cancellation.Token));
        }

        [UITestMethod]
        public void ProviderControlsLogicalRowCount()
        {
            var grid = new VelocityGrid.Managed.VelocityGridControl
            {
                DataProvider = new VelocityGrid.Managed.SyntheticDataProvider(1_000)
            };

            Assert.AreEqual(1_000, grid.RowCount);
        }

        [TestMethod]
        public void ColumnRejectsWidthsBelowMinimum()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                new VelocityGrid.Managed.VelocityGridColumn("Invalid", 20));
        }

        [UITestMethod]
        public void ColumnsRoundTripThroughManagedControl()
        {
            var grid = new VelocityGrid.Managed.VelocityGridControl();
            grid.SetColumns(new[]
            {
                new VelocityGrid.Managed.VelocityGridColumn("Name", 180),
                new VelocityGrid.Managed.VelocityGridColumn(
                    "Value", 100, VelocityGrid.Managed.VelocityGridTextAlignment.Right)
            });

            Assert.AreEqual(2, grid.Columns.Count);
            Assert.AreEqual("Name", grid.Columns[0].Header);
            Assert.AreEqual(100, grid.Columns[1].Width);
            Assert.AreEqual(-1, grid.SelectedRow);
            Assert.AreEqual(-1, grid.SelectedColumn);
        }

        [UITestMethod]
        public void PerformanceMetricsCanBeReset()
        {
            var grid = new VelocityGrid.Managed.VelocityGridControl { RowCount = 1_000_000 };
            grid.ScrollToRow(500_000);
            Assert.AreEqual(500_000, grid.FirstVisibleRow);
            grid.ResetPerformanceMetrics();
            Assert.AreEqual(0UL, grid.PerformanceMetrics.FrameCount);
            Assert.AreEqual(0UL, grid.PerformanceMetrics.CacheHits);
            Assert.AreEqual(0UL, grid.PerformanceMetrics.CacheMisses);
        }

        [TestMethod]
        public void CellUpdateValidatesCoordinates()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                new VelocityGrid.Managed.VelocityGridCellUpdate(-1, 0, "value"));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                new VelocityGrid.Managed.VelocityGridCellUpdate(0, 10, "value"));
        }

        [TestMethod]
        public void PageFormattingMustMatchCellCount()
        {
            Assert.ThrowsExactly<ArgumentException>(() => new VelocityGrid.Managed.VelocityGridPage(
                0, 1, new string[VelocityGrid.Managed.VelocityGridControl.ColumnCount],
                new VelocityGrid.Managed.VelocityGridCellFormat[1]));
        }

        [TestMethod]
        public async Task SyntheticProviderSuppliesCompactFormatting()
        {
            var provider = new VelocityGrid.Managed.SyntheticDataProvider(100);
            var page = await provider.GetRowsAsync(
                new VelocityGrid.Managed.VelocityGridRange(0, 1),
                new VelocityGrid.Managed.VelocityGridFetchContext(1, 1), default);

            Assert.AreEqual(VelocityGrid.Managed.VelocityGridIcon.Up, page.Formats[5].Icon);
            Assert.AreEqual(page.Values.Length, page.Formats.Length);
        }

        [UITestMethod]
        public void GridExposesDataGridAutomationIdentity()
        {
            var grid = new VelocityGrid.Managed.VelocityGridControl();
            var peer = Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.CreatePeerForElement(grid);

            Assert.IsNotNull(peer);
            Assert.AreEqual(Microsoft.UI.Xaml.Automation.Peers.AutomationControlType.DataGrid,
                peer.GetAutomationControlType());
            Assert.AreEqual("VelocityGrid", peer.GetName());
        }

        [TestMethod]
        public void DataErrorPreservesRangeAndException()
        {
            var exception = new InvalidOperationException("provider failed");
            var error = new VelocityGrid.Managed.VelocityGridDataErrorEventArgs(
                new VelocityGrid.Managed.VelocityGridRange(128, 64), exception);

            Assert.AreEqual(128L, error.RequestedRange.StartRow);
            Assert.AreSame(exception, error.Exception);
        }

    }
}
