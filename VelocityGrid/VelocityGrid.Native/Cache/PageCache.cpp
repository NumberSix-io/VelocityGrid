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

    bool page_cache::contains_page(std::int64_t const start_row) const noexcept
    {
        return m_pages.contains(start_row);
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
