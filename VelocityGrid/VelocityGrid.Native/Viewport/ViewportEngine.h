#pragma once

#include <cstdint>

namespace velocity_grid
{
    // InteractionTracker positions are single-precision floats. Keeping the
    // tracker inside this fixed-size local window preserves sub-DIP precision
    // while the logical viewport continues to address the full 64-bit dataset.
    inline constexpr double interaction_window_extent = 1'048'576.0;

    // Compact result consumed directly by scrolling, request policy, and drawing.
    struct viewport_range
    {
        std::int64_t first_row{};
        std::int64_t last_row{};
        std::int32_t visible_row_count{};
        double leading_row_offset{};
        bool has_partial_first_row{};
        bool has_partial_last_row{};
    };

    struct interaction_window
    {
        double origin{};
        float position{};
        float maximum{};
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

    // Maps an arbitrary double-precision logical offset into a small floating
    // interaction range. The origin is moved only when the interaction is idle
    // or a non-interaction scroll changes the logical position.
    [[nodiscard]] __declspec(dllexport) interaction_window calculate_interaction_window(
        double maximum_offset,
        double current_offset) noexcept;

    [[nodiscard]] __declspec(dllexport) double logical_offset_from_interaction(
        interaction_window const& window,
        float interaction_position) noexcept;
}
