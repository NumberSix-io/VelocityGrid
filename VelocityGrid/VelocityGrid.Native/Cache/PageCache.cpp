#include "pch.h"
#include "PageCache.h"

#include <algorithm>

namespace velocity_grid
{
    page_cache::page_cache(std::size_t const capacity_pages) :
        m_capacity((std::max<std::size_t>)(1, capacity_pages))
    {
    }

    void page_cache::insert(page value)
    {
        if (auto existing = m_pages.find(value.start_row); existing != m_pages.end())
        {
            existing->second.value = value;
            touch(existing);
            return;
        }

        m_recency.push_front(value.start_row);
        m_pages.emplace(value.start_row, entry{ value, m_recency.begin() });
        while (m_pages.size() > m_capacity)
        {
            auto const victim = m_recency.back();
            m_recency.pop_back();
            m_pages.erase(victim);
        }
    }

    std::optional<std::reference_wrapper<page const>> page_cache::find_row(std::int64_t const row)
    {
        // Capacity is deliberately tiny (currently eight), so this bounded scan is
        // simpler than maintaining a second interval index and remains predictable.
        for (auto item = m_pages.begin(); item != m_pages.end(); ++item)
        {
            if (item->second.value.contains(row))
            {
                touch(item);
                return std::cref(item->second.value);
            }
        }
        return std::nullopt;
    }

    bool page_cache::update_cell(std::int64_t const row, std::int32_t const column, std::wstring value,
        cell_format format)
    {
        if (column < 0) return false;
        for (auto item = m_pages.begin(); item != m_pages.end(); ++item)
        {
            auto& page = item->second.value;
            if (!page.contains(row)) continue;
            if (column >= page.column_count) return false;
            auto const index = static_cast<std::size_t>((row - page.start_row) * page.column_count + column);
            if (index >= page.values.size()) return false;
            page.values[index] = std::move(value);
            if (page.formats.size() < page.values.size()) page.formats.resize(page.values.size());
            page.formats[index] = format;
            touch(item);
            return true;
        }
        return false;
    }

    bool page_cache::contains_page(std::int64_t const start_row) const noexcept
    {
        return m_pages.contains(start_row);
    }

    void page_cache::erase_page(std::int64_t const start_row) noexcept
    {
        auto const item = m_pages.find(start_row);
        if (item == m_pages.end()) return;
        m_recency.erase(item->second.recency);
        m_pages.erase(item);
    }

    void page_cache::erase_after(std::int64_t const row_count) noexcept
    {
        for (auto item = m_pages.begin(); item != m_pages.end();)
        {
            auto const end_row = item->second.value.start_row + item->second.value.row_count;
            if (item->second.value.start_row >= row_count || end_row > row_count)
            {
                m_recency.erase(item->second.recency);
                item = m_pages.erase(item);
            }
            else
            {
                ++item;
            }
        }
    }

    void page_cache::clear() noexcept
    {
        m_pages.clear();
        m_recency.clear();
    }

    std::size_t page_cache::size() const noexcept { return m_pages.size(); }
    std::size_t page_cache::capacity() const noexcept { return m_capacity; }

    void page_cache::touch(std::unordered_map<std::int64_t, entry>::iterator const item)
    {
        m_recency.erase(item->second.recency);
        m_recency.push_front(item->first);
        item->second.recency = m_recency.begin();
    }
}
