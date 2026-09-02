#pragma once

#include "Data/Page.h"

#include <cstddef>
#include <cstdint>
#include <functional>
#include <list>
#include <optional>
#include <unordered_map>

namespace velocity_grid
{
    // UI-thread-owned bounded LRU. Pages are the eviction unit so memory does not
    // grow with the logical dataset and render-time lookup never crosses the ABI.
    class page_cache
    {
    public:
        explicit page_cache(std::size_t capacity_pages);

        void insert(page value);
        [[nodiscard]] std::optional<std::reference_wrapper<page const>> find_row(std::int64_t row);
        [[nodiscard]] bool update_cell(std::int64_t row, std::int32_t column, std::wstring value,
            cell_format format);
        [[nodiscard]] bool contains_page(std::int64_t start_row) const noexcept;
        void erase_page(std::int64_t start_row) noexcept;
        void erase_after(std::int64_t row_count) noexcept;
        void clear() noexcept;
        [[nodiscard]] std::size_t size() const noexcept;
        [[nodiscard]] std::size_t capacity() const noexcept;

    private:
        struct entry
        {
            page value;
            std::list<std::int64_t>::iterator recency;
        };

        void touch(std::unordered_map<std::int64_t, entry>::iterator item);

        std::size_t m_capacity;
        std::list<std::int64_t> m_recency;
        std::unordered_map<std::int64_t, entry> m_pages;
    };
}
