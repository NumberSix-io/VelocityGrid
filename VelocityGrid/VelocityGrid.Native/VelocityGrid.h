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
#include <string>
#include <vector>

namespace winrt::VelocityGrid_Native::implementation
{
    struct InteractionTrackerOwner;

    // Native host for the small WinUI chrome and immediate-mode grid surface. All
    // methods are UI-thread-affine except work encapsulated by request_scheduler.
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
        std::int32_t VisualTheme() const noexcept;
        void VisualTheme(std::int32_t value);
        bool HasKeyboardFocus() const noexcept;
        void HasKeyboardFocus(bool value);
        winrt::event_token PageRequested(VelocityGrid_Native::PageRequestedHandler const& handler);
        void PageRequested(winrt::event_token const& token) noexcept;
        winrt::event_token PageCanceled(VelocityGrid_Native::PageCanceledHandler const& handler);
        void PageCanceled(winrt::event_token const& token) noexcept;
        void CompletePage(std::uint64_t requestId, std::uint64_t generation, std::int64_t startRow,
            std::int32_t rowCount, winrt::array_view<winrt::hstring const> values,
            winrt::array_view<std::uint8_t const> foregrounds, winrt::array_view<std::uint8_t const> backgrounds,
            winrt::array_view<std::uint8_t const> icons);
        void FailPage(std::uint64_t requestId, std::uint64_t generation, winrt::hstring const& message);
        void SetColumns(winrt::array_view<winrt::hstring const> headers,
            winrt::array_view<double const> widths, winrt::array_view<std::int32_t const> alignments);
        std::int64_t SelectedRow() const noexcept;
        std::int32_t SelectedColumn() const noexcept;
        winrt::event_token SelectionChanged(VelocityGrid_Native::SelectionChangedHandler const& handler);
        void SelectionChanged(winrt::event_token const& token) noexcept;
        void NavigateSelection(std::int32_t command);
        void ScrollToRow(std::int64_t rowIndex);
        void NotifyDataChanged(std::int64_t newRowCount, VelocityGrid_Native::DataChangeKind changeKind);
        void NotifyDataChangedWithOptions(std::int64_t newRowCount,
            VelocityGrid_Native::DataChangeKind changeKind, bool resetScrollPosition);
        void InvalidateRows(std::int64_t startRow, std::int64_t rowCount);
        std::uint64_t FrameCount() const noexcept;
        std::uint64_t CacheHits() const noexcept;
        std::uint64_t CacheMisses() const noexcept;
        std::uint64_t RequestCount() const noexcept;
        void ResetMetrics() noexcept;
        void ApplyUpdates(winrt::array_view<std::int64_t const> rowIndices,
            winrt::array_view<std::int32_t const> columnIndices, winrt::array_view<winrt::hstring const> values,
            winrt::array_view<std::uint8_t const> foregrounds, winrt::array_view<std::uint8_t const> backgrounds,
            winrt::array_view<std::uint8_t const> icons);
        std::uint64_t UpdateBatchCount() const noexcept;
        std::uint64_t UpdateCellCount() const noexcept;
        std::uint64_t UpdateRenderCount() const noexcept;
        std::uint64_t LastUpdateLatencyMicroseconds() const noexcept;

    private:
        friend struct InteractionTrackerOwner;

        void build_visual_tree();
        void initialize_scroll_interaction();
        void update_scroll_interaction_bounds();
        void sync_scroll_interaction_position();
        void on_interaction_tracker_values_changed(
            Microsoft::UI::Composition::Interactions::InteractionTrackerValuesChangedArgs const& args);
        void on_interaction_tracker_idle();
        void create_device_resources();
        void update_theme_resources();
        void recover_device_resources() noexcept;
        void create_size_dependent_resources(double width, double height);
        void render();
        void request_render();
        void update_scrollbars();
        void update_viewport();
        void schedule_pages();
        void process_completions();
        void on_size_changed(IInspectable const&, Microsoft::UI::Xaml::SizeChangedEventArgs const& args);
        void on_loaded(IInspectable const&, Microsoft::UI::Xaml::RoutedEventArgs const&);
        void on_scroll(IInspectable const&, Microsoft::UI::Xaml::Controls::Primitives::RangeBaseValueChangedEventArgs const& args);
        void on_horizontal_scroll(IInspectable const&, Microsoft::UI::Xaml::Controls::Primitives::RangeBaseValueChangedEventArgs const& args);
        void on_pointer_pressed(IInspectable const&, Microsoft::UI::Xaml::Input::PointerRoutedEventArgs const& args);
        void on_key_down(IInspectable const&, Microsoft::UI::Xaml::Input::KeyRoutedEventArgs const& args);
        void on_got_focus(IInspectable const&, Microsoft::UI::Xaml::RoutedEventArgs const&);
        void on_lost_focus(IInspectable const&, Microsoft::UI::Xaml::RoutedEventArgs const&);
        void on_tick(IInspectable const&, IInspectable const&);
        void on_render_tick(IInspectable const&, IInspectable const&);
        [[nodiscard]] std::int32_t column_at(double x) const noexcept;
        void select_cell(std::int64_t row, std::int32_t column);
        void ensure_selection_visible();
        void shutdown() noexcept;

        std::int64_t m_row_count{ 10'000'000 };
        double m_row_height{ 24.0 };
        double m_scroll_offset{};
        double m_horizontal_offset{};
        double m_width{};
        double m_height{};
        double m_surface_height{};
        velocity_grid::viewport_range m_viewport{};
        velocity_grid::page_cache m_cache{ 8 };
        velocity_grid::request_scheduler m_scheduler;
        std::unordered_set<std::int64_t> m_wanted_pages;
        std::uint64_t m_generation{ 1 };
        std::int64_t m_anchor_page{ -1 };
        std::int64_t m_previous_first_row{};
        std::uint64_t m_cache_hits{};
        std::uint64_t m_cache_misses{};
        struct column_definition
        {
            std::wstring header;
            double width;
            std::int32_t alignment;
        };
        std::vector<column_definition> m_columns;
        std::int64_t m_selected_row{ -1 };
        std::int32_t m_selected_column{ -1 };
        bool m_has_focus{};
        bool m_shutdown{};
        struct external_request
        {
            std::int64_t start_row;
            std::uint64_t generation;
        };
        bool m_external_provider_enabled{};
        std::int32_t m_visual_theme{};
        std::uint64_t m_next_external_request_id{ 1 };
        std::unordered_map<std::uint64_t, external_request> m_external_requests;
        std::uint64_t m_external_requested{};
        std::uint64_t m_external_canceled{};
        std::uint64_t m_external_stale{};
        std::uint64_t m_external_failed{};
        winrt::hstring m_last_provider_error;
        winrt::event<VelocityGrid_Native::PageRequestedHandler> m_page_requested;
        winrt::event<VelocityGrid_Native::PageCanceledHandler> m_page_canceled;
        winrt::event<VelocityGrid_Native::SelectionChangedHandler> m_selection_changed;

        Microsoft::UI::Xaml::Controls::Grid m_root{ nullptr };
        Microsoft::UI::Xaml::Controls::SwapChainPanel m_surface{ nullptr };
        Microsoft::UI::Xaml::Controls::Slider m_scrollbar{ nullptr };
        Microsoft::UI::Xaml::Controls::Slider m_horizontal_scrollbar{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBlock m_diagnostics{ nullptr };
        Microsoft::UI::Xaml::DispatcherTimer m_timer{ nullptr };
        Microsoft::UI::Xaml::DispatcherTimer m_render_timer{ nullptr };
        winrt::event_token m_size_changed_token{};
        winrt::event_token m_loaded_token{};
        winrt::event_token m_pointer_pressed_token{};
        winrt::event_token m_key_down_token{};
        winrt::event_token m_got_focus_token{};
        winrt::event_token m_lost_focus_token{};
        winrt::event_token m_vertical_scroll_token{};
        winrt::event_token m_horizontal_scroll_token{};
        winrt::event_token m_timer_token{};
        winrt::event_token m_render_timer_token{};
        bool m_render_pending{};
        bool m_update_render_pending{};
        std::chrono::steady_clock::time_point m_oldest_update{};
        std::uint64_t m_update_batch_count{};
        std::uint64_t m_update_cell_count{};
        std::uint64_t m_update_render_count{};
        std::uint64_t m_last_update_latency_microseconds{};
        std::uint64_t m_wheel_event_count{};
        std::int32_t m_last_wheel_delta{};
        bool m_updating_from_interaction_tracker{};
        velocity_grid::interaction_window m_interaction_window{};
        Microsoft::UI::Composition::Interactions::IInteractionTrackerOwner m_interaction_tracker_owner{ nullptr };
        Microsoft::UI::Composition::Interactions::InteractionTracker m_interaction_tracker{ nullptr };
        Microsoft::UI::Composition::Interactions::VisualInteractionSource m_visual_interaction_source{ nullptr };

        ::Microsoft::WRL::ComPtr<ID3D11Device> m_d3d_device;
        ::Microsoft::WRL::ComPtr<ID3D11DeviceContext> m_d3d_context;
        ::Microsoft::WRL::ComPtr<IDXGISwapChain1> m_swap_chain;
        ::Microsoft::WRL::ComPtr<ID2D1Factory1> m_d2d_factory;
        ::Microsoft::WRL::ComPtr<ID2D1Device> m_d2d_device;
        ::Microsoft::WRL::ComPtr<ID2D1DeviceContext> m_d2d_context;
        ::Microsoft::WRL::ComPtr<ID2D1Bitmap1> m_target_bitmap;
        ::Microsoft::WRL::ComPtr<ID2D1SolidColorBrush> m_line_brush;
        ::Microsoft::WRL::ComPtr<ID2D1SolidColorBrush> m_text_brush;
        ::Microsoft::WRL::ComPtr<ID2D1SolidColorBrush> m_header_brush;
        ::Microsoft::WRL::ComPtr<ID2D1SolidColorBrush> m_selection_brush;
        ::Microsoft::WRL::ComPtr<ID2D1SolidColorBrush> m_focus_brush;
        ::Microsoft::WRL::ComPtr<ID2D1SolidColorBrush> m_cell_format_brush;
        ::Microsoft::WRL::ComPtr<IDWriteFactory> m_dwrite_factory;
        std::array<::Microsoft::WRL::ComPtr<IDWriteTextFormat>, 3> m_text_formats;
        std::uint64_t m_frame_count{};
        std::uint64_t m_fps_frame_count{};
        std::chrono::steady_clock::time_point m_fps_epoch{ std::chrono::steady_clock::now() };
    };
}

namespace winrt::VelocityGrid_Native::factory_implementation
{
    struct VelocityGrid : VelocityGridT<VelocityGrid, implementation::VelocityGrid>
    {
    };
}
