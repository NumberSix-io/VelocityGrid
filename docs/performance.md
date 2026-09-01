# VelocityGrid performance baseline

Phase 6 uses the benchmark runner in `VelocityGrid.Sample.Basic`. It exercises the real WinRT, viewport, cache, provider, DirectWrite, Direct2D, and swap-chain paths. Results are machine-specific and must identify the build and hardware used.

## Reproduce the baseline

1. Build `VelocityGrid.slnx` as `Release | x64`.
2. Deploy and start `VelocityGrid.Sample.Basic` without the Visual Studio debugger attached.
3. Keep the window visible, at its default size, and close unrelated high-load applications.
4. Select **Run Phase 6 benchmarks** and wait for all five result lines.
5. Copy the results together with CPU, GPU, RAM, Windows version, display refresh rate, build commit, and build configuration.
6. Repeat three times after one warm-up run and retain the median result for each scenario.

The scenarios are deterministic:

- **1M sequential:** 240 evenly spaced jumps through one million rows.
- **10M sequential:** 240 evenly spaced jumps through ten million rows.
- **10M random:** 240 random jumps using seed 42.
- **Cache revisit:** eight passes over the same nine page boundaries.
- **GC stress:** 500 managed provider batches of 128 rows (640,000 rows / 6.4 million values).

FPS here means synchronously presented grid frames divided by scenario wall time. Cache percentage counts visible rows found in the native page cache. Working set is a process snapshot, not retained grid memory.

## Results

No baseline is recorded until the Release runner has completed on the target machine.

| Environment | 1M sequential | 10M sequential | 10M random | Cache revisit | GC stress |
|---|---:|---:|---:|---:|---:|
| Pending target-machine run | — | — | — | — | — |

### Synthetic-provider baseline — 2026-08-31

Environment: Intel Core Ultra 9 285HX, Intel Graphics driver 32.0.101.8724, 63.5 GiB RAM, Windows 11 Pro 10.0.26200 (build 26200). The submitted run did not record whether the executable was Debug or Release, so a confirmed Release median remains desirable before treating these numbers as a release-quality threshold.

| Scenario | Time | Presented FPS | Cache | Requests | Working set |
|---|---:|---:|---:|---:|---:|
| 1M sequential | 20,466 ms | 60.0 | 79.3% | 987 | 187.4 MiB |
| 10M sequential | 20,385 ms | 60.1 | 79.3% | 986 | 188.7 MiB |
| 10M random | 20,398 ms | 60.0 | 70.4% | 984 | 189.3 MiB |
| Cache revisit | 2,666 ms | 60.0 | 95.0% | 88 | 189.8 MiB |

GC stress completed in 35 ms with two Gen0 collections and a 16,094.7 KiB managed-memory delta.

The one-million and ten-million sequential results are effectively identical, while working set differs by only 1.3 MiB. This validates that viewport and retained-memory cost are independent of logical row count. Random access reduces the cache hit rate by 8.9 percentage points without materially changing duration or memory.

The dominant issue is redundant presentation after provider completion. Each 240-jump scenario raises roughly 985 page requests. Page completions currently render immediately and `Present(1, 0)` synchronizes each frame to display refresh. The measured 60 FPS ceiling and approximately 20-second duration therefore reflect about 1,200 serialized presentations, not slow row-index calculations. Page-completion rendering should be coalesced to at most one presentation per display interval before streaming updates are added.

### Reproducibility run — 2026-08-31

| Scenario | Time | Presented FPS | Cache | Requests | Working set |
|---|---:|---:|---:|---:|---:|
| 1M sequential | 20,466 ms | 60.0 | 79.3% | 987 | 185.2 MiB |
| 10M sequential | 20,416 ms | 60.1 | 79.3% | 986 | 190.4 MiB |
| 10M random | 20,399 ms | 60.0 | 70.4% | 984 | 186.5 MiB |
| Cache revisit | 2,667 ms | 60.0 | 95.0% | 88 | 187.1 MiB |

GC stress again completed in 35 ms with two Gen0 collections and a 16,094.7 KiB managed-memory delta. Cache percentages and request counts exactly match the first corrected run. Scenario timing differs by no more than 31 ms (0.15%), confirming a stable refresh-synchronized bottleneck. Working-set snapshots vary by up to 3.3 MiB, which is expected for process-level point-in-time measurements.

Post-analysis found that the exposed frame counter shared the diagnostics overlay's one-second reset window. The durations, cache, request, memory, and GC values above remain valid, but their FPS column is retained only as evidence of the observed refresh ceiling. The counter is now cumulative for each benchmark scenario, with a separate rolling counter for diagnostics. Provider-completion rendering is also deferred and coalesced on a 16 ms timer; direct input and scroll rendering remains immediate.

### Post-coalescing baseline — 2026-09-01

| Scenario | Time | Presented FPS | Cache | Requests | Working set |
|---|---:|---:|---:|---:|---:|
| 1M sequential | 3,987 ms | 60.2 | 99.6% | 985 | 177.7 MiB |
| 10M sequential | 3,960 ms | 60.6 | 100.0% | 986 | 177.7 MiB |
| 10M random | 4,000 ms | 60.0 | 100.0% | 984 | 178.0 MiB |
| Cache revisit | 1,199 ms | 60.0 | 100.0% | 88 | 178.3 MiB |

GC stress completed in 32 ms with two Gen0 collections and a 16,094.7 KiB managed-memory delta.

Coalescing reduced the three main scenarios from 20,385–20,466 ms to 3,960–4,000 ms, an improvement of approximately 80.5%, without increasing request count. Cache revisit improved from 2,666–2,667 ms to 1,199 ms (55.0%). Working set fell by roughly 8–12 MiB compared with the prior corrected runs. The cumulative frame counts correspond to approximately 240 presentations per main scenario, confirming that page completions no longer multiply swap-chain presents. Cache rates rise to effectively 100% because completed pages are incorporated into the next coalesced or immediate viewport frame.

This validates the architectural adjustment: viewport cost remains independent of logical row count, cache memory stays bounded, random jumps do not degrade frame throughput, and asynchronous data arrival is now decoupled from presentation frequency.

### Final Release x64 baseline — 2026-09-01

The running executable was verified at the registered Release AppX path before this capture.

| Scenario | Time | Presented FPS | Cache | Requests | Working set |
|---|---:|---:|---:|---:|---:|
| 1M sequential | 3,989 ms | 60.2 | 99.6% | 985 | 178.0 MiB |
| 10M sequential | 3,983 ms | 60.2 | 100.0% | 986 | 182.4 MiB |
| 10M random | 4,000 ms | 60.0 | 100.0% | 984 | 182.7 MiB |
| Cache revisit | 1,199 ms | 60.0 | 100.0% | 88 | 179.1 MiB |

GC stress completed in 42 ms with two Gen0 collections and a 16,094.7 KiB managed-memory delta.

The Release result agrees with the post-coalescing comparison to within 23 ms for every rendering scenario. The ten-million-row cases remain effectively identical to the one-million-row case. This is the accepted Phase 6 performance baseline; future changes should be compared against these values on the same hardware and display configuration.

### Preliminary Debug/latency run — 2026-08-31

The first run used the sample's 100 ms simulated remote provider. It exposed two harness defects and is retained as diagnostic evidence, not the final baseline: `ScrollToRow` presented twice per jump, and the cache revisit delay was too short for pages to arrive.

| Scenario | Time | Presented FPS | Cache | Requests | Working set |
|---|---:|---:|---:|---:|---:|
| 1M sequential | 7,965 ms | 60.1 | 0.0% | 985 | 162.1 MiB |
| 10M sequential | 7,964 ms | 60.3 | 0.0% | 987 | 165.0 MiB |
| 10M random | 7,999 ms | 60.0 | 0.0% | 1,099 | 168.1 MiB |
| Cache revisit | 2,407 ms | 5.0 | 0.0% | 280 | 170.2 MiB |

GC stress completed in 55 ms with two Gen0 collections and a 16,119.2 KiB managed-memory delta. The runner now uses the immediate synthetic provider for repeatable baseline measurements, avoids the duplicate render, and restores the simulated provider afterward.

## ETW / WPA capture

Automated capture was attempted on 2026-09-01 with `GeneralProfile.Verbose.File`, but Windows returned `0xc5585011` while enabling the system-performance profiling policy. WPR and WPA are installed; the capture must be started from a fully elevated administrator terminal on this machine.

Install the Windows Performance Toolkit, open an elevated terminal, and capture a benchmark run:

```powershell
wpr -start GeneralProfile -filemode
# Run the in-app Phase 6 benchmarks.
wpr -stop VelocityGrid.etl
```

Open `VelocityGrid.etl` in Windows Performance Analyzer and inspect:

- CPU Usage (Sampled), grouped by process, module, and stack;
- UI thread time in `VelocityGrid.Native.dll`, DirectWrite, Direct2D, and `Present`;
- DPC/ISR and GPU utilization for scheduling stalls;
- process commit and working set over the run;
- .NET GC pauses, allocation rate, and generation counts;
- request churn during random jumps and cache revisits.

Use Microsoft public symbols and the matching VelocityGrid PDBs. Capture Release builds because debugger and Debug-runtime overhead distort rendering and GC measurements.

## Current hypotheses to validate

- DirectWrite text submission and synchronized swap-chain presentation are expected to dominate continuous rendering.
- Random jumps should increase request cancellation and lower cache hit rate without increasing retained memory with row count.
- Working set should remain broadly similar between the one-million and ten-million scenarios because pages are bounded by the native cache.
- The flat managed page payload is expected to create significant short-lived string allocation pressure; GC stress quantifies whether this needs redesign before streaming updates.
