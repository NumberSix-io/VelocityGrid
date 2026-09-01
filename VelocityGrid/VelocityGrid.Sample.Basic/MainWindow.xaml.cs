using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace VelocityGrid.Sample.Basic
{
    /// <summary>Interactive provider, streaming-update, and benchmark sample.</summary>
    public sealed partial class MainWindow : Window
    {
        private readonly VelocityGrid.Managed.SyntheticDataProvider _syntheticProvider = new(10_000_000);
        private readonly VelocityGrid.Managed.SimulatedRemoteDataProvider _remoteProvider;
        private readonly DispatcherTimer _marketTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
        private readonly DispatcherTimer _flashTimer = new() { Interval = TimeSpan.FromMilliseconds(25) };
        private readonly Random _marketRandom = new(73);
        private readonly Dictionary<long, double> _marketPrices = new();
        private readonly Dictionary<(long Row, int Column), PendingFlash> _pendingFlashes = new();
        private int _marketTicks;

        public MainWindow()
        {
            InitializeComponent();
            _remoteProvider = new VelocityGrid.Managed.SimulatedRemoteDataProvider(
                _syntheticProvider, TimeSpan.FromMilliseconds(100));
            GridControl.DataProvider = _remoteProvider;
            GridControl.RowHeight = 24;
            GridControl.SetColumns(new[]
            {
                new VelocityGrid.Managed.VelocityGridColumn("Row", 100, VelocityGrid.Managed.VelocityGridTextAlignment.Right),
                new VelocityGrid.Managed.VelocityGridColumn("Symbol", 150),
                new VelocityGrid.Managed.VelocityGridColumn("Description", 240),
                new VelocityGrid.Managed.VelocityGridColumn("Quantity", 120, VelocityGrid.Managed.VelocityGridTextAlignment.Right),
                new VelocityGrid.Managed.VelocityGridColumn("Price", 120, VelocityGrid.Managed.VelocityGridTextAlignment.Right),
                new VelocityGrid.Managed.VelocityGridColumn("Status", 130, VelocityGrid.Managed.VelocityGridTextAlignment.Center),
                new VelocityGrid.Managed.VelocityGridColumn("Venue", 120),
                new VelocityGrid.Managed.VelocityGridColumn("Updated", 160),
                new VelocityGrid.Managed.VelocityGridColumn("Owner", 140),
                new VelocityGrid.Managed.VelocityGridColumn("Notes", 220),
                new VelocityGrid.Managed.VelocityGridColumn("Bid", 120, VelocityGrid.Managed.VelocityGridTextAlignment.Right),
                new VelocityGrid.Managed.VelocityGridColumn("Ask", 120, VelocityGrid.Managed.VelocityGridTextAlignment.Right),
                new VelocityGrid.Managed.VelocityGridColumn("Change", 110, VelocityGrid.Managed.VelocityGridTextAlignment.Right),
                new VelocityGrid.Managed.VelocityGridColumn("Volume", 140, VelocityGrid.Managed.VelocityGridTextAlignment.Right),
                new VelocityGrid.Managed.VelocityGridColumn("Currency", 100, VelocityGrid.Managed.VelocityGridTextAlignment.Center),
                new VelocityGrid.Managed.VelocityGridColumn("Exchange Time", 170)
            });
            _marketTimer.Tick += OnMarketTick;
            _flashTimer.Tick += OnFlashTick;
        }

        private void StartMarket_Click(object sender, RoutedEventArgs e)
        {
            GridControl.ResetPerformanceMetrics();
            _marketTicks = 0;
            _marketPrices.Clear();
            _pendingFlashes.Clear();
            _marketTimer.Start();
            _flashTimer.Start();
            StartMarketButton.IsEnabled = false;
            RunBenchmarksButton.IsEnabled = false;
            StopMarketButton.IsEnabled = true;
        }

        private void StopMarket_Click(object sender, RoutedEventArgs e)
        {
            _marketTimer.Stop();
            ClearPendingFlashes();
            _flashTimer.Stop();
            StartMarketButton.IsEnabled = true;
            RunBenchmarksButton.IsEnabled = true;
            StopMarketButton.IsEnabled = false;
            ShowMarketMetrics();
        }

        private void OnMarketTick(object? sender, object e)
        {
            var first = GridControl.FirstVisibleRow;
            var last = GridControl.LastVisibleRow;
            if (last < first) return;
            // A realistic feed changes a sparse, irregular subset of the viewport.
            var updatesPerTick = _marketRandom.Next(3, 7);
            var updates = new VelocityGrid.Managed.VelocityGridCellUpdate[updatesPerTick];
            for (var index = 0; index < updates.Length; ++index)
            {
                var row = _marketRandom.NextInt64(first, last + 1);
                var column = _marketRandom.Next(10) switch { < 6 => 4, < 8 => 5, _ => 7 };
                var oldPrice = _marketPrices.TryGetValue(row, out var cachedPrice)
                    ? cachedPrice
                    : 90 + _marketRandom.NextDouble() * 20;
                var newPrice = Math.Clamp(oldPrice + (_marketRandom.NextDouble() - 0.5) * 0.30, 80, 120);
                if (column == 4) _marketPrices[row] = newPrice;
                var rising = newPrice >= oldPrice;
                var value = column switch
                {
                    4 => $"{newPrice:F4}",
                    5 => _marketRandom.Next(4) == 0 ? "UPDATED" : "LIVE",
                    _ => DateTime.Now.ToString("HH:mm:ss.fff")
                };
                var restingFormat = column switch
                {
                    4 when rising => new VelocityGrid.Managed.VelocityGridCellFormat(
                        VelocityGrid.Managed.VelocityGridColor.Green,
                        VelocityGrid.Managed.VelocityGridColor.None,
                        VelocityGrid.Managed.VelocityGridIcon.UpArrow),
                    4 => new VelocityGrid.Managed.VelocityGridCellFormat(
                        VelocityGrid.Managed.VelocityGridColor.Red,
                        VelocityGrid.Managed.VelocityGridColor.None,
                        VelocityGrid.Managed.VelocityGridIcon.DownArrow),
                    5 => new VelocityGrid.Managed.VelocityGridCellFormat(
                        VelocityGrid.Managed.VelocityGridColor.Amber,
                        VelocityGrid.Managed.VelocityGridColor.None,
                        VelocityGrid.Managed.VelocityGridIcon.Warning),
                    _ => new VelocityGrid.Managed.VelocityGridCellFormat(
                        VelocityGrid.Managed.VelocityGridColor.Gray)
                };
                var flashBackground = restingFormat.Foreground switch
                {
                    VelocityGrid.Managed.VelocityGridColor.Green => VelocityGrid.Managed.VelocityGridColor.LightGreen,
                    VelocityGrid.Managed.VelocityGridColor.Red => VelocityGrid.Managed.VelocityGridColor.LightRed,
                    VelocityGrid.Managed.VelocityGridColor.Amber => VelocityGrid.Managed.VelocityGridColor.Yellow,
                    _ => VelocityGrid.Managed.VelocityGridColor.None
                };
                var flashFormat = restingFormat with { Background = flashBackground };
                updates[index] = new(row, column, value, flashFormat);
                // The sample—not the grid—owns transient formatting policy. Replacing
                // this entry prevents an older clear from overwriting a newer price.
                _pendingFlashes[(row, column)] = new(value, restingFormat, DateTimeOffset.UtcNow.AddMilliseconds(500));
            }
            GridControl.ApplyUpdates(updates);
            if (++_marketTicks % 60 == 0) ShowMarketMetrics();
        }

        private void OnFlashTick(object? sender, object e)
        {
            var now = DateTimeOffset.UtcNow;
            var expired = _pendingFlashes
                .Where(item => item.Value.Due <= now)
                .Select(item => (item.Key, item.Value))
                .ToArray();
            if (expired.Length == 0) return;

            GridControl.ApplyUpdates(expired.Select(item => new VelocityGrid.Managed.VelocityGridCellUpdate(
                item.Key.Row, item.Key.Column, item.Value.Value, item.Value.RestingFormat)));
            foreach (var item in expired)
            {
                // A newer update may have replaced this scheduled clear while the
                // batch was being assembled; never clear that newer visual state.
                if (_pendingFlashes.TryGetValue(item.Key, out var current) && current.Due == item.Value.Due)
                    _pendingFlashes.Remove(item.Key);
            }
        }

        private void ClearPendingFlashes()
        {
            if (_pendingFlashes.Count == 0) return;
            GridControl.ApplyUpdates(_pendingFlashes.Select(item => new VelocityGrid.Managed.VelocityGridCellUpdate(
                item.Key.Row, item.Key.Column, item.Value.Value, item.Value.RestingFormat)));
            _pendingFlashes.Clear();
        }

        private readonly record struct PendingFlash(string Value,
            VelocityGrid.Managed.VelocityGridCellFormat RestingFormat, DateTimeOffset Due);

        private void ShowMarketMetrics()
        {
            var metrics = GridControl.PerformanceMetrics;
            var updatesPerRender = metrics.UpdateRenderCount == 0 ? 0 :
                metrics.UpdateCellCount / (double)metrics.UpdateRenderCount;
            BenchmarkStatus.Text = $"Market stream | batches {metrics.UpdateBatchCount:N0} | cached cells {metrics.UpdateCellCount:N0} | renders {metrics.UpdateRenderCount:N0} | {updatesPerRender:F1} updates/render | visible latency {metrics.LastUpdateLatencyMicroseconds / 1000.0:F2} ms";
        }

        private async void RunBenchmarks_Click(object sender, RoutedEventArgs e)
        {
            RunBenchmarksButton.IsEnabled = false;
            try
            {
                // Isolate grid, cache, and allocation costs from simulated network latency.
                GridControl.DataProvider = _syntheticProvider;
                var report = new StringBuilder();
#if DEBUG
                report.AppendLine("Build: Debug x64");
#else
                report.AppendLine("Build: Release x64");
#endif
                await RunScrollScenario(report, "1M sequential", 1_000_000, false);
                await RunScrollScenario(report, "10M sequential", 10_000_000, false);
                await RunScrollScenario(report, "10M random", 10_000_000, true);
                await RunCacheScenario(report);
                await RunGcStressScenario(report);
                BenchmarkStatus.Text = report.ToString();
                Debug.WriteLine(report.ToString());
            }
            finally
            {
                GridControl.DataProvider = _remoteProvider;
                RunBenchmarksButton.IsEnabled = true;
            }
        }

        private async Task RunScrollScenario(StringBuilder report, string name, long rowCount, bool random)
        {
            BenchmarkStatus.Text = $"Running {name}...";
            GridControl.RowCount = rowCount;
            GridControl.ResetPerformanceMetrics();
            var generator = new Random(42);
            var stopwatch = Stopwatch.StartNew();
            const int iterations = 240;
            for (var index = 0; index < iterations; ++index)
            {
                var row = random ? generator.NextInt64(rowCount) : index * (rowCount / iterations);
                GridControl.ScrollToRow(row);
                await Task.Yield();
            }
            stopwatch.Stop();
            AppendResult(report, name, stopwatch, GridControl.PerformanceMetrics);
        }

        private async Task RunCacheScenario(StringBuilder report)
        {
            BenchmarkStatus.Text = "Running cache revisit...";
            GridControl.RowCount = 10_000_000;
            GridControl.ResetPerformanceMetrics();
            var stopwatch = Stopwatch.StartNew();
            for (var pass = 0; pass < 8; ++pass)
            {
                for (var row = 0; row <= 1_024; row += 128)
                {
                    GridControl.ScrollToRow(row);
                    await Task.Yield();
                }
            }
            stopwatch.Stop();
            AppendResult(report, "Cache revisit", stopwatch, GridControl.PerformanceMetrics);
        }

        private async Task RunGcStressScenario(StringBuilder report)
        {
            BenchmarkStatus.Text = "Running managed GC stress...";
            var provider = new VelocityGrid.Managed.SyntheticDataProvider(10_000_000);
            GC.Collect();
            var collections = GC.CollectionCount(0);
            var before = GC.GetTotalMemory(true);
            var stopwatch = Stopwatch.StartNew();
            for (var index = 0; index < 500; ++index)
                await provider.GetRowsAsync(
                    new VelocityGrid.Managed.VelocityGridRange(index * 128L, 128),
                    new VelocityGrid.Managed.VelocityGridFetchContext((ulong)index, 1),
                    default);
            stopwatch.Stop();
            var after = GC.GetTotalMemory(false);
            report.AppendLine($"GC stress: {stopwatch.ElapsedMilliseconds} ms | Gen0 {GC.CollectionCount(0) - collections} | managed delta {(after - before) / 1024.0:F1} KiB");
        }

        private static void AppendResult(StringBuilder report, string name, Stopwatch stopwatch,
            VelocityGrid.Managed.VelocityGridPerformanceMetrics metrics)
        {
            var fps = stopwatch.Elapsed.TotalSeconds == 0 ? 0 : metrics.FrameCount / stopwatch.Elapsed.TotalSeconds;
            var workingSet = Process.GetCurrentProcess().WorkingSet64 / (1024.0 * 1024.0);
            report.AppendLine($"{name}: {stopwatch.ElapsedMilliseconds} ms | {fps:F1} FPS | cache {metrics.CacheHitPercent:F1}% | requests {metrics.RequestCount} | working set {workingSet:F1} MiB");
        }
    }
}
