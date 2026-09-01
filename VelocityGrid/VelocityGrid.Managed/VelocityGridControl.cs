using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using VelocityGrid_Native;
using Windows.System;

namespace VelocityGrid.Managed;

/// <summary>
/// Provides the idiomatic C# control surface while the viewport and renderer remain native.
/// </summary>
public sealed class VelocityGridControl : UserControl
{
    public const int ColumnCount = 10;

    private readonly VelocityGrid_Native.VelocityGrid _nativeGrid = new();
    private readonly Dictionary<ulong, CancellationTokenSource> _requests = [];
    private IVelocityGridDataProvider? _dataProvider;
    private IReadOnlyList<VelocityGridColumn> _columns = [];
    private bool _isLoaded;

    public VelocityGridControl()
    {
        Content = _nativeGrid.View;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        IsTabStop = true;
        AddHandler(PointerPressedEvent, new PointerEventHandler(OnAnyPointerPressed), true);
        KeyDown += OnGridKeyDown;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SetColumns(Enumerable.Range(1, ColumnCount).Select(index => new VelocityGridColumn($"Column {index}")));
    }

    public event EventHandler<VelocityGridSelectionChangedEventArgs>? SelectionChanged;
    public IReadOnlyList<VelocityGridColumn> Columns => _columns;
    public long SelectedRow => _nativeGrid.SelectedRow;
    public int SelectedColumn => _nativeGrid.SelectedColumn;

    public VelocityGridPerformanceMetrics PerformanceMetrics => new(
        _nativeGrid.FrameCount, _nativeGrid.CacheHits, _nativeGrid.CacheMisses, _nativeGrid.RequestCount,
        _nativeGrid.UpdateBatchCount, _nativeGrid.UpdateCellCount, _nativeGrid.UpdateRenderCount,
        _nativeGrid.LastUpdateLatencyMicroseconds);

    public void ResetPerformanceMetrics() => _nativeGrid.ResetMetrics();

    public void ScrollToRow(long rowIndex) => _nativeGrid.ScrollToRow(rowIndex);

    public void ApplyUpdates(IEnumerable<VelocityGridCellUpdate> updates)
    {
        ArgumentNullException.ThrowIfNull(updates);
        var batch = updates.ToArray();
        if (batch.Length == 0) return;
        _nativeGrid.ApplyUpdates(
            batch.Select(update => update.RowIndex).ToArray(),
            batch.Select(update => update.ColumnIndex).ToArray(),
            batch.Select(update => update.Value).ToArray(),
            batch.Select(update => (byte)update.Format.Foreground).ToArray(),
            batch.Select(update => (byte)update.Format.Background).ToArray(),
            batch.Select(update => (byte)update.Format.Icon).ToArray());
    }

    public void SetColumns(IEnumerable<VelocityGridColumn> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        var snapshot = columns.ToArray();
        if (snapshot.Length is < 1 or > ColumnCount)
            throw new ArgumentException("VelocityGrid requires between one and ten columns.", nameof(columns));
        _nativeGrid.SetColumns(
            snapshot.Select(column => column.Header).ToArray(),
            snapshot.Select(column => column.Width).ToArray(),
            snapshot.Select(column => (int)column.Alignment).ToArray());
        _columns = snapshot;
    }

    public IVelocityGridDataProvider? DataProvider
    {
        get => _dataProvider;
        set
        {
            if (ReferenceEquals(_dataProvider, value)) return;
            CancelAllRequests();
            _dataProvider = value;
            if (value is not null) RowCount = value.RowCount;
            // PageRequested handlers are attached in OnLoaded. Activating a provider
            // earlier would emit an initial request that no managed listener can receive.
            _nativeGrid.ExternalProviderEnabled = _isLoaded && value is not null;
        }
    }

    public long RowCount
    {
        get => _nativeGrid.RowCount;
        set => _nativeGrid.RowCount = value;
    }

    public double RowHeight
    {
        get => _nativeGrid.RowHeight;
        set => _nativeGrid.RowHeight = value;
    }

    public long FirstVisibleRow => _nativeGrid.FirstVisibleRow;

    public long LastVisibleRow => _nativeGrid.LastVisibleRow;

    private async void OnPageRequested(long startRow, int rowCount, ulong requestId, ulong generation)
    {
        var provider = _dataProvider;
        if (provider is null) return;
        var cancellation = new CancellationTokenSource();
        _requests[requestId] = cancellation;
        try
        {
            var page = await provider.GetRowsAsync(
                new VelocityGridRange(startRow, rowCount),
                new VelocityGridFetchContext(requestId, generation),
                cancellation.Token);
            if (!cancellation.IsCancellationRequested)
                _nativeGrid.CompletePage(requestId, generation, page.StartRow, page.RowCount, page.Values,
                    page.Formats.Select(format => (byte)format.Foreground).ToArray(),
                    page.Formats.Select(format => (byte)format.Background).ToArray(),
                    page.Formats.Select(format => (byte)format.Icon).ToArray());
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception error)
        {
            if (!cancellation.IsCancellationRequested)
                _nativeGrid.FailPage(requestId, generation, error.Message);
        }
        finally
        {
            _requests.Remove(requestId);
            cancellation.Dispose();
        }
    }

    private void OnPageCanceled(ulong requestId)
    {
        if (_requests.Remove(requestId, out var cancellation)) cancellation.Cancel();
    }

    private void CancelAllRequests()
    {
        foreach (var cancellation in _requests.Values) cancellation.Cancel();
        _requests.Clear();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        _isLoaded = true;
        _nativeGrid.PageRequested -= OnPageRequested;
        _nativeGrid.PageCanceled -= OnPageCanceled;
        _nativeGrid.PageRequested += OnPageRequested;
        _nativeGrid.PageCanceled += OnPageCanceled;
        _nativeGrid.SelectionChanged -= OnNativeSelectionChanged;
        _nativeGrid.SelectionChanged += OnNativeSelectionChanged;
        _nativeGrid.ExternalProviderEnabled = _dataProvider is not null;
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        _isLoaded = false;
        _nativeGrid.ExternalProviderEnabled = false;
        CancelAllRequests();
        _nativeGrid.PageRequested -= OnPageRequested;
        _nativeGrid.PageCanceled -= OnPageCanceled;
        _nativeGrid.SelectionChanged -= OnNativeSelectionChanged;
    }

    private void OnNativeSelectionChanged(long rowIndex, int columnIndex)
    {
        SelectionChanged?.Invoke(this, new VelocityGridSelectionChangedEventArgs(rowIndex, columnIndex));
    }

    private void OnAnyPointerPressed(object sender, PointerRoutedEventArgs args)
    {
        Focus(FocusState.Pointer);
    }

    private void OnGridKeyDown(object sender, KeyRoutedEventArgs args)
    {
        var command = args.Key switch
        {
            VirtualKey.Up => 0,
            VirtualKey.Down => 1,
            VirtualKey.Left => 2,
            VirtualKey.Right => 3,
            VirtualKey.Home => 4,
            VirtualKey.End => 5,
            VirtualKey.PageUp => 6,
            VirtualKey.PageDown => 7,
            _ => -1
        };
        if (command < 0) return;
        _nativeGrid.NavigateSelection(command);
        args.Handled = true;
    }

}
