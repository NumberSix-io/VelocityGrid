#pragma once

#include "Data/Page.h"

#include <atomic>
#include <cstdint>
#include <memory>
#include <mutex>
#include <unordered_map>
#include <unordered_set>
#include <vector>

namespace velocity_grid
{
    // Completion values contain no reference to the control or XAML objects; detached
    // workers communicate only through shared_state and are safe during teardown.
    struct completed_request
    {
        std::uint64_t request_id{};
        std::uint64_t generation{};
        page value{};
        bool canceled{};
    };

    struct scheduler_metrics
    {
        std::uint64_t requested{};
        std::uint64_t completed{};
        std::uint64_t canceled{};
        std::uint64_t stale{};
    };

    // Synthetic asynchronous source used to exercise cancellation/generation policy.
    // Managed applications normally use IVelocityGridDataProvider instead.
    class request_scheduler
    {
    public:
        request_scheduler();
        ~request_scheduler();
        request_scheduler(request_scheduler const&) = delete;
        request_scheduler& operator=(request_scheduler const&) = delete;

        void request(std::int64_t start_row, std::int32_t row_count, std::uint64_t generation);
        void cancel_obsolete(std::uint64_t generation, std::unordered_set<std::int64_t> const& wanted_pages);
        [[nodiscard]] std::vector<completed_request> drain_completions();
        void record_stale() noexcept;
        [[nodiscard]] scheduler_metrics metrics() const noexcept;

    private:
        struct request_state
        {
            std::uint64_t id{};
            std::uint64_t generation{};
            std::shared_ptr<std::atomic_bool> canceled;
        };

        struct shared_state
        {
            mutable std::mutex mutex;
            std::unordered_map<std::int64_t, request_state> outstanding;
            std::vector<completed_request> completions;
            scheduler_metrics metrics;
            std::atomic_bool stopping{};
            std::atomic_uint64_t next_id{ 1 };
        };

        std::shared_ptr<shared_state> m_state;
    };
}
