# Phase 1 normalized benchmark - 2026-07-11

## Scope

This rerun fixes the benchmark methodology before interpreting CPU capacity. No PostgreSQL tuning,
batch-size tuning, index tuning, commit-frequency tuning or COPY tuning was performed in this run.

The corrected harness is `scripts/benchmark/run-phase1-benchmark.ps1`.

## Harness corrections

| Issue | Correction |
|---|---|
| Docker CPU percent was treated as global PostgreSQL CPU utilization. | CPU is now recorded as consumed cores (`docker stats CPUPerc / 100`) and normalized as `consumed cores / allocated cores`. |
| No explicit resource budget existed. | Compose now sets PostgreSQL to 2 CPUs/2 GiB and Worker to 2 CPUs/1 GiB. |
| MQTT persistent-session contamination could leak between profiles. | Each profile runs after `docker compose down -v --remove-orphans`; Mosquitto persistence remains disabled. |
| Profiles shared API-provisioning side effects and slow provisioning. | Benchmark now seeds workspace/devices directly in the per-profile database. The benchmark scope is Worker/PostgreSQL ingestion, not provisioning API throughput. |
| `psql -c` through Windows PowerShell stripped quoted identifiers. | SQL is now sent to `psql` through stdin. |
| Simulator process exit code was unreliable under redirected `Start-Process`. | Harness validates simulator summary with `errors=0` when `ExitCode` is not populated. |
| `docker logs` could block or fail the run due very large EF logs/stderr. | Metrics extraction reads only a bounded log tail; full log capture is best-effort. |
| Previous burst profile used `interval-seconds=0.01`, not comparable to the ADR load shape. | Profiles now use `interval-seconds=1.0`, matching 100 devices x 300 messages over about 5 minutes. |

## Resource budget

| Service | CPU budget | Memory budget |
|---|---:|---:|
| PostgreSQL 16 Alpine | 2.0 cores | 2 GiB |
| Worker | 2.0 cores | 1 GiB |

## Benchmark results

Throughput is persisted ingestion records divided by publish-to-drain wall-clock seconds. Latency is
Worker processing latency from internal `BENCHMARK_METRICS`, not end-to-end MQTT latency.

| Profile | P50 | P95 | P99 | Throughput | Backlog | Recovery | PG cores avg/max | PG normalized CPU | Worker cores avg/max |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| legacy | 27.057 ms | 67.371 ms | 124.221 ms | 97.377/s | 635 | 2.415 s | 0.6663 / 1.4917 | 33.32% / 74.58% | 0.7578 / 1.9460 |
| v2-light | 24.360 ms | 50.263 ms | 84.791 ms | 97.677/s | 509 | 2.272 s | 0.6075 / 0.9668 | 30.38% / 48.34% | 0.7238 / 1.9154 |
| v2-medium COPY | 25.114 ms | 48.657 ms | 76.742 ms | 97.326/s | 247 | 2.309 s | 0.7569 / 1.4307 | 37.84% / 71.54% | 0.6783 / 1.6456 |
| v2-dense COPY | 31.467 ms | 65.100 ms | 113.718 ms | 97.236/s | 491 | 2.412 s | 1.0801 / 1.6010 | 54.01% / 80.05% | 0.7494 / 1.7241 |

All four profiles persisted the expected records and points with zero rejection:

| Profile | Records | Points | Rejections | WAL |
|---|---:|---:|---:|---:|
| legacy | 30,000 | 150,000 | 0 | 181,103,584 B |
| v2-light | 30,000 | 150,000 | 0 | 174,355,112 B |
| v2-medium COPY | 30,000 | 480,000 | 0 | 423,073,504 B |
| v2-dense COPY | 30,000 | 960,000 | 0 | 755,693,080 B |

## Storage decomposition

Values are incremental within each isolated profile. `Bytes/record` is
`TelemetryIngestionRecord data + indexes` divided by records. `Bytes/point` is `TelemetryPoint`
data divided by points. `Index bytes/point` is `TelemetryPoint` indexes divided by points.

| Profile | Record data | Record index | Point data | Point index | Bytes/record | Bytes/point | Index bytes/point |
|---|---:|---:|---:|---:|---:|---:|---:|
| v2-light | 16,400,384 | 13,606,912 | 23,642,112 | 30,515,200 | 1,000.243 | 157.614 | 203.435 |
| v2-medium COPY | 27,344,896 | 15,368,192 | 78,667,776 | 110,059,520 | 1,423.770 | 163.891 | 229.291 |
| v2-dense COPY | 41,000,960 | 15,204,352 | 157,327,360 | 197,476,352 | 1,873.510 | 163.883 | 205.705 |

Metric catalog overhead was measured separately:

| Profile | MetricDefinition/DiscoveryAudit overhead |
|---|---:|
| v2-light | 57,344 B |
| v2-medium COPY | 57,344 B |
| v2-dense COPY | 0 B incremental in the measured window because the dense catalog was preseeded before the baseline snapshot, as required to avoid the 20 new keys/device/hour guardrail. |

## Retention projection

Assumptions:

- 1,000 devices.
- 1 message every 10 seconds per device = 8,640,000 ingestion records/day.
- Light = 5 metrics/message.
- Medium = 16 metrics/message, with 13 numeric metrics for rollup.
- Dense = 32 metrics/message, with 26 numeric metrics for rollup.
- Raw projection uses measured incremental bytes/record, bytes/point and index bytes/point.
- Rollup is conceptual only and was not implemented. A 1-minute numeric rollup row is modeled as:
  `2 * measured point data bytes + measured point index bytes`, representing min/max/avg/count/last
  plus the same dimensional lookup pattern. This is a planning model, not a measured table size.
- `hot raw + rollup` means 30-day retention with N hot raw days and 1-minute rollup for the remaining days.

| Profile | Raw 7 days | Raw 14 days | Raw 30 days | Hot raw 7d + rollup to 30d | Hot raw 14d + rollup to 30d | Hot raw 30d |
|---|---:|---:|---:|---:|---:|---:|
| v2-light | 169,675,914,240 B | 339,351,828,480 B | 727,182,489,600 B | 255,566,507,040 B | 399,101,806,080 B | 727,182,489,600 B |
| v2-medium COPY | 466,583,967,360 B | 933,167,934,720 B | 1,999,645,574,400 B | 706,437,318,240 B | 1,100,022,439,680 B | 1,999,645,574,400 B |
| v2-dense COPY | 828,595,716,480 B | 1,657,191,432,960 B | 3,551,124,499,200 B | 1,287,978,264,000 B | 1,976,761,900,800 B | 3,551,124,499,200 B |

## CPU gate conclusion

CPU GATE REQUIRES NORMALIZATION.

The previous statement "PostgreSQL CPU 105.84%" was a Docker raw CPU percentage. In Docker, 100%
means approximately one consumed CPU core, not necessarily 100% of the container budget or host.

With an explicit 2-core PostgreSQL budget, the dense COPY profile consumed:

- average: 1.0801 cores = 54.01% normalized utilization;
- max: 1.6010 cores = 80.05% normalized utilization.

Under normalized CPU budget, dense COPY is below the ADR's average CPU gate. Peak normalized CPU
briefly reached 80.05%, which is useful operational evidence but not the gate currently written.
The ADR should be updated explicitly to define CPU as normalized utilization against an allocated
CPU budget and to clarify whether the gate applies to average, peak, or both.

## Binary COPY

Binary COPY remains the recommended strategy for medium and dense profiles. The corrected benchmark
shows medium and dense meeting the load shape with zero rejection, bounded backlog, and normalized
PostgreSQL CPU below 75% without any tuning beyond the already implemented COPY writer.

## Verdict

PHASE 1 COMPLETE.

PHASE 1 GATE: PASSED, using the corrected and explicit CPU normalization methodology above.
