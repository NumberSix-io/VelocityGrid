using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Threading;
using VelocityGrid_Native;

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

    public VelocityGridControl()
    {
        Content = _nativeGrid.View;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
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
            _nativeGrid.ExternalProviderEnabled = value is not null;
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
                _nativeGrid.CompletePage(requestId, generation, page.StartRow, page.RowCount, page.Values);
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
        _nativeGrid.PageRequested -= OnPageRequested;
        _nativeGrid.PageCanceled -= OnPageCanceled;
        _nativeGrid.PageRequested += OnPageRequested;
        _nativeGrid.PageCanceled += OnPageCanceled;
        _nativeGrid.ExternalProviderEnabled = _dataProvider is not null;
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        _nativeGrid.ExternalProviderEnabled = false;
        CancelAllRequests();
        _nativeGrid.PageRequested -= OnPageRequested;
        _nativeGrid.PageCanceled -= OnPageCanceled;
    }
}
