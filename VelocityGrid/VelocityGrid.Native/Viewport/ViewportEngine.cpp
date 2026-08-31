#include "pch.h"
#include "ViewportEngine.h"

#include <algorithm>
#include <cmath>
#include <limits>

namespace velocity_grid
{
    double clamp_scroll_offset(
        std::int64_t const row_count,
        double const row_height,
        double const viewport_height,
        double const requested_offset) noexcept
    {
        if (row_count <= 0 || row_height <= 0.0 || !std::isfinite(row_height) || viewport_height <= 0.0)
        {
            return 0.0;
        }

        auto const extent = static_cast<long double>(row_count) * row_height;
        auto const maximum = (std::max)(0.0L, extent - (std::max)(0.0, viewport_height));
        auto const safe_maximum = static_cast<double>((std::min)(
            maximum,
            static_cast<long double>((std::numeric_limits<double>::max)())));

        if (!std::isfinite(requested_offset))
        {
            return requested_offset > 0.0 ? safe_maximum : 0.0;
        }

        return (std::clamp)(requested_offset, 0.0, safe_maximum);
    }

    viewport_range calculate_viewport(
        std::int64_t const row_count,
        double const row_height,
        double const viewport_height,
        double const scroll_offset) noexcept
    {
        viewport_range result{};
        if (row_count <= 0 || row_height <= 0.0 || viewport_height <= 0.0 || !std::isfinite(row_height))
        {
            result.last_row = -1;
            return result;
        }

        auto const offset = clamp_scroll_offset(row_count, row_height, viewport_height, scroll_offset);
        result.first_row = (std::min)(
            static_cast<std::int64_t>(std::floor(offset / row_height)), row_count - 1);
        result.leading_row_offset = std::fmod(offset, row_height);
        result.has_partial_first_row = result.leading_row_offset > 0.0001;

        auto const rows_intersecting = static_cast<std::int64_t>(
            std::ceil((result.leading_row_offset + viewport_height) / row_height));
        result.last_row = (std::min)(row_count - 1, result.first_row + (std::max<std::int64_t>)(0, rows_intersecting - 1));
        result.visible_row_count = static_cast<std::int32_t>(result.last_row - result.first_row + 1);

        auto const trailing_edge = result.leading_row_offset + viewport_height;
        result.has_partial_last_row = std::fmod(trailing_edge, row_height) > 0.0001 && result.last_row < row_count;
        return result;
    }
}
