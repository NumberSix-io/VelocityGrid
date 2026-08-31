#pragma once

#include <cstdint>

namespace velocity_grid
{
    struct viewport_range
    {
        std::int64_t first_row{};
        std::int64_t last_row{};
        std::int32_t visible_row_count{};
        double leading_row_offset{};
        bool has_partial_first_row{};
        bool has_partial_last_row{};
    };

    [[nodiscard]] __declspec(dllexport) viewport_range calculate_viewport(
        std::int64_t row_count,
        double row_height,
        double viewport_height,
        double scroll_offset) noexcept;

    [[nodiscard]] __declspec(dllexport) double clamp_scroll_offset(
        std::int64_t row_count,
        double row_height,
        double viewport_height,
        double requested_offset) noexcept;
}
