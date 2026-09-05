using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VelocityGrid.Managed;

namespace VelocityGrid.Sample.WinUI.CSharp;

public sealed partial class MainWindow : Window
{
    private readonly TradeProvider _provider = new(10_000_000);
    private bool _compact;
    private readonly DispatcherTimer _marketTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly Random _random = new(73);
    private readonly Dictionary<long, PendingFlash> _flashes = new();

    public MainWindow()
    {
        InitializeComponent();
        TradesGrid.RowHeight = 25;
        SetColumns();
        TradesGrid.DataProvider = _provider;
        TradesGrid.DataError += (_, e) => Status.Text = e.Exception.Message;
        Status.Text = $"{_provider.RowCount:N0} rows — NuGet.org package";
        _marketTimer.Tick += MarketTick;
        _marketTimer.Start();
        Closed += (_, _) => _marketTimer.Stop();
    }

    private void Append_Click(object sender, RoutedEventArgs e)
    {
        _provider.Append(1_000);
        TradesGrid.NotifyDataChanged(_provider.RowCount, VelocityGridDataChangeKind.Append);
        Status.Text = $"{_provider.RowCount:N0} rows";
    }

    private void Invalidate_Click(object sender, RoutedEventArgs e) => TradesGrid.InvalidateRows(0, 128);

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        _provider.Descending = !_provider.Descending;
        TradesGrid.Refresh(resetScrollPosition: true);
        Status.Text = _provider.Descending ? "Descending" : "Ascending";
    }

    private void Columns_Click(object sender, RoutedEventArgs e)
    {
        _compact = !_compact;
        SetColumns();
    }

    private void End_Click(object sender, RoutedEventArgs e)
    {
        var target = Math.Max(0, _provider.RowCount - 100);
        TradesGrid.ScrollToRow(target);
        Status.Text = $"Near end — target row {target:N0} of {_provider.RowCount:N0}";
    }

    private void SetColumns() => TradesGrid.SetColumns(_compact
        ? new[] { new VelocityGridColumn("symbol", "Symbol", 150), new VelocityGridColumn("price", "Price", 120, VelocityGridTextAlignment.Right) }
        : new[] { new VelocityGridColumn("id", "Row", 100, VelocityGridTextAlignment.Right), new VelocityGridColumn("symbol", "Symbol", 150), new VelocityGridColumn("price", "Price", 120, VelocityGridTextAlignment.Right), new VelocityGridColumn("status", "Status", 130), new VelocityGridColumn("venue", "Venue", 120), new VelocityGridColumn("notes", "Notes", 260) });

    private void MarketTick(object? sender, object e)
    {
        var now = DateTimeOffset.UtcNow;
        var priceColumn = _compact ? 1 : 2;
        var clears = _flashes.Where(x => x.Value.Due <= now).ToArray();
        if (clears.Length > 0)
        {
            TradesGrid.ApplyUpdates(clears.Select(x => new VelocityGridCellUpdate(
                x.Key, priceColumn, x.Value.Value, x.Value.RestingFormat)));
            foreach (var clear in clears)
                if (_flashes.TryGetValue(clear.Key, out var current) && current.Due == clear.Value.Due)
                    _flashes.Remove(clear.Key);
        }

        var first = TradesGrid.FirstVisibleRow;
        var last = TradesGrid.LastVisibleRow;
        if (last < first) return;
        var updates = new List<VelocityGridCellUpdate>();
        for (var i = 0; i < 3; i++)
        {
            var row = _random.NextInt64(first, last + 1);
            var value = 80 + _random.NextDouble() * 40;
            var rising = _random.Next(2) == 0;
            var foreground = rising ? VelocityGridColor.Green : VelocityGridColor.Red;
            var resting = new VelocityGridCellFormat(foreground, VelocityGridColor.None,
                rising ? VelocityGridIcon.UpArrow : VelocityGridIcon.DownArrow);
            var text = value.ToString("F2");
            updates.Add(new(row, priceColumn, text, resting with
            {
                Background = rising ? VelocityGridColor.LightGreen : VelocityGridColor.LightRed
            }));
            _flashes[row] = new(text, resting, now.AddMilliseconds(500));
        }
        TradesGrid.ApplyUpdates(updates);
    }

    private readonly record struct PendingFlash(string Value, VelocityGridCellFormat RestingFormat, DateTimeOffset Due);
}

internal sealed class TradeProvider(long rowCount) : IVelocityGridDataProvider
{
    public long RowCount { get; private set; } = rowCount;
    public bool Descending { get; set; }
    public void Append(long count) => RowCount += count;

    public ValueTask<VelocityGridPage> GetRowsAsync(VelocityGridRange range, VelocityGridFetchContext context, CancellationToken cancellationToken)
    {
        var values = new string[checked(range.RowCount * context.ColumnCount)];
        var formats = new VelocityGridCellFormat[values.Length];
        for (var r = 0; r < range.RowCount; r++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var logical = range.StartRow + r;
            var row = Descending ? RowCount - logical - 1 : logical;
            for (var c = 0; c < context.ColumnCount; c++)
            {
                var index = r * context.ColumnCount + c;
                var key = context.Columns[c].Key;
                values[index] = key switch { "id" => row.ToString("N0"), "symbol" => $"SYM{row % 997:000}", "price" => $"{80 + row % 4000 / 100.0:F2}", "status" => row % 7 == 0 ? "Updated" : "Live", "venue" => row % 2 == 0 ? "LSE" : "XNAS", "notes" => $"Provider row {row:N0}", _ => "" };
                if (key == "price") formats[index] = row % 2 == 0 ? new(VelocityGridColor.Green, VelocityGridColor.None, VelocityGridIcon.UpArrow) : new(VelocityGridColor.Red, VelocityGridColor.None, VelocityGridIcon.DownArrow);
            }
        }
        return ValueTask.FromResult(new VelocityGridPage(range.StartRow, range.RowCount, values, formats));
    }
}
