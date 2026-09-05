using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using VelocityGrid.Managed;

namespace VelocityGrid.Wpf;

/// <summary>Hosts the WinUI VelocityGrid control inside an ordinary WPF visual tree.</summary>
public sealed class VelocityGridHost : HwndHost
{
    [ThreadStatic]
    private static DispatcherQueueController? s_dispatcherQueueController;
    [ThreadStatic]
    private static int s_dispatcherQueueLeaseCount;

    private bool _hasDispatcherQueueLease;
    private WindowsXamlManager? _xamlManager;
    private DesktopWindowXamlSource? _xamlSource;
    private nint _islandWindow;
    private IVelocityGridDataProvider? _dataProvider;
    private double _rowHeight = 24;
    private IEnumerable<VelocityGridColumn>? _columns;

    /// <summary>The hosted grid API. It is available after the WPF host handle is created.</summary>
    public VelocityGridControl? Grid { get; private set; }

    /// <summary>Gets or sets the provider used when the island is created.</summary>
    public IVelocityGridDataProvider? DataProvider
    {
        get => _dataProvider;
        set { _dataProvider = value; if (Grid is not null) Grid.DataProvider = value; }
    }

    /// <summary>Gets or sets the row height used when the island is created.</summary>
    public double RowHeight
    {
        get => _rowHeight;
        set { _rowHeight = value; if (Grid is not null) Grid.RowHeight = value; }
    }

    /// <summary>Optional initial column configuration.</summary>
    public IEnumerable<VelocityGridColumn>? Columns
    {
        get => _columns;
        set { _columns = value; if (Grid is not null && value is not null) Grid.SetColumns(value); }
    }

    /// <summary>Raised after the hosted WinUI grid has been constructed.</summary>
    public event EventHandler? GridReady;

    /// <summary>Updates the hosted grid's logical extent and cache state.</summary>
    public void NotifyDataChanged(long newRowCount, VelocityGridDataChangeKind changeKind)
    {
        if (Grid is null)
            throw new InvalidOperationException("The grid is available after the host raises GridReady.");
        Grid.NotifyDataChanged(newRowCount, changeKind);
    }

    /// <summary>Updates the logical extent and optionally returns the viewport to the first row.</summary>
    public void NotifyDataChanged(long newRowCount, VelocityGridDataChangeKind changeKind,
        bool resetScrollPosition)
    {
        if (Grid is null) throw new InvalidOperationException("The grid is available after the host raises GridReady.");
        Grid.NotifyDataChanged(newRowCount, changeKind, resetScrollPosition);
    }

    /// <summary>Clears cached data and reloads the current provider snapshot.</summary>
    public void Refresh(bool resetScrollPosition = false)
    {
        if (Grid is null) throw new InvalidOperationException("The grid is available after the host raises GridReady.");
        Grid.Refresh(resetScrollPosition);
    }

    /// <summary>Evicts pages intersecting a changed logical row range.</summary>
    public void InvalidateRows(long startRow, long rowCount)
    {
        if (Grid is null) throw new InvalidOperationException("The grid is available after the host raises GridReady.");
        Grid.InvalidateRows(startRow, rowCount);
    }

    /// <summary>Applies caller-owned values and visual formatting to cached cells.</summary>
    public void ApplyUpdates(IEnumerable<VelocityGridCellUpdate> updates)
    {
        if (Grid is null) throw new InvalidOperationException("The grid is available after the host raises GridReady.");
        Grid.ApplyUpdates(updates);
    }

    /// <inheritdoc />
    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        if (DesignerProperties.GetIsInDesignMode(this))
            return new HandleRef(this, hwndParent.Handle);

        // WPF's dispatcher does not provide the WinUI DispatcherQueue required
        // by XAML Islands. Own one only when the application has not supplied it.
        if (s_dispatcherQueueLeaseCount++ == 0 && DispatcherQueue.GetForCurrentThread() is null)
            s_dispatcherQueueController = DispatcherQueueController.CreateOnCurrentThread();
        _hasDispatcherQueueLease = true;

        _xamlManager = WindowsXamlManager.InitializeForCurrentThread();
        _xamlSource = new DesktopWindowXamlSource();
        // WinUI 3 XAML Islands attach through WindowId. The legacy
        // IDesktopWindowXamlSourceNative interface belongs to UWP XAML and is
        // deliberately not implemented by the Windows App SDK projection.
        _xamlSource.Initialize(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwndParent.Handle));
        _islandWindow = Microsoft.UI.Win32Interop.GetWindowFromWindowId(_xamlSource.SiteBridge.WindowId);

        Grid = new VelocityGridControl { RowHeight = _rowHeight };
        if (_columns is not null) Grid.SetColumns(_columns);
        Grid.DataProvider = _dataProvider;
        _xamlSource.Content = Grid;
        GridReady?.Invoke(this, EventArgs.Empty);
        return new HandleRef(this, _islandWindow);
    }

    /// <inheritdoc />
    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        if (_xamlSource is not null) _xamlSource.Content = null;
        Grid = null;
        _xamlSource?.Dispose();
        _xamlSource = null;
        _xamlManager?.Dispose();
        _xamlManager = null;
        if (_hasDispatcherQueueLease)
        {
            _hasDispatcherQueueLease = false;
            if (--s_dispatcherQueueLeaseCount == 0)
            {
                s_dispatcherQueueController?.ShutdownQueue();
                s_dispatcherQueueController = null;
            }
        }
        _islandWindow = 0;
    }

    /// <inheritdoc />
    protected override void OnWindowPositionChanged(Rect rcBoundingBox)
    {
        base.OnWindowPositionChanged(rcBoundingBox);
        if (_islandWindow != 0)
            SetWindowPos(_islandWindow, 0, 0, 0,
                Math.Max(1, (int)rcBoundingBox.Width), Math.Max(1, (int)rcBoundingBox.Height),
                SetWindowPosFlags.NoActivate | SetWindowPosFlags.NoZOrder | SetWindowPosFlags.ShowWindow);
    }

    [Flags]
    private enum SetWindowPosFlags : uint
    {
        NoZOrder = 0x0004,
        NoActivate = 0x0010,
        ShowWindow = 0x0040
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        nint window, nint insertAfter, int x, int y, int width, int height, SetWindowPosFlags flags);
}
