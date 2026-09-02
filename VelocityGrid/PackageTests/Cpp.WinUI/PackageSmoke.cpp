#include <winrt/VelocityGrid_Native.h>

void velocity_grid_package_compile_smoke()
{
    winrt::VelocityGrid_Native::VelocityGrid grid{ nullptr };
    if (grid)
    {
        grid.NotifyDataChanged(1'100, winrt::VelocityGrid_Native::DataChangeKind::Append);
        grid.InvalidateRows(0, 1);
        grid.NotifyDataChangedWithOptions(1'100, winrt::VelocityGrid_Native::DataChangeKind::Reset, true);
    }
    (void)grid;
}
