#include "pch.h"
#include "RequestScheduler.h"

#include <algorithm>
#include <chrono>
#include <thread>

namespace velocity_grid
{
    request_scheduler::request_scheduler() : m_state(std::make_shared<shared_state>()) {}

    request_scheduler::~request_scheduler()
    {
        m_state->stopping = true;
        std::scoped_lock lock(m_state->mutex);
        for (auto const& [_, request] : m_state->outstanding)
        {
            *request.canceled = true;
        }
    }

    void request_scheduler::request(
        std::int64_t const start_row,
        std::int32_t const row_count,
        std::uint64_t const generation)
    {
        auto cancellation = std::make_shared<std::atomic_bool>(false);
        auto const id = m_state->next_id.fetch_add(1);
        {
            std::scoped_lock lock(m_state->mutex);
            auto active = m_state->outstanding.find(start_row);
            if (active != m_state->outstanding.end() && !*active->second.canceled) return;
            if (active == m_state->outstanding.end())
            {
                m_state->outstanding.emplace(start_row, request_state{ id, generation, cancellation });
            }
            else
            {
                active->second = request_state{ id, generation, cancellation };
            }
            ++m_state->metrics.requested;
        }

        auto state = m_state;
        std::thread([state, cancellation, id, generation, start_row, row_count]
        {
            auto const delay = 50 + static_cast<int>((start_row / (std::max)(1, row_count)) % 101);
            for (int elapsed = 0; elapsed < delay && !*cancellation && !state->stopping; elapsed += 5)
            {
                std::this_thread::sleep_for(std::chrono::milliseconds(5));
            }

            completed_request completion{ id, generation, { start_row, row_count }, *cancellation || state->stopping };
            std::scoped_lock lock(state->mutex);
            if (auto active = state->outstanding.find(start_row);
                active != state->outstanding.end() && active->second.id == id)
            {
                state->outstanding.erase(active);
            }
            if (completion.canceled) ++state->metrics.canceled;
            else ++state->metrics.completed;
            state->completions.push_back(completion);
        }).detach();
    }

    void request_scheduler::cancel_obsolete(
        std::uint64_t const generation,
        std::unordered_set<std::int64_t> const& wanted_pages)
    {
        std::scoped_lock lock(m_state->mutex);
        for (auto const& [start, request] : m_state->outstanding)
        {
            if (request.generation != generation || !wanted_pages.contains(start))
            {
                *request.canceled = true;
            }
        }
    }

    std::vector<completed_request> request_scheduler::drain_completions()
    {
        std::scoped_lock lock(m_state->mutex);
        std::vector<completed_request> result;
        result.swap(m_state->completions);
        return result;
    }

    void request_scheduler::record_stale() noexcept
    {
        std::scoped_lock lock(m_state->mutex);
        ++m_state->metrics.stale;
    }

    scheduler_metrics request_scheduler::metrics() const noexcept
    {
        std::scoped_lock lock(m_state->mutex);
        return m_state->metrics;
    }
}
