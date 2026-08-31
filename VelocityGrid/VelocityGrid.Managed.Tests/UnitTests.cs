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
    }
}
