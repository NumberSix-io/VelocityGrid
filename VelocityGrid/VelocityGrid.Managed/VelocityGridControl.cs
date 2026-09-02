using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
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
    /// <summary>Number of columns in the initial configuration.</summary>
    public const int DefaultColumnCount = 10;

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
        AutomationProperties.SetName(this, "VelocityGrid");
        AutomationProperties.SetHelpText(this, "Read-only virtual data grid. Use arrow, Home, End, Page Up, and Page Down keys to navigate.");
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        AddHandler(PointerPressedEvent, new PointerEventHandler(OnAnyPointerPressed), true);
        KeyDown += OnGridKeyDown;
        GotFocus += OnFocusChanged;
        LostFocus += OnFocusChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SetColumns(Enumerable.Range(1, DefaultColumnCount).Select(index => new VelocityGridColumn($"Column {index}")));
    }

    /// <summary>Raised after native pointer or keyboard selection changes.</summary>
    public event EventHandler<VelocityGridSelectionChangedEventArgs>? SelectionChanged;
    /// <summary>Raised when a provider request fails for a reason other than cancellation.</summary>
    public event EventHandler<VelocityGridDataErrorEventArgs>? DataError;
    /// <summary>Current immutable visible-column configuration.</summary>
    public IReadOnlyList<VelocityGridColumn> Columns => _columns;
    /// <summary>Selected zero-based logical row, or -1 when no cell is selected.</summary>
    public long SelectedRow => _nativeGrid.SelectedRow;
    /// <summary>Selected zero-based source column, or -1 when no cell is selected.</summary>
    public int SelectedColumn => _nativeGrid.SelectedColumn;

    /// <summary>Returns a point-in-time copy of native diagnostic counters.</summary>
    public VelocityGridPerformanceMetrics PerformanceMetrics => new(
        _nativeGrid.FrameCount, _nativeGrid.CacheHits, _nativeGrid.CacheMisses, _nativeGrid.RequestCount,
        _nativeGrid.UpdateBatchCount, _nativeGrid.UpdateCellCount, _nativeGrid.UpdateRenderCount,
        _nativeGrid.LastUpdateLatencyMicroseconds);

    /// <summary>Resets counters without clearing cached data or selection.</summary>
    public void ResetPerformanceMetrics() => _nativeGrid.ResetMetrics();

    /// <summary>Jumps directly to a logical row; native code clamps the value.</summary>
    public void ScrollToRow(long rowIndex) => _nativeGrid.ScrollToRow(rowIndex);

    /// <summary>Updates the logical extent using explicit cache-invalidation semantics.</summary>
    public void NotifyDataChanged(long newRowCount, VelocityGridDataChangeKind changeKind)
    {
        if (newRowCount < 0) throw new ArgumentOutOfRangeException(nameof(newRowCount));
        if (!Enum.IsDefined(changeKind)) throw new ArgumentOutOfRangeException(nameof(changeKind));
        if (changeKind == VelocityGridDataChangeKind.Append && newRowCount < RowCount)
            throw new ArgumentException("Append cannot reduce the row count.", nameof(newRowCount));
        if (changeKind == VelocityGridDataChangeKind.TrimEnd && newRowCount > RowCount)
            throw new ArgumentException("TrimEnd cannot increase the row count.", nameof(newRowCount));

        _nativeGrid.NotifyDataChanged(newRowCount, (DataChangeKind)changeKind);
    }

    /// <summary>Applies a caller-owned batch to resident native cache entries.</summary>
    public void ApplyUpdates(IEnumerable<VelocityGridCellUpdate> updates)
    {
        ArgumentNullException.ThrowIfNull(updates);
        var batch = updates.ToArray();
        if (batch.Length == 0) return;
        if (batch.Any(update => update.ColumnIndex >= _columns.Count))
            throw new ArgumentOutOfRangeException(nameof(updates), "An update column index exceeds the configured columns.");
        _nativeGrid.ApplyUpdates(
            batch.Select(update => update.RowIndex).ToArray(),
            batch.Select(update => update.ColumnIndex).ToArray(),
            batch.Select(update => update.Value).ToArray(),
            batch.Select(update => (byte)update.Format.Foreground).ToArray(),
            batch.Select(update => (byte)update.Format.Background).ToArray(),
            batch.Select(update => (byte)update.Format.Icon).ToArray());
    }

    /// <summary>Replaces the visible column snapshot and native layout metadata.</summary>
    public void SetColumns(IEnumerable<VelocityGridColumn> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        var snapshot = columns.ToArray();
        if (snapshot.Length < 1)
            throw new ArgumentException("VelocityGrid requires at least one column.", nameof(columns));
        var previous = _columns;
        _columns = snapshot;
        try
        {
            _nativeGrid.SetColumns(
                snapshot.Select(column => column.Header).ToArray(),
                snapshot.Select(column => column.Width).ToArray(),
                snapshot.Select(column => (int)column.Alignment).ToArray());
        }
        catch
        {
            _columns = previous;
            throw;
        }
    }

    /// <summary>Gets or sets the viewport-driven page provider.</summary>
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

    /// <summary>Gets or sets the 64-bit logical dataset size.</summary>
    public long RowCount
    {
        get => _nativeGrid.RowCount;
        set => _nativeGrid.RowCount = value;
    }

    /// <summary>Gets or sets the fixed row height in device-independent pixels.</summary>
    public double RowHeight
    {
        get => _nativeGrid.RowHeight;
        set => _nativeGrid.RowHeight = value;
    }

    /// <summary>First logical row intersecting the viewport.</summary>
    public long FirstVisibleRow => _nativeGrid.FirstVisibleRow;

    /// <summary>Last logical row intersecting the viewport.</summary>
    public long LastVisibleRow => _nativeGrid.LastVisibleRow;

    protected override AutomationPeer OnCreateAutomationPeer() => new VelocityGridAutomationPeer(this);

    private async void OnPageRequested(long startRow, int rowCount, ulong requestId, ulong generation)
    {
        // This callback begins on the UI thread. The provider owns asynchronous I/O;
        // completion is marshalled back here before crossing the ABI once per page.
        var provider = _dataProvider;
        if (provider is null) return;
        var cancellation = new CancellationTokenSource();
        _requests[requestId] = cancellation;
        try
        {
            var page = await provider.GetRowsAsync(
                new VelocityGridRange(startRow, rowCount),
                new VelocityGridFetchContext(requestId, generation, _columns.Count),
                cancellation.Token);
            if (page.ColumnCount != _columns.Count)
                throw new InvalidOperationException(
                    $"The provider returned {page.ColumnCount} columns per row; {_columns.Count} were requested.");
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
            {
                _nativeGrid.FailPage(requestId, generation, error.Message);
                AutomationProperties.SetItemStatus(this, $"Data loading error: {error.Message}");
                FrameworkElementAutomationPeer.FromElement(this)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
                DataError?.Invoke(this, new VelocityGridDataErrorEventArgs(
                    new VelocityGridRange(startRow, rowCount), error));
            }
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
        // Cancellation is cooperative; clearing the registry after signaling makes
        // later native cancellation notifications harmless no-ops.
        foreach (var cancellation in _requests.Values) cancellation.Cancel();
        _requests.Clear();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        _isLoaded = true;
        ActualThemeChanged -= OnActualThemeChanged;
        ActualThemeChanged += OnActualThemeChanged;
        UpdateVisualTheme();
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
        ActualThemeChanged -= OnActualThemeChanged;
        _nativeGrid.ExternalProviderEnabled = false;
        CancelAllRequests();
        _nativeGrid.PageRequested -= OnPageRequested;
        _nativeGrid.PageCanceled -= OnPageCanceled;
        _nativeGrid.SelectionChanged -= OnNativeSelectionChanged;
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args) => UpdateVisualTheme();

    private void UpdateVisualTheme()
    {
        _nativeGrid.VisualTheme = ActualTheme == ElementTheme.Dark ? 1 : 0;
    }

    private void OnNativeSelectionChanged(long rowIndex, int columnIndex)
    {
        var columnName = columnIndex >= 0 && columnIndex < _columns.Count
            ? _columns[columnIndex].Header : $"Column {columnIndex + 1}";
        AutomationProperties.SetItemStatus(this, $"Row {rowIndex + 1}, {columnName}");
        FrameworkElementAutomationPeer.FromElement(this)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        SelectionChanged?.Invoke(this, new VelocityGridSelectionChangedEventArgs(rowIndex, columnIndex));
    }

    private void OnAnyPointerPressed(object sender, PointerRoutedEventArgs args)
    {
        Focus(FocusState.Pointer);
    }

    private void OnFocusChanged(object sender, RoutedEventArgs args) => _nativeGrid.HasKeyboardFocus = FocusState != FocusState.Unfocused;

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

internal sealed class VelocityGridAutomationPeer(VelocityGridControl owner) : FrameworkElementAutomationPeer(owner)
{
    protected override string GetClassNameCore() => nameof(VelocityGridControl);
    protected override string GetNameCore() => string.IsNullOrWhiteSpace(base.GetNameCore()) ? "VelocityGrid" : base.GetNameCore();
    protected override string GetHelpTextCore() =>
        "Read-only virtual data grid with keyboard selection and viewport-driven data loading.";
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.DataGrid;
}
