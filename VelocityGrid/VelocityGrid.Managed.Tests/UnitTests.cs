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
        public void GridAcceptsMoreThanTenColumns()
        {
            var grid = new VelocityGrid.Managed.VelocityGridControl();
            grid.SetColumns(System.Linq.Enumerable.Select(
                System.Linq.Enumerable.Range(1, 24),
                index => new VelocityGrid.Managed.VelocityGridColumn($"Column {index}")));

            Assert.AreEqual(24, grid.Columns.Count);
        }

        [UITestMethod]
        public void PerformanceMetricsCanBeReset()
        {
            var grid = new VelocityGrid.Managed.VelocityGridControl { RowCount = 1_000_000 };
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
                new VelocityGrid.Managed.VelocityGridCellUpdate(0, -1, "value"));
        }

        [TestMethod]
        public void PageFormattingMustMatchCellCount()
        {
            Assert.ThrowsExactly<ArgumentException>(() => new VelocityGrid.Managed.VelocityGridPage(
                0, 1, new string[12],
                new VelocityGrid.Managed.VelocityGridCellFormat[1]));
        }

        [TestMethod]
        public async Task SyntheticProviderSuppliesCompactFormatting()
        {
            var provider = new VelocityGrid.Managed.SyntheticDataProvider(100);
            var page = await provider.GetRowsAsync(
                new VelocityGrid.Managed.VelocityGridRange(0, 1),
                new VelocityGrid.Managed.VelocityGridFetchContext(1, 1), default);

            Assert.AreEqual(VelocityGrid.Managed.VelocityGridIcon.UpArrow, page.Formats[5].Icon);
            Assert.AreEqual(page.Values.Length, page.Formats.Length);
        }

        [TestMethod]
        public async Task SyntheticProviderUsesRequestedColumnCount()
        {
            var provider = new VelocityGrid.Managed.SyntheticDataProvider(100);
            var page = await provider.GetRowsAsync(
                new VelocityGrid.Managed.VelocityGridRange(0, 2),
                new VelocityGrid.Managed.VelocityGridFetchContext(1, 1, 24), default);

            Assert.AreEqual(24, page.ColumnCount);
            Assert.AreEqual(48, page.Values.Length);
            Assert.AreEqual("R1  C24", page.Values[47]);
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

        [TestMethod]
        public void FormattingCatalogueAbiValuesRemainStable()
        {
            Assert.AreEqual((byte)7, (byte)VelocityGrid.Managed.VelocityGridColor.Red);
            Assert.AreEqual((byte)25, (byte)VelocityGrid.Managed.VelocityGridColor.Brown);
            Assert.AreEqual((byte)2, (byte)VelocityGrid.Managed.VelocityGridIcon.DownArrow);
            Assert.AreEqual((byte)28, (byte)VelocityGrid.Managed.VelocityGridIcon.Edit);
        }

        [UITestMethod]
        public void DataChangeModesUpdateAndValidateLogicalExtent()
        {
            var grid = new VelocityGrid.Managed.VelocityGridControl { RowCount = 100 };

            grid.NotifyDataChanged(125, VelocityGrid.Managed.VelocityGridDataChangeKind.Append);
            Assert.AreEqual(125, grid.RowCount);
            grid.NotifyDataChanged(80, VelocityGrid.Managed.VelocityGridDataChangeKind.TrimEnd);
            Assert.AreEqual(80, grid.RowCount);
            grid.NotifyDataChanged(80, VelocityGrid.Managed.VelocityGridDataChangeKind.Reset);
            Assert.AreEqual(80, grid.RowCount);

            Assert.ThrowsExactly<ArgumentException>(() =>
                grid.NotifyDataChanged(79, VelocityGrid.Managed.VelocityGridDataChangeKind.Append));
            Assert.ThrowsExactly<ArgumentException>(() =>
                grid.NotifyDataChanged(81, VelocityGrid.Managed.VelocityGridDataChangeKind.TrimEnd));
        }

        [TestMethod]
        public void DataChangeKindAbiValuesRemainStable()
        {
            Assert.AreEqual(0, (int)VelocityGrid.Managed.VelocityGridDataChangeKind.Append);
            Assert.AreEqual(1, (int)VelocityGrid.Managed.VelocityGridDataChangeKind.TrimEnd);
            Assert.AreEqual(2, (int)VelocityGrid.Managed.VelocityGridDataChangeKind.Reset);
        }

    }
}
