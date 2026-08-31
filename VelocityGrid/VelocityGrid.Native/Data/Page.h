#pragma once

#include <cstdint>
#include <string>
#include <vector>

namespace velocity_grid
{
    struct page
    {
        std::int64_t start_row{};
        std::int32_t row_count{};
        std::vector<std::wstring> values;

        [[nodiscard]] bool contains(std::int64_t const row) const noexcept
        {
            return row >= start_row && row < start_row + row_count;
        }
    };
}
