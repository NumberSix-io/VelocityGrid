# Native data-virtualization pipeline

The Phase 3 implementation separates the visible viewport from data availability. The renderer never waits for data: it draws cached rows immediately and a lightweight loading value for cache misses.

## Request flow

1. The viewport maps visible rows to 128-row page boundaries.
2. Directional prefetch requests one page behind and two ahead while scrolling down, reversing those budgets while scrolling up.
3. For the native synthetic path, `RequestScheduler` simulates latency on worker threads. For normal C# use, native code emits one `PageRequested` event and the managed adapter calls the provider asynchronously.
4. Moving to a new anchor page advances the generation and cancels obsolete work.
5. The UI-thread timer drains completions. Canceled, stale-generation, and no-longer-wanted results are rejected before they can enter the cache.
6. Accepted pages enter an eight-page LRU cache and request a coalesced redraw.

The small fixed page and cache values are initial policy constants intended for measurement, not public API defaults.

## Ownership and threading

`RequestScheduler` workers own only shared scheduler state and cancellation flags; they do not access XAML, renderer, or `VelocityGrid` instances. Completions are transferred through a mutex-protected queue and applied on the UI thread. Scheduler destruction marks the shared state as stopping and cancels every outstanding request, so detached workers cannot call into a destroyed control.

`PageCache` is UI-thread-owned. Its capacity is bounded and LRU touches occur during cached row lookup. Ordinary scrolling therefore cannot make resident row state grow with the logical dataset size.

## Diagnostics

The on-grid overlay reports the visible range, cache occupancy and hit ratio, total requests, canceled requests, rejected stale results, and approximate FPS. These counters are deliberately visible in the basic sample so rapid-scroll behavior can be inspected without a profiler.

## Managed boundary

`IVelocityGridDataProvider` returns display-ready values and optional compact formats in a coarse-grained page. The adapter flattens page fields into arrays for one completion call. Strings/formats are copied into native page storage; cached scrolling, hit testing, selection, and rendering then require no provider callback.

The page size (128 rows), prefetch budget, and cache capacity (eight pages) are currently internal policies rather than configurable public properties. Column count comes from the active column configuration and is included in every provider fetch context.
