#include "pch.h"

#include "CppUnitTest.h"
#include "../VelocityGrid.Native/Viewport/ViewportEngine.h"
#include "../VelocityGrid.Native/Cache/PageCache.h"
#include "../VelocityGrid.Native/Data/RequestScheduler.h"

#include <chrono>
#include <thread>
#include <unordered_set>
using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace VelocityGrid_Native_Tests
{
    TEST_CLASS(CppUnitTests)
    {
    public:
        TEST_METHOD(EmptyDatasetHasNoVisibleRows)
        {
            auto const viewport = velocity_grid::calculate_viewport(0, 24.0, 600.0, 0.0);
            Assert::AreEqual<std::int64_t>(-1, viewport.last_row);
            Assert::AreEqual<std::int32_t>(0, viewport.visible_row_count);
        }

        TEST_METHOD(ViewportCalculatesPartialRows)
        {
            auto const viewport = velocity_grid::calculate_viewport(10'000'000, 24.0, 60.0, 12.0);
            Assert::AreEqual<std::int64_t>(0, viewport.first_row);
            Assert::AreEqual<std::int64_t>(2, viewport.last_row);
            Assert::IsTrue(viewport.has_partial_first_row);
            Assert::IsTrue(viewport.has_partial_last_row);
        }

        TEST_METHOD(RandomJumpIsClampedToDataset)
        {
            auto const maximum = velocity_grid::clamp_scroll_offset(10'000'000, 24.0, 600.0, DBL_MAX);
            auto const viewport = velocity_grid::calculate_viewport(10'000'000, 24.0, 600.0, maximum);
            Assert::AreEqual<std::int64_t>(9'999'999, viewport.last_row);
        }

        TEST_METHOD(PageCacheRemainsBoundedAndEvictsLeastRecentlyUsed)
        {
            velocity_grid::page_cache cache(2);
            cache.insert({ 0, 128 });
            cache.insert({ 128, 128 });
            Assert::IsTrue(cache.find_row(1).has_value());
            cache.insert({ 256, 128 });

            Assert::AreEqual<std::size_t>(2, cache.size());
            Assert::IsTrue(cache.contains_page(0));
            Assert::IsFalse(cache.contains_page(128));
            Assert::IsTrue(cache.contains_page(256));
        }

        TEST_METHOD(PageCacheUpdatesCachedCellsInPlace)
        {
            velocity_grid::page page{ 128, 2, 10 };
            page.values.resize(20, L"old");
            velocity_grid::page_cache cache(2);
            cache.insert(std::move(page));

            Assert::IsTrue(cache.update_cell(129, 4, L"101.2500", { 1, 2, 1 }));
            Assert::IsFalse(cache.update_cell(500, 4, L"not cached", {}));
            auto const cached = cache.find_row(129);
            Assert::IsTrue(cached.has_value());
            Assert::AreEqual(L"101.2500", cached->get().values[14].c_str());
            Assert::AreEqual<std::uint8_t>(1, cached->get().formats[14].foreground);
        }

        TEST_METHOD(SchedulerPreservesGenerationOnCompletion)
        {
            velocity_grid::request_scheduler scheduler;
            scheduler.request(0, 128, 42);
            std::vector<velocity_grid::completed_request> completions;
            for (int attempt = 0; attempt < 50 && completions.empty(); ++attempt)
            {
                std::this_thread::sleep_for(std::chrono::milliseconds(5));
                completions = scheduler.drain_completions();
            }

            Assert::AreEqual<std::size_t>(1, completions.size());
            Assert::AreEqual<std::uint64_t>(42, completions.front().generation);
            Assert::IsFalse(completions.front().canceled);
        }

        TEST_METHOD(SchedulerCancelsObsoleteRequests)
        {
            velocity_grid::request_scheduler scheduler;
            scheduler.request(12'800, 128, 1);
            scheduler.cancel_obsolete(2, std::unordered_set<std::int64_t>{});
            std::vector<velocity_grid::completed_request> completions;
            for (int attempt = 0; attempt < 50 && completions.empty(); ++attempt)
            {
                std::this_thread::sleep_for(std::chrono::milliseconds(5));
                completions = scheduler.drain_completions();
            }

            Assert::AreEqual<std::size_t>(1, completions.size());
            Assert::IsTrue(completions.front().canceled);
            Assert::AreEqual<std::uint64_t>(1, scheduler.metrics().canceled);
        }
    };
}
