#include "pch.h"
#include "VelocityGrid.h"

#include <algorithm>
#include <array>
#include <cmath>
#include <ranges>
#include <numeric>
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
        constexpr double header_height = 32.0;

        D2D1_COLOR_F palette_colour(std::uint8_t const id, D2D1_COLOR_F const fallback) noexcept
        {
            // Index zero means no colour override. Remaining entries mirror the
            // public VelocityGridColor enum and deliberately carry no semantics.
            static constexpr std::array<std::uint32_t, 26> colours{
                0x000000, 0x000000, 0xFFFFFF, 0x374151, 0x6B7280, 0xD1D5DB,
                0x991B1B, 0xDC2626, 0xFECACA, 0xEA580C, 0xD97706, 0xEAB308,
                0x65A30D, 0x166534, 0x16A34A, 0xBBF7D0, 0x0D9488, 0x0891B2,
                0x1E3A8A, 0x2563EB, 0xBFDBFE, 0x4F46E5, 0x7C3AED, 0x9333EA,
                0xDB2777, 0x92400E };
            if (id == 0 || id >= colours.size()) return fallback;
            auto const value = colours[id];
            return D2D1::ColorF(
                static_cast<float>((value >> 16) & 0xff) / 255.0f,
                static_cast<float>((value >> 8) & 0xff) / 255.0f,
                static_cast<float>(value & 0xff) / 255.0f);
        }
    }

    VelocityGrid::VelocityGrid()
    {
        for (int column = 0; column < 10; ++column)
            m_columns.push_back({ std::format(L"Column {}", column + 1), 130.0, 0 });
        build_visual_tree();
        create_device_resources();
        m_size_changed_token = m_root.SizeChanged({ this, &VelocityGrid::on_size_changed });
        m_pointer_wheel_token = m_root.PointerWheelChanged({ this, &VelocityGrid::on_pointer_wheel });
        m_pointer_pressed_token = m_root.PointerPressed({ this, &VelocityGrid::on_pointer_pressed });
        m_key_down_token = m_root.KeyDown({ this, &VelocityGrid::on_key_down });

        m_render_timer = DispatcherTimer();
        m_render_timer.Interval(std::chrono::milliseconds(16));
        m_render_timer_token = m_render_timer.Tick({ this, &VelocityGrid::on_render_tick });

        m_timer = DispatcherTimer();
        m_timer.Interval(std::chrono::milliseconds(250));
        m_timer_token = m_timer.Tick({ this, &VelocityGrid::on_tick });
        m_timer.Start();
    }

    VelocityGrid::~VelocityGrid() noexcept
    {
        shutdown();
    }

    std::int64_t VelocityGrid::RowCount() const noexcept { return m_row_count; }
    double VelocityGrid::RowHeight() const noexcept { return m_row_height; }
    std::int64_t VelocityGrid::FirstVisibleRow() const noexcept { return m_viewport.first_row; }
    std::int64_t VelocityGrid::LastVisibleRow() const noexcept { return m_viewport.last_row; }
    UIElement VelocityGrid::View() const noexcept { return m_root; }
    bool VelocityGrid::ExternalProviderEnabled() const noexcept { return m_external_provider_enabled; }
    std::int32_t VelocityGrid::VisualTheme() const noexcept { return m_visual_theme; }

    void VelocityGrid::VisualTheme(std::int32_t const value)
    {
        auto const theme = (std::clamp)(value, 0, 2);
        if (theme == m_visual_theme) return;
        m_visual_theme = theme;
        update_theme_resources();
        request_render();
    }

    bool VelocityGrid::HasKeyboardFocus() const noexcept { return m_has_focus; }
    void VelocityGrid::HasKeyboardFocus(bool const value)
    {
        if (m_has_focus == value) return;
        m_has_focus = value;
        request_render();
    }

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
        std::int64_t const start_row, std::int32_t const row_count, array_view<hstring const> const values,
        array_view<std::uint8_t const> const foregrounds, array_view<std::uint8_t const> const backgrounds,
        array_view<std::uint8_t const> const icons)
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
        auto const column_count = m_columns.size();
        auto const cell_count = row_count > 0
            ? static_cast<std::uint64_t>(row_count) * column_count : 0;
        if (row_count <= 0 || column_count == 0 || values.size() != cell_count ||
            foregrounds.size() != cell_count || backgrounds.size() != cell_count || icons.size() != cell_count)
        {
            ++m_external_failed;
            m_last_provider_error = L"Provider returned an incomplete page";
            return;
        }
        velocity_grid::page page{ start_row, row_count, static_cast<std::int32_t>(column_count) };
        page.values.reserve(values.size());
        for (auto const& value : values) page.values.emplace_back(value.c_str());
        page.formats.reserve(cell_count);
        for (std::uint64_t index = 0; index < cell_count; ++index)
            page.formats.push_back({ foregrounds[index], backgrounds[index], icons[index] });
        m_cache.insert(std::move(page));
        m_last_provider_error = {};
        request_render();
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

    void VelocityGrid::SetColumns(array_view<hstring const> const headers,
        array_view<double const> const widths, array_view<std::int32_t const> const alignments)
    {
        if (headers.empty() || headers.size() != widths.size() || headers.size() != alignments.size())
            throw hresult_invalid_argument(L"Columns require matching, non-empty header, width, and alignment arrays.");
        std::vector<column_definition> columns;
        columns.reserve(headers.size());
        for (std::uint32_t index = 0; index < headers.size(); ++index)
        {
            if (!std::isfinite(widths[index]) || widths[index] < 32.0)
                throw hresult_invalid_argument(L"Column widths must be finite and at least 32 DIPs.");
            columns.push_back({ headers[index].c_str(), widths[index], (std::clamp)(alignments[index], 0, 2) });
        }
        for (auto const& [id, _] : m_external_requests) m_page_canceled(id);
        m_external_requests.clear();
        m_cache.clear();
        ++m_generation;
        m_anchor_page = -1;
        m_columns = std::move(columns);
        if (m_selected_column >= static_cast<std::int32_t>(m_columns.size())) m_selected_column = -1;
        update_scrollbars();
        update_viewport();
    }

    std::int64_t VelocityGrid::SelectedRow() const noexcept { return m_selected_row; }
    std::int32_t VelocityGrid::SelectedColumn() const noexcept { return m_selected_column; }
    event_token VelocityGrid::SelectionChanged(VelocityGrid_Native::SelectionChangedHandler const& handler)
    {
        return m_selection_changed.add(handler);
    }
    void VelocityGrid::SelectionChanged(event_token const& token) noexcept { m_selection_changed.remove(token); }

    void VelocityGrid::NavigateSelection(std::int32_t const command)
    {
        if (m_row_count <= 0 || m_columns.empty()) return;
        auto row = m_selected_row < 0 ? 0 : m_selected_row;
        auto column = m_selected_column < 0 ? 0 : m_selected_column;
        switch (command)
        {
        case 0: --row; break;
        case 1: ++row; break;
        case 2: --column; break;
        case 3: ++column; break;
        case 4: column = 0; break;
        case 5: column = static_cast<std::int32_t>(m_columns.size()) - 1; break;
        case 6: row -= (std::max<std::int64_t>)(1, static_cast<std::int64_t>(m_height / m_row_height)); break;
        case 7: row += (std::max<std::int64_t>)(1, static_cast<std::int64_t>(m_height / m_row_height)); break;
        default: throw hresult_invalid_argument(L"Unknown selection navigation command.");
        }
        m_has_focus = true;
        select_cell(row, column);
        ensure_selection_visible();
    }

    void VelocityGrid::ScrollToRow(std::int64_t const row_index)
    {
        if (m_row_count <= 0) return;
        auto const row = (std::clamp<std::int64_t>)(row_index, 0, m_row_count - 1);
        m_scroll_offset = velocity_grid::clamp_scroll_offset(
            m_row_count, m_row_height, m_height, static_cast<double>(row) * m_row_height);
        auto const previous_value = m_scrollbar.Value();
        m_scrollbar.Value(m_scroll_offset);
        // A changed Slider value synchronously raises ValueChanged, which updates
        // and renders the viewport. Only update directly when no event is raised.
        if (previous_value == m_scroll_offset) update_viewport();
    }

    std::uint64_t VelocityGrid::FrameCount() const noexcept { return m_frame_count; }
    std::uint64_t VelocityGrid::CacheHits() const noexcept { return m_cache_hits; }
    std::uint64_t VelocityGrid::CacheMisses() const noexcept { return m_cache_misses; }
    std::uint64_t VelocityGrid::RequestCount() const noexcept { return m_external_requested; }

    void VelocityGrid::ResetMetrics() noexcept
    {
        m_frame_count = 0;
        m_fps_frame_count = 0;
        m_cache_hits = 0;
        m_cache_misses = 0;
        m_external_requested = 0;
        m_update_batch_count = 0;
        m_update_cell_count = 0;
        m_update_render_count = 0;
        m_last_update_latency_microseconds = 0;
        m_fps_epoch = std::chrono::steady_clock::now();
    }

    void VelocityGrid::ApplyUpdates(array_view<std::int64_t const> const row_indices,
        array_view<std::int32_t const> const column_indices, array_view<hstring const> const values,
        array_view<std::uint8_t const> const foregrounds, array_view<std::uint8_t const> const backgrounds,
        array_view<std::uint8_t const> const icons)
    {
        if (row_indices.size() != column_indices.size() || row_indices.size() != values.size() ||
            row_indices.size() != foregrounds.size() || row_indices.size() != backgrounds.size() ||
            row_indices.size() != icons.size())
            throw hresult_invalid_argument(L"Streaming update arrays must have matching lengths.");
        if (row_indices.empty()) return;

        ++m_update_batch_count;
        auto applied = std::uint64_t{};
        auto visible_applied = std::uint64_t{};
        auto apply = [&](std::uint32_t const index)
        {
            auto const row = row_indices[index];
            auto const column = column_indices[index];
            if (row < 0 || row >= m_row_count || column < 0 ||
                column >= static_cast<std::int32_t>(m_columns.size())) return;
            if (m_cache.update_cell(row, column, values[index].c_str(),
                { foregrounds[index], backgrounds[index], icons[index] }))
            {
                ++applied;
                if (row >= m_viewport.first_row && row <= m_viewport.last_row) ++visible_applied;
            }
        };

        // Visible mutations are applied first so the next coalesced frame contains
        // the newest on-screen values even for very large batches.
        for (std::uint32_t index = 0; index < row_indices.size(); ++index)
            if (row_indices[index] >= m_viewport.first_row && row_indices[index] <= m_viewport.last_row) apply(index);
        for (std::uint32_t index = 0; index < row_indices.size(); ++index)
            if (row_indices[index] < m_viewport.first_row || row_indices[index] > m_viewport.last_row) apply(index);

        m_update_cell_count += applied;
        if (visible_applied == 0) return;
        if (!m_update_render_pending) m_oldest_update = std::chrono::steady_clock::now();
        m_update_render_pending = true;
        request_render();
    }

    std::uint64_t VelocityGrid::UpdateBatchCount() const noexcept { return m_update_batch_count; }
    std::uint64_t VelocityGrid::UpdateCellCount() const noexcept { return m_update_cell_count; }
    std::uint64_t VelocityGrid::UpdateRenderCount() const noexcept { return m_update_render_count; }
    std::uint64_t VelocityGrid::LastUpdateLatencyMicroseconds() const noexcept { return m_last_update_latency_microseconds; }

    void VelocityGrid::shutdown() noexcept
    {
        if (m_shutdown) return;
        m_shutdown = true;
        try
        {
            if (m_timer)
            {
                m_timer.Stop();
                m_timer.Tick(m_timer_token);
            }
            if (m_render_timer)
            {
                m_render_timer.Stop();
                m_render_timer.Tick(m_render_timer_token);
            }
            if (m_root)
            {
                m_root.SizeChanged(m_size_changed_token);
                m_root.PointerWheelChanged(m_pointer_wheel_token);
                m_root.PointerPressed(m_pointer_pressed_token);
                m_root.KeyDown(m_key_down_token);
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
        check_hresult(m_d2d_context->CreateSolidColorBrush(D2D1::ColorF(0.78f, 0.81f, 0.85f), &m_line_brush));
        check_hresult(m_d2d_context->CreateSolidColorBrush(D2D1::ColorF(0.10f, 0.13f, 0.18f), &m_text_brush));
        check_hresult(m_d2d_context->CreateSolidColorBrush(D2D1::ColorF(0.88f, 0.91f, 0.95f), &m_header_brush));
        check_hresult(m_d2d_context->CreateSolidColorBrush(D2D1::ColorF(0.78f, 0.87f, 0.98f), &m_selection_brush));
        check_hresult(m_d2d_context->CreateSolidColorBrush(D2D1::ColorF(0.10f, 0.35f, 0.72f), &m_focus_brush));
        check_hresult(m_d2d_context->CreateSolidColorBrush(D2D1::ColorF(0.10f, 0.13f, 0.18f), &m_cell_format_brush));
        check_hresult(DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED, __uuidof(IDWriteFactory), &m_dwrite_factory));
        constexpr std::array alignments{
            DWRITE_TEXT_ALIGNMENT_LEADING, DWRITE_TEXT_ALIGNMENT_CENTER, DWRITE_TEXT_ALIGNMENT_TRAILING };
        for (std::size_t index = 0; index < m_text_formats.size(); ++index)
        {
            check_hresult(m_dwrite_factory->CreateTextFormat(L"Segoe UI", nullptr, DWRITE_FONT_WEIGHT_NORMAL,
                DWRITE_FONT_STYLE_NORMAL, DWRITE_FONT_STRETCH_NORMAL, 12.0f, L"en-GB", &m_text_formats[index]));
            check_hresult(m_text_formats[index]->SetWordWrapping(DWRITE_WORD_WRAPPING_NO_WRAP));
            check_hresult(m_text_formats[index]->SetTextAlignment(alignments[index]));
        }
    }

    void VelocityGrid::update_theme_resources()
    {
        if (!m_line_brush) return;
        if (m_visual_theme == 2)
        {
            m_line_brush->SetColor(D2D1::ColorF(D2D1::ColorF::White));
            m_text_brush->SetColor(D2D1::ColorF(D2D1::ColorF::White));
            m_header_brush->SetColor(D2D1::ColorF(D2D1::ColorF::Black));
            m_selection_brush->SetColor(D2D1::ColorF(D2D1::ColorF::Yellow));
            m_focus_brush->SetColor(D2D1::ColorF(D2D1::ColorF::White));
        }
        else if (m_visual_theme == 1)
        {
            m_line_brush->SetColor(D2D1::ColorF(0.27f, 0.30f, 0.35f));
            m_text_brush->SetColor(D2D1::ColorF(0.92f, 0.94f, 0.97f));
            m_header_brush->SetColor(D2D1::ColorF(0.13f, 0.15f, 0.19f));
            m_selection_brush->SetColor(D2D1::ColorF(0.13f, 0.30f, 0.52f));
            m_focus_brush->SetColor(D2D1::ColorF(0.45f, 0.70f, 1.0f));
        }
        else
        {
            m_line_brush->SetColor(D2D1::ColorF(0.78f, 0.81f, 0.85f));
            m_text_brush->SetColor(D2D1::ColorF(0.10f, 0.13f, 0.18f));
            m_header_brush->SetColor(D2D1::ColorF(0.88f, 0.91f, 0.95f));
            m_selection_brush->SetColor(D2D1::ColorF(0.78f, 0.87f, 0.98f));
            m_focus_brush->SetColor(D2D1::ColorF(0.10f, 0.35f, 0.72f));
        }
        // Keep theme changes inside the Direct2D renderer. Mutating XAML controls
        // from this native resource path can run while the projected control is
        // still being constructed and causes Microsoft.UI.Xaml to fail fast.
    }

    void VelocityGrid::recover_device_resources() noexcept
    {
        try
        {
            if (m_surface)
                m_surface.as<ISwapChainPanelNative>()->SetSwapChain(nullptr);
            if (m_d2d_context) m_d2d_context->SetTarget(nullptr);
            m_target_bitmap.Reset();
            m_cell_format_brush.Reset();
            m_focus_brush.Reset();
            m_selection_brush.Reset();
            m_header_brush.Reset();
            m_text_brush.Reset();
            m_line_brush.Reset();
            for (auto& format : m_text_formats) format.Reset();
            m_dwrite_factory.Reset();
            m_swap_chain.Reset();
            m_d2d_context.Reset();
            m_d2d_device.Reset();
            m_d2d_factory.Reset();
            m_d3d_context.Reset();
            m_d3d_device.Reset();
            create_device_resources();
            create_size_dependent_resources(m_width, m_surface_height);
        }
        catch (...)
        {
            m_last_provider_error = L"Graphics device recovery failed";
        }
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
        if (m_render_pending)
        {
            m_render_pending = false;
            if (m_render_timer) m_render_timer.Stop();
        }
        m_d2d_context->BeginDraw();
        m_d2d_context->Clear(m_visual_theme == 0
            ? D2D1::ColorF(0.96f, 0.97f, 0.98f) : D2D1::ColorF(0.06f, 0.07f, 0.09f));

        struct visible_column
        {
            std::size_t index;
            float left;
            float right;
            IDWriteTextFormat* text_format;
        };
        std::vector<visible_column> visible_columns;
        visible_columns.reserve(m_columns.size());
        double logical_left = 0.0;
        for (std::size_t column = 0; column < m_columns.size(); ++column)
        {
            auto const left = static_cast<float>(logical_left - m_horizontal_offset);
            auto const right = left + static_cast<float>(m_columns[column].width);
            logical_left += m_columns[column].width;
            if (left >= m_width) break;
            if (right <= 0.0f) continue;
            visible_columns.push_back({
                column, left, right, m_text_formats[m_columns[column].alignment].Get() });
        }

        m_d2d_context->FillRectangle({ 0.0f, 0.0f, static_cast<float>(m_width), static_cast<float>(header_height) }, m_header_brush.Get());
        for (auto const& layout : visible_columns)
        {
            auto const left = layout.left;
            auto const right = layout.right;
            m_d2d_context->DrawLine({ right, 0.0f }, { right, static_cast<float>(m_surface_height) }, m_line_brush.Get());
            D2D1_RECT_F const header_bounds{ left + 7.0f, 7.0f, right - 7.0f, static_cast<float>(header_height) };
            auto const& header = m_columns[layout.index].header;
            m_d2d_context->DrawText(header.c_str(), static_cast<UINT32>(header.size()), layout.text_format, header_bounds, m_text_brush.Get());
        }
        m_d2d_context->DrawLine({ 0.0f, static_cast<float>(header_height) },
            { static_cast<float>(m_width), static_cast<float>(header_height) }, m_line_brush.Get(), 1.0f);

        for (std::int32_t visible = 0; visible < m_viewport.visible_row_count; ++visible)
        {
            auto const row = m_viewport.first_row + visible;
            auto const cached_page = m_cache.find_row(row);
            auto const cached = cached_page.has_value();
            auto const* page = cached ? &cached_page->get() : nullptr;
            if (cached) ++m_cache_hits;
            else ++m_cache_misses;
            auto const top = static_cast<float>(header_height + visible * m_row_height - m_viewport.leading_row_offset);
            auto const bottom = top + static_cast<float>(m_row_height);
            m_d2d_context->DrawLine({ 0.0f, bottom }, { static_cast<float>(m_width), bottom }, m_line_brush.Get(), 1.0f);
            for (auto const& layout : visible_columns)
            {
                auto const left = layout.left;
                auto const right = layout.right;
                velocity_grid::cell_format format{};
                auto cell_index = std::size_t{};
                if (page)
                {
                    cell_index = static_cast<std::size_t>((row - page->start_row) * page->column_count + layout.index);
                    if (cell_index < page->formats.size()) format = page->formats[cell_index];
                }
                if (format.background != 0 && m_visual_theme != 2)
                {
                    auto colour = palette_colour(format.background, D2D1::ColorF(0.96f, 0.97f, 0.98f));
                    m_cell_format_brush->SetColor(colour);
                    m_d2d_context->FillRectangle({ left, top, right, bottom }, m_cell_format_brush.Get());
                }
                if (row == m_selected_row && static_cast<std::int32_t>(layout.index) == m_selected_column)
                    m_d2d_context->FillRectangle({ left, top, right, bottom }, m_selection_brush.Get());
                m_d2d_context->DrawLine({ left, top }, { left, bottom }, m_line_brush.Get(), 1.0f);
                std::wstring value;
                if (page && !page->values.empty())
                {
                    value = cell_index < page->values.size() ? page->values[cell_index] : L"";
                }
                else
                {
                    value = cached ? std::format(L"R{}  C{}", row, layout.index + 1) : std::format(L"Loading row {}", row);
                }
                auto const selected = row == m_selected_row && static_cast<std::int32_t>(layout.index) == m_selected_column;
                m_cell_format_brush->SetColor(m_visual_theme == 2
                    ? D2D1::ColorF(selected ? D2D1::ColorF::Black : D2D1::ColorF::White)
                    : palette_colour(format.foreground, m_text_brush->GetColor()));
                auto text_left = left + 7.0f;
                if (format.icon != 0)
                {
                    // Escapes keep these built-in glyphs independent of the compiler's source-file encoding.
                    static constexpr std::array<std::wstring_view, 29> symbols{
                        L"", L"\x2191", L"\x2193", L"\x2190", L"\x2192",
                        L"\x25B2", L"\x25BC", L"\x2713", L"\x2715", L"\x26A0",
                        L"\x24D8", L"\x2605", L"\x25CF", L"\x25A0", L"\x25C6",
                        L"+", L"\x2212", L"\x25B6", L"\x2016", L"\x25A0",
                        L"\x25F7", L"\x2691", L"\x2665", L"\x26A1", L"\U0001F514",
                        L"\U0001F512", L"\U0001F513", L"\U0001F50D", L"\x270E" };
                    auto const icon = format.icon < symbols.size() ? symbols[format.icon] : symbols[0];
                    D2D1_RECT_F const icon_bounds{ text_left, top + 4.0f, text_left + 16.0f, bottom };
                    m_d2d_context->DrawText(icon.data(), static_cast<UINT32>(icon.size()), m_text_formats[0].Get(),
                        icon_bounds, m_cell_format_brush.Get());
                    text_left += 18.0f;
                }
                D2D1_RECT_F const bounds{ text_left, top + 4.0f, right - 7.0f, bottom };
                m_d2d_context->DrawText(value.c_str(), static_cast<UINT32>(value.size()), layout.text_format,
                    bounds, m_cell_format_brush.Get());
                if (m_has_focus && row == m_selected_row && static_cast<std::int32_t>(layout.index) == m_selected_column)
                    m_d2d_context->DrawRectangle({ left + 1.0f, top + 1.0f, right - 1.0f, bottom - 1.0f }, m_focus_brush.Get(), 2.0f);
            }
        }

        auto const result = m_d2d_context->EndDraw();
        if (result == D2DERR_RECREATE_TARGET)
        {
            create_size_dependent_resources(m_width, m_surface_height);
            return;
        }
        check_hresult(result);
        auto const present_result = m_swap_chain->Present(1, 0);
        if (present_result == DXGI_ERROR_DEVICE_REMOVED || present_result == DXGI_ERROR_DEVICE_RESET)
        {
            recover_device_resources();
            return;
        }
        check_hresult(present_result);
        ++m_frame_count;
        ++m_fps_frame_count;
        if (m_update_render_pending)
        {
            m_update_render_pending = false;
            ++m_update_render_count;
            m_last_update_latency_microseconds = static_cast<std::uint64_t>(
                std::chrono::duration_cast<std::chrono::microseconds>(
                    std::chrono::steady_clock::now() - m_oldest_update).count());
        }
    }

    void VelocityGrid::request_render()
    {
        // A one-shot timer collapses page completions and update batches arriving in
        // the same display interval into one synchronized swap-chain presentation.
        if (m_render_pending || m_shutdown) return;
        m_render_pending = true;
        m_render_timer.Start();
    }

    void VelocityGrid::update_scrollbars()
    {
        auto const maximum = velocity_grid::clamp_scroll_offset(m_row_count, m_row_height, m_height, DBL_MAX);
        m_scrollbar.Maximum(maximum);
        m_scrollbar.StepFrequency(m_row_height * 3.0);
        m_scroll_offset = (std::min)(m_scroll_offset, maximum);
        m_scrollbar.Value(m_scroll_offset);

        auto const total_width = std::accumulate(m_columns.begin(), m_columns.end(), 0.0,
            [](double const total, column_definition const& column) { return total + column.width; });
        auto const horizontal_maximum = (std::max)(0.0, total_width - m_width);
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

        // Bias the bounded prefetch window toward motion while retaining one page
        // behind for small reversals. Changing these constants requires measurement.
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
            // Erase before invoking managed cancellation: an event handler may complete
            // synchronously/re-enter and must not observe the request as still active.
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
                // The event is deliberately page-grained; no cell lookup ever calls managed code.
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
        if (changed) request_render();
    }

    void VelocityGrid::on_size_changed(IInspectable const&, SizeChangedEventArgs const& args)
    {
        m_width = (std::max)(0.0, args.NewSize().Width - scrollbar_width);
        m_surface_height = (std::max)(0.0, args.NewSize().Height - diagnostics_height - scrollbar_height);
        m_height = (std::max)(0.0, m_surface_height - header_height);
        create_size_dependent_resources(m_width, m_surface_height);
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

    std::int32_t VelocityGrid::column_at(double const x) const noexcept
    {
        auto boundary = 0.0;
        for (std::size_t index = 0; index < m_columns.size(); ++index)
        {
            boundary += m_columns[index].width;
            if (x < boundary) return static_cast<std::int32_t>(index);
        }
        return -1;
    }

    void VelocityGrid::select_cell(std::int64_t const row, std::int32_t const column)
    {
        if (m_row_count <= 0 || m_columns.empty()) return;
        auto const selected_row = (std::clamp<std::int64_t>)(row, 0, m_row_count - 1);
        auto const selected_column = (std::clamp<std::int32_t>)(column, 0, static_cast<std::int32_t>(m_columns.size()) - 1);
        if (selected_row == m_selected_row && selected_column == m_selected_column) return;
        m_selected_row = selected_row;
        m_selected_column = selected_column;
        m_selection_changed(m_selected_row, m_selected_column);
        render();
    }

    void VelocityGrid::ensure_selection_visible()
    {
        if (m_selected_row < 0 || m_selected_column < 0) return;
        auto const row_top = m_selected_row * m_row_height;
        auto const row_bottom = row_top + m_row_height;
        if (row_top < m_scroll_offset) m_scroll_offset = row_top;
        else if (row_bottom > m_scroll_offset + m_height) m_scroll_offset = row_bottom - m_height;
        m_scroll_offset = velocity_grid::clamp_scroll_offset(m_row_count, m_row_height, m_height, m_scroll_offset);
        m_scrollbar.Value(m_scroll_offset);

        auto column_left = 0.0;
        for (std::int32_t index = 0; index < m_selected_column; ++index) column_left += m_columns[index].width;
        auto const column_right = column_left + m_columns[m_selected_column].width;
        if (column_left < m_horizontal_offset) m_horizontal_offset = column_left;
        else if (column_right > m_horizontal_offset + m_width) m_horizontal_offset = column_right - m_width;
        m_horizontal_scrollbar.Value(m_horizontal_offset);
        update_viewport();
    }

    void VelocityGrid::on_pointer_pressed(IInspectable const&, Input::PointerRoutedEventArgs const& args)
    {
        auto const point = args.GetCurrentPoint(m_surface);
        auto const position = point.Position();
        if (position.Y < header_height || !point.Properties().IsLeftButtonPressed()) return;
        auto const row = m_viewport.first_row + static_cast<std::int64_t>(
            std::floor((position.Y - header_height + m_viewport.leading_row_offset) / m_row_height));
        auto const column = column_at(position.X + m_horizontal_offset);
        if (row >= 0 && row < m_row_count && column >= 0)
        {
            m_has_focus = true;
            m_scrollbar.Focus(FocusState::Pointer);
            select_cell(row, column);
            args.Handled(true);
        }
    }

    void VelocityGrid::on_key_down(IInspectable const&, Input::KeyRoutedEventArgs const& args)
    {
        if (!m_has_focus || m_row_count <= 0 || m_columns.empty()) return;
        auto command = -1;
        switch (args.Key())
        {
        case Windows::System::VirtualKey::Up: command = 0; break;
        case Windows::System::VirtualKey::Down: command = 1; break;
        case Windows::System::VirtualKey::Left: command = 2; break;
        case Windows::System::VirtualKey::Right: command = 3; break;
        case Windows::System::VirtualKey::Home: command = 4; break;
        case Windows::System::VirtualKey::End: command = 5; break;
        case Windows::System::VirtualKey::PageUp: command = 6; break;
        case Windows::System::VirtualKey::PageDown: command = 7; break;
        }
        if (command < 0) return;
        NavigateSelection(command);
        args.Handled(true);
    }

    void VelocityGrid::on_tick(IInspectable const&, IInspectable const&)
    {
        process_completions();
        auto const now = std::chrono::steady_clock::now();
        auto const elapsed = std::chrono::duration<double>(now - m_fps_epoch).count();
        auto const fps = elapsed > 0.0 ? m_fps_frame_count / elapsed : 0.0;
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
            m_fps_frame_count = 0;
            m_fps_epoch = now;
        }
    }

    void VelocityGrid::on_render_tick(IInspectable const&, IInspectable const&)
    {
        m_render_timer.Stop();
        if (!m_render_pending) return;
        m_render_pending = false;
        render();
    }
}
