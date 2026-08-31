#pragma once

#include "MainWindow.g.h"

namespace winrt::VelocityGrid_Native_Tests::implementation
{
    struct MainWindow : MainWindowT<MainWindow>
    {
        MainWindow();
    };
}

namespace winrt::VelocityGrid_Native_Tests::factory_implementation
{
    struct MainWindow : MainWindowT<MainWindow, implementation::MainWindow>
    {
    };
}
