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

    /// <inheritdoc />
    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        if (DesignerProperties.GetIsInDesignMode(this))
            return new HandleRef(this, hwndParent.Handle);

        _xamlManager = WindowsXamlManager.InitializeForCurrentThread();
        _xamlSource = new DesktopWindowXamlSource();
        var native = (IDesktopWindowXamlSourceNative)_xamlSource;
        native.AttachToWindow(hwndParent.Handle);
        _islandWindow = native.WindowHandle;

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

    [ComImport]
    [Guid("3CBCF1BF-2F76-4E9C-96AB-E84B37972554")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDesktopWindowXamlSourceNative
    {
        void AttachToWindow(nint parentWindow);
        nint WindowHandle { get; }
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
