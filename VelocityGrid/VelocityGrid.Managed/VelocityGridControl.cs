using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using VelocityGrid_Native;

namespace VelocityGrid.Managed;

/// <summary>
/// Provides the idiomatic C# control surface while the viewport and renderer remain native.
/// </summary>
public sealed class VelocityGridControl : UserControl
{
    /// <summary>Number of columns in the initial configuration.</summary>
    public const int DefaultColumnCount = 10;

    private VelocityGrid_Native.VelocityGrid? _nativeGrid;
    private readonly Dictionary<ulong, CancellationTokenSource> _requests = [];
    private IVelocityGridDataProvider? _dataProvider;
    private IReadOnlyList<VelocityGridColumn> _columns = [];
    private bool _isLoaded;
    private long _rowCount;
    private double _rowHeight = 24;

    public VelocityGridControl()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        AutomationProperties.SetName(this, "VelocityGrid");
        AutomationProperties.SetHelpText(this, "Read-only virtual data grid. Use arrow, Home, End, Page Up, and Page Down keys to navigate.");
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _columns = Array.AsReadOnly(Enumerable.Range(1, DefaultColumnCount)
            .Select(index => new VelocityGridColumn($"Column {index}")).ToArray());
    }

    private VelocityGrid_Native.VelocityGrid NativeGrid => _nativeGrid ?? CreateNativeGrid();

    private VelocityGrid_Native.VelocityGrid CreateNativeGrid()
    {
        var native = CreateNativeGridAtStage("activate the native grid", NativeActivationRegistration.CreateGrid);
        _nativeGrid = native;
        CreateNativeGridAtStage("set RowHeight", () => native.RowHeight = _rowHeight);
        CreateNativeGridAtStage("set RowCount", () => native.RowCount = _dataProvider?.RowCount ?? _rowCount);
        CreateNativeGridAtStage("configure columns", () => ApplyColumns(native, _columns));
        Content = native;
        return native;
    }

    private static T CreateNativeGridAtStage<T>(string stage, Func<T> operation)
    {
        try { return operation(); }
        catch (Exception error)
        {
            throw new InvalidOperationException($"VelocityGrid could not {stage}.", error);
        }
    }

    private static void CreateNativeGridAtStage(string stage, Action operation) =>
        CreateNativeGridAtStage(stage, () => { operation(); return true; });

    private static void ApplyColumns(VelocityGrid_Native.VelocityGrid native,
        IReadOnlyList<VelocityGridColumn> columns) => native.SetColumns(
            columns.Select(column => column.Header).ToArray(),
            columns.Select(column => column.Width).ToArray(),
            columns.Select(column => (int)column.Alignment).ToArray());

    /// <summary>Raised after native pointer or keyboard selection changes.</summary>
    public event EventHandler<VelocityGridSelectionChangedEventArgs>? SelectionChanged;
    /// <summary>Raised when a provider request fails for a reason other than cancellation.</summary>
    public event EventHandler<VelocityGridDataErrorEventArgs>? DataError;
    /// <summary>Current immutable visible-column configuration.</summary>
    public IReadOnlyList<VelocityGridColumn> Columns => _columns;
    /// <summary>Selected zero-based logical row, or -1 when no cell is selected.</summary>
    public long SelectedRow => NativeGrid.SelectedRow;
    /// <summary>Selected zero-based source column, or -1 when no cell is selected.</summary>
    public int SelectedColumn => NativeGrid.SelectedColumn;

    /// <summary>Returns a point-in-time copy of native diagnostic counters.</summary>
    public VelocityGridPerformanceMetrics PerformanceMetrics => new(
        NativeGrid.FrameCount, NativeGrid.CacheHits, NativeGrid.CacheMisses, NativeGrid.RequestCount,
        NativeGrid.UpdateBatchCount, NativeGrid.UpdateCellCount, NativeGrid.UpdateRenderCount,
        NativeGrid.LastUpdateLatencyMicroseconds);

    /// <summary>Resets counters without clearing cached data or selection.</summary>
    public void ResetPerformanceMetrics() => NativeGrid.ResetMetrics();

    /// <summary>Jumps directly to a logical row; native code clamps the value.</summary>
    public void ScrollToRow(long rowIndex) => NativeGrid.ScrollToRow(rowIndex);

    /// <summary>Updates the logical extent using explicit cache-invalidation semantics.</summary>
    public void NotifyDataChanged(long newRowCount, VelocityGridDataChangeKind changeKind)
        => NotifyDataChanged(newRowCount, changeKind, resetScrollPosition: false);

    /// <summary>Updates the logical extent and optionally returns the viewport to the first row.</summary>
    public void NotifyDataChanged(long newRowCount, VelocityGridDataChangeKind changeKind,
        bool resetScrollPosition)
    {
        if (newRowCount < 0) throw new ArgumentOutOfRangeException(nameof(newRowCount));
        if (!Enum.IsDefined(changeKind)) throw new ArgumentOutOfRangeException(nameof(changeKind));
        if (changeKind == VelocityGridDataChangeKind.Append && newRowCount < RowCount)
            throw new ArgumentException("Append cannot reduce the row count.", nameof(newRowCount));
        if (changeKind == VelocityGridDataChangeKind.TrimEnd && newRowCount > RowCount)
            throw new ArgumentException("TrimEnd cannot increase the row count.", nameof(newRowCount));

        _rowCount = newRowCount;
        NativeGrid.NotifyDataChangedWithOptions(newRowCount, (DataChangeKind)changeKind, resetScrollPosition);
    }

    /// <summary>Clears cached data and reloads the current provider snapshot.</summary>
    public void Refresh(bool resetScrollPosition = false) =>
        NotifyDataChanged(RowCount, VelocityGridDataChangeKind.Reset, resetScrollPosition);

    /// <summary>Evicts provider pages intersecting a changed logical row range.</summary>
    public void InvalidateRows(long startRow, long rowCount)
    {
        if (startRow < 0) throw new ArgumentOutOfRangeException(nameof(startRow));
        if (rowCount <= 0) throw new ArgumentOutOfRangeException(nameof(rowCount));
        if (startRow >= RowCount || rowCount > RowCount - startRow)
            throw new ArgumentOutOfRangeException(nameof(rowCount), "The invalidation range must be within the current dataset.");
        NativeGrid.InvalidateRows(startRow, rowCount);
    }

    /// <summary>Applies a caller-owned batch to resident native cache entries.</summary>
    public void ApplyUpdates(IEnumerable<VelocityGridCellUpdate> updates)
    {
        ArgumentNullException.ThrowIfNull(updates);
        var batch = updates.ToArray();
        if (batch.Length == 0) return;
        if (batch.Any(update => update.ColumnIndex >= _columns.Count))
            throw new ArgumentOutOfRangeException(nameof(updates), "An update column index exceeds the configured columns.");
        NativeGrid.ApplyUpdates(
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
        if (snapshot.Any(column => column is null))
            throw new ArgumentException("VelocityGrid columns cannot contain null entries.", nameof(columns));
        if (snapshot.Select(column => column.Key).Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
            throw new ArgumentException("VelocityGrid column keys must be unique.", nameof(columns));
        var immutableSnapshot = Array.AsReadOnly(snapshot);
        var previous = _columns;
        _columns = immutableSnapshot;
        try
        {
            if (_nativeGrid is not null) ApplyColumns(_nativeGrid, snapshot);
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
            if (value is not null && _nativeGrid is not null)
                NotifyDataChanged(value.RowCount, VelocityGridDataChangeKind.Reset);
            // PageRequested handlers are attached in OnLoaded. Activating a provider
            // earlier would emit an initial request that no managed listener can receive.
            if (_nativeGrid is not null)
                _nativeGrid.ExternalProviderEnabled = _isLoaded && value is not null;
        }
    }

    /// <summary>Gets or sets the 64-bit logical dataset size.</summary>
    public long RowCount
    {
        get => _nativeGrid?.RowCount ?? _dataProvider?.RowCount ?? _rowCount;
        set { _rowCount = value; if (_nativeGrid is not null) _nativeGrid.RowCount = value; }
    }

    /// <summary>Gets or sets the fixed row height in device-independent pixels.</summary>
    public double RowHeight
    {
        get => _nativeGrid?.RowHeight ?? _rowHeight;
        set { _rowHeight = value; if (_nativeGrid is not null) _nativeGrid.RowHeight = value; }
    }

    /// <summary>First logical row intersecting the viewport.</summary>
    public long FirstVisibleRow => _nativeGrid?.FirstVisibleRow ?? 0;

    /// <summary>Last logical row intersecting the viewport.</summary>
    public long LastVisibleRow => _nativeGrid?.LastVisibleRow ?? -1;

    protected override AutomationPeer OnCreateAutomationPeer() => new VelocityGridAutomationPeer(this);

    private async void OnPageRequested(long startRow, int rowCount, ulong requestId, ulong generation)
    {
        // This callback begins on the UI thread. The provider owns asynchronous I/O;
        // completion is marshalled back here before crossing the ABI once per page.
        var provider = _dataProvider;
        if (provider is null) return;
        var columns = _columns;
        var cancellation = new CancellationTokenSource();
        _requests[requestId] = cancellation;
        try
        {
            var page = await provider.GetRowsAsync(
                new VelocityGridRange(startRow, rowCount),
                new VelocityGridFetchContext(requestId, generation, columns),
                cancellation.Token);
            if (page.ColumnCount != columns.Count)
                throw new InvalidOperationException(
                    $"The provider returned {page.ColumnCount} columns per row; {columns.Count} were requested.");
            if (!cancellation.IsCancellationRequested)
                NativeGrid.CompletePage(requestId, generation, page.StartRow, page.RowCount, page.Values,
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
                NativeGrid.FailPage(requestId, generation, error.Message);
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
        // Leave the XAML loader (and Visual Studio's XAML diagnostics hooks)
        // before activating native WinUI classes. Activating synchronously from
        // Loaded can be re-entrant under the debugger and fail with E_NOINTERFACE.
        DispatcherQueue.TryEnqueue(InitializeNativeAfterLoad);
    }

    private void InitializeNativeAfterLoad()
    {
        if (!_isLoaded) return;
        try
        {
            var native = NativeGrid;
            UpdateVisualTheme();
            native.PageRequested -= OnPageRequested;
            native.PageCanceled -= OnPageCanceled;
            native.PageRequested += OnPageRequested;
            native.PageCanceled += OnPageCanceled;
            native.SelectionChanged -= OnNativeSelectionChanged;
            native.SelectionChanged += OnNativeSelectionChanged;
            native.ExternalProviderEnabled = _dataProvider is not null;
        }
        catch (Exception error)
        {
            // Dispatcher callbacks otherwise turn WinRT activation failures into
            // native stowed exceptions, terminating the host without a useful
            // managed stack trace.
            var details = FormatExceptionDetails(error);
            Content = new TextBlock
            {
                Text = details,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(12)
            };
            AutomationProperties.SetItemStatus(this, details);
        }
    }

    private static string FormatExceptionDetails(Exception error)
    {
        var details = new StringBuilder();
        for (Exception? current = error; current is not null; current = current.InnerException)
        {
            if (details.Length != 0) details.AppendLine().AppendLine("Inner exception:");
            details.Append(current.GetType().FullName)
                .Append(" (HRESULT 0x")
                .Append(current.HResult.ToString("X8"))
                .AppendLine(")")
                .AppendLine(current.Message);
            if (!string.IsNullOrWhiteSpace(current.StackTrace))
                details.AppendLine(current.StackTrace);
        }
        return details.ToString().TrimEnd();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        _isLoaded = false;
        ActualThemeChanged -= OnActualThemeChanged;
        if (_nativeGrid is null) return;
        _nativeGrid.ExternalProviderEnabled = false;
        CancelAllRequests();
        _nativeGrid.PageRequested -= OnPageRequested;
        _nativeGrid.PageCanceled -= OnPageCanceled;
        _nativeGrid.SelectionChanged -= OnNativeSelectionChanged;
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args) => UpdateVisualTheme();

    private void UpdateVisualTheme()
    {
        if (_nativeGrid is not null) _nativeGrid.VisualTheme = ActualTheme == ElementTheme.Dark ? 1 : 0;
    }

    private void OnNativeSelectionChanged(long rowIndex, int columnIndex)
    {
        var columnName = columnIndex >= 0 && columnIndex < _columns.Count
            ? _columns[columnIndex].Header : $"Column {columnIndex + 1}";
        AutomationProperties.SetItemStatus(this, $"Row {rowIndex + 1}, {columnName}");
        FrameworkElementAutomationPeer.FromElement(this)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        SelectionChanged?.Invoke(this, new VelocityGridSelectionChangedEventArgs(rowIndex, columnIndex));
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
