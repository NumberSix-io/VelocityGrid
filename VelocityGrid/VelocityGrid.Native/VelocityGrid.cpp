#include "pch.h"
#include "VelocityGrid.h"

#include <algorithm>
#include <array>
#include <cmath>
#include <ranges>
#include <microsoft.ui.xaml.media.dxinterop.h>

using namespace winrt;
using ::Microsoft::WRL::ComPtr;
using namespace winrt::Microsoft::UI::Xaml;
using namespace winrt::Microsoft::UI::Xaml::Controls;
using namespace winrt::Microsoft::UI::Xaml::Controls::Primitives;

namespace winrt::VelocityGrid_Native::implementation
{
    namespace
    {
        constexpr double scrollbar_width = 28.0;
        constexpr double scrollbar_height = 28.0;
        constexpr double diagnostics_height = 34.0;
        constexpr double content_width = 1300.0;
    }

    VelocityGrid::VelocityGrid()
    {
        build_visual_tree();
        create_device_resources();
        m_size_changed_token = m_root.SizeChanged({ this, &VelocityGrid::on_size_changed });
        m_pointer_wheel_token = m_root.PointerWheelChanged({ this, &VelocityGrid::on_pointer_wheel });

        m_timer = DispatcherTimer();
        m_timer.Interval(std::chrono::milliseconds(250));
        m_timer_token = m_timer.Tick({ this, &VelocityGrid::on_tick });
        m_timer.Start();
    }

    VelocityGrid::~VelocityGrid() noexcept
    {
        try
        {
            if (m_timer)
            {
                m_timer.Stop();
                m_timer.Tick(m_timer_token);
            }
            if (m_root)
            {
                m_root.SizeChanged(m_size_changed_token);
                m_root.PointerWheelChanged(m_pointer_wheel_token);
            }
            if (m_scrollbar) m_scrollbar.ValueChanged(m_vertical_scroll_token);
            if (m_horizontal_scrollbar) m_horizontal_scrollbar.ValueChanged(m_horizontal_scroll_token);
            for (auto const& [id, _] : m_external_requests) m_page_canceled(id);
            m_external_requests.clear();
            if (m_d2d_context) m_d2d_context->SetTarget(nullptr);
            if (m_surface)
            {
                auto panel_native = m_surface.as<ISwapChainPanelNative>();
                panel_native->SetSwapChain(nullptr);
            }
        }
        catch (...)
        {
            // Shutdown must not surface exceptions through a WinRT final release.
        }
    }

    std::int64_t VelocityGrid::RowCount() const noexcept { return m_row_count; }
    double VelocityGrid::RowHeight() const noexcept { return m_row_height; }
    std::int64_t VelocityGrid::FirstVisibleRow() const noexcept { return m_viewport.first_row; }
    std::int64_t VelocityGrid::LastVisibleRow() const noexcept { return m_viewport.last_row; }
    UIElement VelocityGrid::View() const noexcept { return m_root; }
    bool VelocityGrid::ExternalProviderEnabled() const noexcept { return m_external_provider_enabled; }

    void VelocityGrid::ExternalProviderEnabled(bool const value)
    {
        if (m_external_provider_enabled == value) return;
        for (auto const& [id, _] : m_external_requests) m_page_canceled(id);
        m_external_requests.clear();
        m_external_provider_enabled = value;
        m_cache.clear();
        ++m_generation;
        m_anchor_page = -1;
        m_last_provider_error = {};
        update_viewport();
    }

    event_token VelocityGrid::PageRequested(VelocityGrid_Native::PageRequestedHandler const& handler)
    {
        return m_page_requested.add(handler);
    }

    void VelocityGrid::PageRequested(event_token const& token) noexcept { m_page_requested.remove(token); }

    event_token VelocityGrid::PageCanceled(VelocityGrid_Native::PageCanceledHandler const& handler)
    {
        return m_page_canceled.add(handler);
    }

    void VelocityGrid::PageCanceled(event_token const& token) noexcept { m_page_canceled.remove(token); }

    void VelocityGrid::CompletePage(std::uint64_t const request_id, std::uint64_t const generation,
        std::int64_t const start_row, std::int32_t const row_count, array_view<hstring const> const values)
    {
        auto request = m_external_requests.find(request_id);
        if (request == m_external_requests.end() || request->second.generation != generation ||
            request->second.start_row != start_row || generation != m_generation ||
            !m_wanted_pages.contains(start_row))
        {
            ++m_external_stale;
            return;
        }
        m_external_requests.erase(request);
        if (row_count <= 0 || values.size() < static_cast<std::uint32_t>(row_count * 10))
        {
            ++m_external_failed;
            m_last_provider_error = L"Provider returned an incomplete page";
            return;
        }
        velocity_grid::page page{ start_row, row_count };
        page.values.reserve(values.size());
        for (auto const& value : values) page.values.emplace_back(value.c_str());
        m_cache.insert(std::move(page));
        m_last_provider_error = {};
        render();
    }

    void VelocityGrid::FailPage(std::uint64_t const request_id, std::uint64_t const generation,
        hstring const& message)
    {
        auto request = m_external_requests.find(request_id);
        if (request == m_external_requests.end() || request->second.generation != generation)
        {
            ++m_external_stale;
            return;
        }
        m_external_requests.erase(request);
        ++m_external_failed;
        m_last_provider_error = message;
    }

    void VelocityGrid::RowCount(std::int64_t const value)
    {
        m_row_count = (std::max<std::int64_t>)(0, value);
        update_scrollbars();
        update_viewport();
    }

    void VelocityGrid::RowHeight(double const value)
    {
        if (std::isfinite(value) && value >= 8.0)
        {
            m_row_height = value;
            update_scrollbars();
            update_viewport();
        }
    }

    void VelocityGrid::build_visual_tree()
    {
        m_root = Grid();
        m_root.Background(Media::SolidColorBrush(Windows::UI::ColorHelper::FromArgb(255, 245, 247, 250)));
        ColumnDefinition surface_column;
        surface_column.Width(GridLengthHelper::FromValueAndType(1.0, GridUnitType::Star));
        ColumnDefinition scrollbar_column;
        scrollbar_column.Width(GridLengthHelper::FromPixels(scrollbar_width));
        m_root.ColumnDefinitions().Append(surface_column);
        m_root.ColumnDefinitions().Append(scrollbar_column);
        RowDefinition diagnostics_row;
        diagnostics_row.Height(GridLengthHelper::FromPixels(diagnostics_height));
        RowDefinition surface_row;
        surface_row.Height(GridLengthHelper::FromValueAndType(1.0, GridUnitType::Star));
        RowDefinition scrollbar_row;
        scrollbar_row.Height(GridLengthHelper::FromPixels(scrollbar_height));
        m_root.RowDefinitions().Append(diagnostics_row);
        m_root.RowDefinitions().Append(surface_row);
        m_root.RowDefinitions().Append(scrollbar_row);

        m_surface = SwapChainPanel();
        Grid::SetColumn(m_surface, 0);
        Grid::SetRow(m_surface, 1);
        m_root.Children().Append(m_surface);

        auto const track_brush = Media::SolidColorBrush(Windows::UI::ColorHelper::FromArgb(255, 210, 216, 224));
        Border vertical_track;
        vertical_track.Background(track_brush);
        Grid::SetColumn(vertical_track, 1);
        Grid::SetRow(vertical_track, 1);
        m_root.Children().Append(vertical_track);

        m_scrollbar = Slider();
        m_scrollbar.Orientation(Orientation::Vertical);
        // A vertical Slider maps its minimum to the bottom by default. A grid
        // scrollbar must map offset zero to the top and its maximum to the bottom.
        m_scrollbar.IsDirectionReversed(true);
        m_scrollbar.Width(scrollbar_width);
        m_scrollbar.HorizontalAlignment(HorizontalAlignment::Stretch);
        m_scrollbar.VerticalAlignment(VerticalAlignment::Stretch);
        m_scrollbar.Visibility(Visibility::Visible);
        m_scrollbar.Background(Media::SolidColorBrush(Windows::UI::ColorHelper::FromArgb(255, 225, 229, 235)));
        m_scrollbar.Foreground(Media::SolidColorBrush(Windows::UI::ColorHelper::FromArgb(255, 45, 55, 72)));
        m_scrollbar.StepFrequency(m_row_height * 3.0);
        m_scrollbar.IsThumbToolTipEnabled(true);
        m_vertical_scroll_token = m_scrollbar.ValueChanged({ this, &VelocityGrid::on_scroll });
        Grid::SetColumn(m_scrollbar, 1);
        Grid::SetRow(m_scrollbar, 1);
        m_root.Children().Append(m_scrollbar);

        Border horizontal_track;
        horizontal_track.Background(track_brush);
        Grid::SetColumn(horizontal_track, 0);
        Grid::SetRow(horizontal_track, 2);
        m_root.Children().Append(horizontal_track);

        m_horizontal_scrollbar = Slider();
        m_horizontal_scrollbar.Orientation(Orientation::Horizontal);
        m_horizontal_scrollbar.Height(scrollbar_height);
        m_horizontal_scrollbar.HorizontalAlignment(HorizontalAlignment::Stretch);
        m_horizontal_scrollbar.VerticalAlignment(VerticalAlignment::Stretch);
        m_horizontal_scrollbar.Visibility(Visibility::Visible);
        m_horizontal_scrollbar.Background(Media::SolidColorBrush(Windows::UI::ColorHelper::FromArgb(255, 225, 229, 235)));
        m_horizontal_scrollbar.Foreground(Media::SolidColorBrush(Windows::UI::ColorHelper::FromArgb(255, 45, 55, 72)));
        m_horizontal_scrollbar.StepFrequency(130.0);
        m_horizontal_scrollbar.IsThumbToolTipEnabled(true);
        m_horizontal_scroll_token = m_horizontal_scrollbar.ValueChanged({ this, &VelocityGrid::on_horizontal_scroll });
        Grid::SetColumn(m_horizontal_scrollbar, 0);
        Grid::SetRow(m_horizontal_scrollbar, 2);
        m_root.Children().Append(m_horizontal_scrollbar);

        Border corner;
        corner.Background(track_brush);
        Grid::SetColumn(corner, 1);
        Grid::SetRow(corner, 2);
        m_root.Children().Append(corner);

        m_diagnostics = TextBlock();
        m_diagnostics.Padding(ThicknessHelper::FromLengths(8, 6, 8, 6));
        m_diagnostics.HorizontalAlignment(HorizontalAlignment::Left);
        m_diagnostics.VerticalAlignment(VerticalAlignment::Center);
        m_diagnostics.Foreground(Media::SolidColorBrush(Windows::UI::ColorHelper::FromArgb(255, 25, 32, 45)));
        Grid::SetColumn(m_diagnostics, 0);
        Grid::SetRow(m_diagnostics, 0);
        Grid::SetColumnSpan(m_diagnostics, 2);
        m_root.Children().Append(m_diagnostics);
    }

    void VelocityGrid::create_device_resources()
    {
        UINT flags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;
        std::array<D3D_FEATURE_LEVEL, 3> const levels{
            D3D_FEATURE_LEVEL_11_1, D3D_FEATURE_LEVEL_11_0, D3D_FEATURE_LEVEL_10_0 };
        D3D_FEATURE_LEVEL selected{};
        auto result = D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, flags,
            levels.data(), static_cast<UINT>(levels.size()), D3D11_SDK_VERSION,
            &m_d3d_device, &selected, &m_d3d_context);
        check_hresult(result);

        D2D1_FACTORY_OPTIONS options{};
        check_hresult(D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED, options, m_d2d_factory.GetAddressOf()));
        ComPtr<IDXGIDevice> dxgi_device;
        check_hresult(m_d3d_device.As(&dxgi_device));
        check_hresult(m_d2d_factory->CreateDevice(dxgi_device.Get(), &m_d2d_device));
        check_hresult(m_d2d_device->CreateDeviceContext(D2D1_DEVICE_CONTEXT_OPTIONS_NONE, &m_d2d_context));
        check_hresult(DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED, __uuidof(IDWriteFactory), &m_dwrite_factory));
        check_hresult(m_dwrite_factory->CreateTextFormat(L"Segoe UI", nullptr, DWRITE_FONT_WEIGHT_NORMAL,
            DWRITE_FONT_STYLE_NORMAL, DWRITE_FONT_STRETCH_NORMAL, 12.0f, L"en-GB", &m_text_format));
        m_text_format->SetWordWrapping(DWRITE_WORD_WRAPPING_NO_WRAP);
    }

    void VelocityGrid::create_size_dependent_resources(double const width, double const height)
    {
        if (width < 1.0 || height < 1.0) return;
        m_target_bitmap.Reset();
        m_d2d_context->SetTarget(nullptr);

        auto const pixel_width = (std::max)(1u, static_cast<UINT>(std::lround(width)));
        auto const pixel_height = (std::max)(1u, static_cast<UINT>(std::lround(height)));
        if (m_swap_chain)
        {
            check_hresult(m_swap_chain->ResizeBuffers(2, pixel_width, pixel_height, DXGI_FORMAT_B8G8R8A8_UNORM, 0));
        }
        else
        {
            ComPtr<IDXGIDevice> dxgi_device;
            check_hresult(m_d3d_device.As(&dxgi_device));
            ComPtr<IDXGIAdapter> adapter;
            check_hresult(dxgi_device->GetAdapter(&adapter));
            ComPtr<IDXGIFactory2> factory;
            check_hresult(adapter->GetParent(IID_PPV_ARGS(&factory)));
            DXGI_SWAP_CHAIN_DESC1 description{};
            description.Width = pixel_width;
            description.Height = pixel_height;
            description.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
            description.SampleDesc.Count = 1;
            description.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
            description.BufferCount = 2;
            description.SwapEffect = DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL;
            description.Scaling = DXGI_SCALING_STRETCH;
            description.AlphaMode = DXGI_ALPHA_MODE_IGNORE;
            check_hresult(factory->CreateSwapChainForComposition(m_d3d_device.Get(), &description, nullptr, &m_swap_chain));
            auto panel_native = m_surface.as<ISwapChainPanelNative>();
            check_hresult(panel_native->SetSwapChain(m_swap_chain.Get()));
        }

        ComPtr<IDXGISurface> back_buffer;
        check_hresult(m_swap_chain->GetBuffer(0, IID_PPV_ARGS(&back_buffer)));
        auto const properties = D2D1::BitmapProperties1(
            D2D1_BITMAP_OPTIONS_TARGET | D2D1_BITMAP_OPTIONS_CANNOT_DRAW,
            D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_IGNORE));
        check_hresult(m_d2d_context->CreateBitmapFromDxgiSurface(back_buffer.Get(), &properties, &m_target_bitmap));
        m_d2d_context->SetTarget(m_target_bitmap.Get());
        update_scrollbars();
        update_viewport();
    }

    void VelocityGrid::render()
    {
        if (!m_swap_chain || !m_target_bitmap) return;
        m_d2d_context->BeginDraw();
        m_d2d_context->Clear(D2D1::ColorF(0.96f, 0.97f, 0.98f));

        ComPtr<ID2D1SolidColorBrush> line_brush;
        ComPtr<ID2D1SolidColorBrush> text_brush;
        check_hresult(m_d2d_context->CreateSolidColorBrush(D2D1::ColorF(0.78f, 0.81f, 0.85f), &line_brush));
        check_hresult(m_d2d_context->CreateSolidColorBrush(D2D1::ColorF(0.10f, 0.13f, 0.18f), &text_brush));

        constexpr float column_width = 130.0f;
        for (std::int32_t visible = 0; visible < m_viewport.visible_row_count; ++visible)
        {
            auto const row = m_viewport.first_row + visible;
            auto const cached_page = m_cache.find_row(row);
            auto const cached = cached_page.has_value();
            auto const* page = cached ? &cached_page->get() : nullptr;
            if (cached) ++m_cache_hits;
            else ++m_cache_misses;
            auto const top = static_cast<float>(visible * m_row_height - m_viewport.leading_row_offset);
            auto const bottom = top + static_cast<float>(m_row_height);
            m_d2d_context->DrawLine({ 0.0f, bottom }, { static_cast<float>(m_width), bottom }, line_brush.Get(), 1.0f);
            for (int column = 0; column < 10; ++column)
            {
                auto const left = column * column_width - static_cast<float>(m_horizontal_offset);
                if (left >= m_width) break;
                if (left + column_width <= 0.0f) continue;
                m_d2d_context->DrawLine({ left, top }, { left, bottom }, line_brush.Get(), 1.0f);
                std::wstring value;
                if (page && !page->values.empty())
                {
                    auto const index = static_cast<std::size_t>((row - page->start_row) * 10 + column);
                    value = index < page->values.size() ? page->values[index] : L"";
                }
                else
                {
                    value = cached ? std::format(L"R{}  C{}", row, column + 1) : std::format(L"Loading row {}", row);
                }
                D2D1_RECT_F const bounds{ left + 7.0f, top + 4.0f, left + column_width - 4.0f, bottom };
                m_d2d_context->DrawText(value.c_str(), static_cast<UINT32>(value.size()), m_text_format.Get(), bounds, text_brush.Get());
            }
        }

        auto const result = m_d2d_context->EndDraw();
        if (result == D2DERR_RECREATE_TARGET)
        {
            create_size_dependent_resources(m_width, m_height);
            return;
        }
        check_hresult(result);
        check_hresult(m_swap_chain->Present(1, 0));
        ++m_frame_count;
    }

    void VelocityGrid::update_scrollbars()
    {
        auto const maximum = velocity_grid::clamp_scroll_offset(m_row_count, m_row_height, m_height, DBL_MAX);
        m_scrollbar.Maximum(maximum);
        m_scrollbar.StepFrequency(m_row_height * 3.0);
        m_scroll_offset = (std::min)(m_scroll_offset, maximum);
        m_scrollbar.Value(m_scroll_offset);

        auto const horizontal_maximum = (std::max)(0.0, content_width - m_width);
        m_horizontal_scrollbar.Maximum(horizontal_maximum);
        m_horizontal_offset = (std::clamp)(m_horizontal_offset, 0.0, horizontal_maximum);
        m_horizontal_scrollbar.Value(m_horizontal_offset);
    }

    void VelocityGrid::update_viewport()
    {
        m_scroll_offset = velocity_grid::clamp_scroll_offset(m_row_count, m_row_height, m_height, m_scroll_offset);
        m_viewport = velocity_grid::calculate_viewport(m_row_count, m_row_height, m_height, m_scroll_offset);
        schedule_pages();
        render();
    }

    void VelocityGrid::schedule_pages()
    {
        constexpr std::int32_t page_size = 128;
        if (m_viewport.visible_row_count <= 0) return;

        auto const anchor = (m_viewport.first_row / page_size) * page_size;
        if (anchor != m_anchor_page)
        {
            ++m_generation;
            m_anchor_page = anchor;
        }

        auto const scrolling_down = m_viewport.first_row >= m_previous_first_row;
        m_previous_first_row = m_viewport.first_row;
        auto const behind = scrolling_down ? 1 : 2;
        auto const ahead = scrolling_down ? 2 : 1;
        auto const last_visible_page = (m_viewport.last_row / page_size) * page_size;

        m_wanted_pages.clear();
        for (int offset = -behind; offset <= ahead + static_cast<int>((last_visible_page - anchor) / page_size); ++offset)
        {
            auto const start = anchor + static_cast<std::int64_t>(offset) * page_size;
            if (start < 0 || start >= m_row_count) continue;
            m_wanted_pages.insert(start);
        }

        if (m_external_provider_enabled)
        {
            for (auto request = m_external_requests.begin(); request != m_external_requests.end();)
            {
                if (request->second.generation != m_generation || !m_wanted_pages.contains(request->second.start_row))
                {
                    auto const id = request->first;
                    request = m_external_requests.erase(request);
                    ++m_external_canceled;
                    m_page_canceled(id);
                }
                else ++request;
            }
            for (auto const start : m_wanted_pages)
            {
                if (m_cache.contains_page(start)) continue;
                auto const already_requested = std::ranges::any_of(m_external_requests,
                    [start](auto const& item) { return item.second.start_row == start; });
                if (already_requested) continue;
                auto const count = static_cast<std::int32_t>((std::min<std::int64_t>)(page_size, m_row_count - start));
                auto const id = m_next_external_request_id++;
                m_external_requests.emplace(id, external_request{ start, m_generation });
                ++m_external_requested;
                m_page_requested(start, count, id, m_generation);
            }
            return;
        }

        for (auto const start : m_wanted_pages)
        {
            if (!m_cache.contains_page(start))
            {
                auto const count = static_cast<std::int32_t>((std::min<std::int64_t>)(page_size, m_row_count - start));
                m_scheduler.request(start, count, m_generation);
            }
        }
        m_scheduler.cancel_obsolete(m_generation, m_wanted_pages);
    }

    void VelocityGrid::process_completions()
    {
        auto changed = false;
        auto const completions = m_scheduler.drain_completions();
        for (auto const& completion : completions)
        {
            if (completion.canceled) continue;
            if (completion.generation != m_generation || !m_wanted_pages.contains(completion.value.start_row))
            {
                m_scheduler.record_stale();
                continue;
            }
            m_cache.insert(completion.value);
            changed = true;
        }
        if (!completions.empty()) schedule_pages();
        if (changed) render();
    }

    void VelocityGrid::on_size_changed(IInspectable const&, SizeChangedEventArgs const& args)
    {
        m_width = (std::max)(0.0, args.NewSize().Width - scrollbar_width);
        m_height = (std::max)(0.0, args.NewSize().Height - diagnostics_height - scrollbar_height);
        create_size_dependent_resources(m_width, m_height);
    }

    void VelocityGrid::on_scroll(IInspectable const&, RangeBaseValueChangedEventArgs const& args)
    {
        m_scroll_offset = args.NewValue();
        update_viewport();
    }

    void VelocityGrid::on_horizontal_scroll(IInspectable const&, RangeBaseValueChangedEventArgs const& args)
    {
        m_horizontal_offset = args.NewValue();
        render();
    }

    void VelocityGrid::on_pointer_wheel(IInspectable const&, Input::PointerRoutedEventArgs const& args)
    {
        auto const delta = args.GetCurrentPoint(m_root).Properties().MouseWheelDelta();
        m_scroll_offset -= (static_cast<double>(delta) / 120.0) * m_row_height * 3.0;
        m_scroll_offset = velocity_grid::clamp_scroll_offset(m_row_count, m_row_height, m_height, m_scroll_offset);
        m_scrollbar.Value(m_scroll_offset);
        update_viewport();
        args.Handled(true);
    }

    void VelocityGrid::on_tick(IInspectable const&, IInspectable const&)
    {
        process_completions();
        auto const now = std::chrono::steady_clock::now();
        auto const elapsed = std::chrono::duration<double>(now - m_fps_epoch).count();
        auto const fps = elapsed > 0.0 ? m_frame_count / elapsed : 0.0;
        auto const metrics = m_scheduler.metrics();
        auto const cache_total = m_cache_hits + m_cache_misses;
        auto const hit_rate = cache_total == 0 ? 0.0 : 100.0 * m_cache_hits / cache_total;
        auto const requested = m_external_provider_enabled ? m_external_requested : metrics.requested;
        auto const canceled = m_external_provider_enabled ? m_external_canceled : metrics.canceled;
        auto const stale = m_external_provider_enabled ? m_external_stale : metrics.stale;
        auto const error = m_last_provider_error.empty() ? L"" : std::format(L" | Error {}", m_last_provider_error.c_str());
        m_diagnostics.Text(std::format(
            L"Viewport {:L}-{:L} | Cache {}/{} ({:.0f}%) | Requests {} | Canceled {} | Stale {} | Failed {} | {:.1f} FPS{}",
            m_viewport.first_row, m_viewport.last_row, m_cache.size(), m_cache.capacity(), hit_rate,
            requested, canceled, stale, m_external_failed, fps, error));
        if (elapsed >= 1.0)
        {
            m_frame_count = 0;
            m_fps_epoch = now;
        }
    }
}
