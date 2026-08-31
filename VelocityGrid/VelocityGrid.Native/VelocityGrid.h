#pragma once

#include "VelocityGrid.g.h"
#include "Viewport/ViewportEngine.h"
#include "Cache/PageCache.h"
#include "Data/RequestScheduler.h"

#include <d2d1_1.h>
#include <d3d11_1.h>
#include <dwrite.h>
#include <dxgi1_2.h>
#include <wrl/client.h>
#include <unordered_set>
#include <unordered_map>

namespace winrt::VelocityGrid_Native::implementation
{
    struct VelocityGrid : VelocityGridT<VelocityGrid>
    {
        VelocityGrid();
        ~VelocityGrid() noexcept;

        std::int64_t RowCount() const noexcept;
        void RowCount(std::int64_t value);
        double RowHeight() const noexcept;
        void RowHeight(double value);
        std::int64_t FirstVisibleRow() const noexcept;
        std::int64_t LastVisibleRow() const noexcept;
        Microsoft::UI::Xaml::UIElement View() const noexcept;
        bool ExternalProviderEnabled() const noexcept;
        void ExternalProviderEnabled(bool value);
        winrt::event_token PageRequested(VelocityGrid_Native::PageRequestedHandler const& handler);
        void PageRequested(winrt::event_token const& token) noexcept;
        winrt::event_token PageCanceled(VelocityGrid_Native::PageCanceledHandler const& handler);
        void PageCanceled(winrt::event_token const& token) noexcept;
        void CompletePage(std::uint64_t requestId, std::uint64_t generation, std::int64_t startRow,
            std::int32_t rowCount, winrt::array_view<winrt::hstring const> values);
        void FailPage(std::uint64_t requestId, std::uint64_t generation, winrt::hstring const& message);

    private:
        void build_visual_tree();
        void create_device_resources();
        void create_size_dependent_resources(double width, double height);
        void render();
        void update_scrollbars();
        void update_viewport();
        void schedule_pages();
        void process_completions();
        void on_size_changed(IInspectable const&, Microsoft::UI::Xaml::SizeChangedEventArgs const& args);
        void on_scroll(IInspectable const&, Microsoft::UI::Xaml::Controls::Primitives::RangeBaseValueChangedEventArgs const& args);
        void on_horizontal_scroll(IInspectable const&, Microsoft::UI::Xaml::Controls::Primitives::RangeBaseValueChangedEventArgs const& args);
        void on_pointer_wheel(IInspectable const&, Microsoft::UI::Xaml::Input::PointerRoutedEventArgs const& args);
        void on_tick(IInspectable const&, IInspectable const&);

        std::int64_t m_row_count{ 10'000'000 };
        double m_row_height{ 24.0 };
        double m_scroll_offset{};
        double m_horizontal_offset{};
        double m_width{};
        double m_height{};
        velocity_grid::viewport_range m_viewport{};
        velocity_grid::page_cache m_cache{ 8 };
        velocity_grid::request_scheduler m_scheduler;
        std::unordered_set<std::int64_t> m_wanted_pages;
        std::uint64_t m_generation{ 1 };
        std::int64_t m_anchor_page{ -1 };
        std::int64_t m_previous_first_row{};
        std::uint64_t m_cache_hits{};
        std::uint64_t m_cache_misses{};
        struct external_request
        {
            std::int64_t start_row;
            std::uint64_t generation;
        };
        bool m_external_provider_enabled{};
        std::uint64_t m_next_external_request_id{ 1 };
        std::unordered_map<std::uint64_t, external_request> m_external_requests;
        std::uint64_t m_external_requested{};
        std::uint64_t m_external_canceled{};
        std::uint64_t m_external_stale{};
        std::uint64_t m_external_failed{};
        winrt::hstring m_last_provider_error;
        winrt::event<VelocityGrid_Native::PageRequestedHandler> m_page_requested;
        winrt::event<VelocityGrid_Native::PageCanceledHandler> m_page_canceled;

        Microsoft::UI::Xaml::Controls::Grid m_root{ nullptr };
        Microsoft::UI::Xaml::Controls::SwapChainPanel m_surface{ nullptr };
        Microsoft::UI::Xaml::Controls::Slider m_scrollbar{ nullptr };
        Microsoft::UI::Xaml::Controls::Slider m_horizontal_scrollbar{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBlock m_diagnostics{ nullptr };
        Microsoft::UI::Xaml::DispatcherTimer m_timer{ nullptr };
        winrt::event_token m_size_changed_token{};
        winrt::event_token m_pointer_wheel_token{};
        winrt::event_token m_vertical_scroll_token{};
        winrt::event_token m_horizontal_scroll_token{};
        winrt::event_token m_timer_token{};

        ::Microsoft::WRL::ComPtr<ID3D11Device> m_d3d_device;
        ::Microsoft::WRL::ComPtr<ID3D11DeviceContext> m_d3d_context;
        ::Microsoft::WRL::ComPtr<IDXGISwapChain1> m_swap_chain;
        ::Microsoft::WRL::ComPtr<ID2D1Factory1> m_d2d_factory;
        ::Microsoft::WRL::ComPtr<ID2D1Device> m_d2d_device;
        ::Microsoft::WRL::ComPtr<ID2D1DeviceContext> m_d2d_context;
        ::Microsoft::WRL::ComPtr<ID2D1Bitmap1> m_target_bitmap;
        ::Microsoft::WRL::ComPtr<IDWriteFactory> m_dwrite_factory;
        ::Microsoft::WRL::ComPtr<IDWriteTextFormat> m_text_format;
        std::uint64_t m_frame_count{};
        std::chrono::steady_clock::time_point m_fps_epoch{ std::chrono::steady_clock::now() };
    };
}

namespace winrt::VelocityGrid_Native::factory_implementation
{
    struct VelocityGrid : VelocityGridT<VelocityGrid, implementation::VelocityGrid>
    {
    };
}
