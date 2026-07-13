# Benchmark Fase 2 — Query API — 2026-07-12

- Workspace: `22222222-2222-2222-2222-222222222222`
- TelemetryPoint: **5115083**
- Partições com dados: **5**
- Maior série: **500000** pontos
- Índices novos antes da medição: **nenhum**

## Resumo executivo

Os cenários Q1–Q7 foram executados no volume e nas partições exigidos pelo ADR-0003. Todos
atenderam aos gates estabelecidos. Q6, o pior caso agregado, executou em 1624,705 ms, abaixo do
limite de 2 segundos. Os Parallel Seq Scans de Q6 são coerentes com a alta proporção de dados
relevantes: aproximadamente 4,28 milhões das 5,11 milhões de linhas.

| Query | Cenário | Execution Time | Plano dominante | Gate |
|---|---|---:|---|---|
| Q1 | 1 device × 1 métrica, raw 24h | 0,448 ms | Index Scan | Passou |
| Q2 | 5 devices × 1 métrica, raw 24h | 2,392 ms | Index Scan + Sort | Passou |
| Q3 | 1 device × 5 métricas, raw 24h | 3,554 ms | Index Scan + Sort | Passou |
| Q4 | 10 devices × 10 métricas, raw 24h | 59,641 ms | Parallel Bitmap Heap Scan | Passou |
| Q5 | 1 device × 1 métrica, agregado 1h/30d | 13,081 ms | Index/Bitmap Scan + GroupAggregate | Passou |
| Q6 | 10 devices × 10 métricas, agregado 6h/90d | 1624,705 ms | Parallel Seq Scan + HashAggregate | Passou (< 2 s) |
| Q7 | 5 devices × 5 métricas, last 15m/7d | 293,031 ms | Bitmap Heap Scan + Sort/Unique | Passou |

## Q1

```sql
Limit  (cost=2.27..1234.73 rows=668 width=627) (actual time=0.091..0.311 rows=374 loops=1)
  Buffers: shared hit=19
  ->  Append  (cost=2.27..1234.73 rows=668 width=627) (actual time=0.090..0.285 rows=374 loops=1)
        Buffers: shared hit=19
        Subplans Removed: 4
        ->  Index Scan using "telemetry_points_2026_07_WorkspaceId_DeviceId_MetricDefinit_idx" on telemetry_points_2026_07 telemetry_points_1  (cost=0.55..1197.56 rows=664 width=627) (actual time=0.089..0.256 rows=374 loops=1)
              Index Cond: (("WorkspaceId" = '22222222-2222-2222-2222-222222222222'::uuid) AND (("DeviceId")::text = 'benchmark-device-01'::text) AND ("MetricDefinitionId" = '2694f2a2-22dd-5fbe-be97-d55a97cb350a'::uuid) AND ("OccurredAtUtc" >= ('2026-07-12 15:10:58.990082+00'::timestamp with time zone - '24:00:00'::interval)) AND ("OccurredAtUtc" <= '2026-07-12 15:10:58.990082+00'::timestamp with time zone))
              Buffers: shared hit=19
Planning:
  Buffers: shared hit=709
Planning Time: 12.953 ms
Execution Time: 0.448 ms
```

## Q2

```sql
Limit  (cost=5544.67..5553.00 rows=3334 width=627) (actual time=1.794..2.082 rows=1870 loops=1)
  Buffers: shared hit=99
  ->  Sort  (cost=5544.67..5553.00 rows=3334 width=627) (actual time=1.792..1.946 rows=1870 loops=1)
        Sort Key: telemetry_points."OccurredAtUtc"
        Sort Method: quicksort  Memory: 531kB
        Buffers: shared hit=99
        ->  Append  (cost=0.43..5349.58 rows=3334 width=627) (actual time=0.069..0.924 rows=1870 loops=1)
              Buffers: shared hit=96
              Subplans Removed: 4
              ->  Index Scan using "telemetry_points_2026_07_WorkspaceId_DeviceId_MetricDefinit_idx" on telemetry_points_2026_07 telemetry_points_1  (cost=0.55..5299.07 rows=3330 width=627) (actual time=0.069..0.792 rows=1870 loops=1)
                    Index Cond: (("WorkspaceId" = '22222222-2222-2222-2222-222222222222'::uuid) AND (("DeviceId")::text = ANY ('{benchmark-device-01,benchmark-device-02,benchmark-device-03,benchmark-device-04,benchmark-device-05}'::text[])) AND ("MetricDefinitionId" = '2694f2a2-22dd-5fbe-be97-d55a97cb350a'::uuid) AND ("OccurredAtUtc" >= ('2026-07-12 15:10:58.990082+00'::timestamp with time zone - '24:00:00'::interval)) AND ("OccurredAtUtc" <= '2026-07-12 15:10:58.990082+00'::timestamp with time zone))
                    Buffers: shared hit=96
Planning:
  Buffers: shared hit=85
Planning Time: 2.185 ms
Execution Time: 2.392 ms
```

## Q3

```sql
Limit  (cost=3587.74..3592.82 rows=2035 width=627) (actual time=2.854..3.204 rows=1870 loops=1)
  Buffers: shared hit=95
  ->  Sort  (cost=3587.74..3592.82 rows=2035 width=627) (actual time=2.852..3.045 rows=1870 loops=1)
        Sort Key: telemetry_points."OccurredAtUtc"
        Sort Method: quicksort  Memory: 531kB
        Buffers: shared hit=95
        ->  Append  (cost=0.43..3475.91 rows=2035 width=627) (actual time=0.102..1.667 rows=1870 loops=1)
              Buffers: shared hit=95
              Subplans Removed: 4
              ->  Index Scan using "telemetry_points_2026_07_WorkspaceId_DeviceId_MetricDefinit_idx" on telemetry_points_2026_07 telemetry_points_1  (cost=0.55..3431.89 rows=2031 width=627) (actual time=0.101..1.498 rows=1870 loops=1)
                    Index Cond: (("WorkspaceId" = '22222222-2222-2222-2222-222222222222'::uuid) AND (("DeviceId")::text = 'benchmark-device-01'::text) AND ("MetricDefinitionId" = ANY ('{2694f2a2-22dd-5fbe-be97-d55a97cb350a,89285110-1c72-5dd7-b9a6-04e7c692cff9,e5cc7f64-76ca-532d-a8f2-66916bcf3919,4d50e086-0532-5227-93f0-6c2c59a6b954,f85117b0-ad3e-53b5-bd66-cf237da8a8c7}'::uuid[])) AND ("OccurredAtUtc" >= ('2026-07-12 15:10:58.990082+00'::timestamp with time zone - '24:00:00'::interval)) AND ("OccurredAtUtc" <= '2026-07-12 15:10:58.990082+00'::timestamp with time zone))
                    Buffers: shared hit=95
Planning:
  Buffers: shared hit=95
Planning Time: 2.971 ms
Execution Time: 3.554 ms
```

## Q4

```sql
Limit  (cost=15893.62..18227.11 rows=20000 width=627) (actual time=38.072..55.871 rows=20000 loops=1)
  Buffers: shared hit=1144, temp read=247 written=248
  ->  Gather Merge  (cost=15893.62..18499.43 rows=22334 width=627) (actual time=38.071..54.102 rows=20000 loops=1)
        Workers Planned: 2
        Workers Launched: 2
        Buffers: shared hit=1144, temp read=247 written=248
        ->  Sort  (cost=14893.59..14921.51 rows=11167 width=627) (actual time=24.702..25.559 rows=6928 loops=3)
              Sort Key: telemetry_points."OccurredAtUtc"
              Sort Method: external merge  Disk: 1976kB
              Buffers: shared hit=1144, temp read=247 written=248
              Worker 0:  Sort Method: quicksort  Memory: 3471kB
              Worker 1:  Sort Method: quicksort  Memory: 3796kB
              ->  Parallel Append  (cost=0.48..11010.28 rows=11167 width=627) (actual time=1.325..9.624 rows=13677 loops=3)
                    Buffers: shared hit=1128
                    Subplans Removed: 4
                    ->  Parallel Bitmap Heap Scan on telemetry_points_2026_07 telemetry_points_1  (cost=1521.51..10920.44 rows=11163 width=627) (actual time=1.324..8.419 rows=13677 loops=3)
                          Recheck Cond: (("OccurredAtUtc" >= ('2026-07-12 15:10:58.990082+00'::timestamp with time zone - '24:00:00'::interval)) AND ("OccurredAtUtc" <= '2026-07-12 15:10:58.990082+00'::timestamp with time zone))
                          Filter: (("WorkspaceId" = '22222222-2222-2222-2222-222222222222'::uuid) AND (("DeviceId")::text = ANY ('{benchmark-device-01,benchmark-device-02,benchmark-device-03,benchmark-device-04,benchmark-device-05,benchmark-device-06,benchmark-device-07,benchmark-device-08,benchmark-device-09,benchmark-device-10}'::text[])) AND ("MetricDefinitionId" = ANY ('{2694f2a2-22dd-5fbe-be97-d55a97cb350a,89285110-1c72-5dd7-b9a6-04e7c692cff9,e5cc7f64-76ca-532d-a8f2-66916bcf3919,4d50e086-0532-5227-93f0-6c2c59a6b954,f85117b0-ad3e-53b5-bd66-cf237da8a8c7,dcf981e8-4f8f-5937-b2a4-4c73c8b76972,b46a2468-6d30-55f4-b044-e562438d3c9c,d295e729-426f-59d9-a3bb-63883a76abc6,cfc932cb-2064-5c85-95e4-9c4ad1169dbc,001c3fe8-4609-5d96-b91d-b29643f25722}'::uuid[])))
                          Heap Blocks: exact=314
                          Buffers: shared hit=1128
                          ->  Bitmap Index Scan on telemetry_points_2026_07_pkey  (cost=0.00..1514.77 rows=41034 width=0) (actual time=3.643..3.643 rows=41032 loops=1)
                                Index Cond: (("OccurredAtUtc" >= ('2026-07-12 15:10:58.990082+00'::timestamp with time zone - '24:00:00'::interval)) AND ("OccurredAtUtc" <= '2026-07-12 15:10:58.990082+00'::timestamp with time zone))
                                Buffers: shared hit=277
Planning:
  Buffers: shared hit=42
Planning Time: 1.456 ms
Execution Time: 59.641 ms
```

## Q5

```sql
GroupAggregate  (cost=29877.22..30533.62 rows=20197 width=48) (actual time=9.450..12.534 rows=720 loops=1)
  Group Key: (date_bin('01:00:00'::interval, telemetry_points."OccurredAtUtc", '2001-01-01 00:00:00+00'::timestamp with time zone))
  Buffers: shared hit=434
  ->  Sort  (cost=29877.22..29927.71 rows=20197 width=16) (actual time=9.435..10.093 rows=11455 loops=1)
        Sort Key: (date_bin('01:00:00'::interval, telemetry_points."OccurredAtUtc", '2001-01-01 00:00:00+00'::timestamp with time zone))
        Sort Method: quicksort  Memory: 832kB
        Buffers: shared hit=434
        ->  Append  (cost=0.43..28432.94 rows=20197 width=16) (actual time=0.047..6.913 rows=11455 loops=1)
              Buffers: shared hit=434
              Subplans Removed: 3
              ->  Index Scan using "telemetry_points_2026_06_WorkspaceId_DeviceId_MetricDefinit_idx" on telemetry_points_2026_06 telemetry_points_1  (cost=0.56..18383.44 rows=12321 width=16) (actual time=0.046..3.643 rows=7019 loops=1)
                    Index Cond: (("WorkspaceId" = '22222222-2222-2222-2222-222222222222'::uuid) AND (("DeviceId")::text = 'benchmark-device-01'::text) AND ("MetricDefinitionId" = '2694f2a2-22dd-5fbe-be97-d55a97cb350a'::uuid) AND ("OccurredAtUtc" >= ('2026-07-12 15:10:58.990082+00'::timestamp with time zone - '30 days'::interval)) AND ("OccurredAtUtc" <= '2026-07-12 15:10:58.990082+00'::timestamp with time zone))
                    Buffers: shared hit=265
              ->  Bitmap Heap Scan on telemetry_points_2026_07 telemetry_points_2  (cost=708.30..9923.15 rows=7873 width=16) (actual time=0.994..2.315 rows=4436 loops=1)
                    Recheck Cond: (("WorkspaceId" = '22222222-2222-2222-2222-222222222222'::uuid) AND (("DeviceId")::text = 'benchmark-device-01'::text) AND ("MetricDefinitionId" = '2694f2a2-22dd-5fbe-be97-d55a97cb350a'::uuid) AND ("OccurredAtUtc" >= ('2026-07-12 15:10:58.990082+00'::timestamp with time zone - '30 days'::interval)) AND ("OccurredAtUtc" <= '2026-07-12 15:10:58.990082+00'::timestamp with time zone))
                    Heap Blocks: exact=82
                    Buffers: shared hit=169
                    ->  Bitmap Index Scan on "telemetry_points_2026_07_WorkspaceId_DeviceId_MetricDefinit_idx"  (cost=0.00..706.33 rows=7873 width=0) (actual time=0.972..0.972 rows=4436 loops=1)
                          Index Cond: (("WorkspaceId" = '22222222-2222-2222-2222-222222222222'::uuid) AND (("DeviceId")::text = 'benchmark-device-01'::text) AND ("MetricDefinitionId" = '2694f2a2-22dd-5fbe-be97-d55a97cb350a'::uuid) AND ("OccurredAtUtc" >= ('2026-07-12 15:10:58.990082+00'::timestamp with time zone - '30 days'::interval)) AND ("OccurredAtUtc" <= '2026-07-12 15:10:58.990082+00'::timestamp with time zone))
                          Buffers: shared hit=87
Planning:
  Buffers: shared hit=66
Planning Time: 1.671 ms
Execution Time: 13.081 ms
```

## Q6

```sql
Finalize GroupAggregate  (cost=340298.22..418506.30 rows=291441 width=52) (actual time=1540.171..1583.031 rows=36100 loops=1)
  Group Key: (date_bin('06:00:00'::interval, telemetry_points."OccurredAtUtc", '2001-01-01 00:00:00+00'::timestamp with time zone)), telemetry_points."DeviceId", telemetry_points."MetricDefinitionId"
  Buffers: shared hit=12835 read=65559
  ->  Gather Merge  (cost=340298.22..408305.87 rows=582882 width=76) (actual time=1540.121..1566.692 rows=36281 loops=1)
        Workers Planned: 2
        Workers Launched: 2
        Buffers: shared hit=12835 read=65559
        ->  Sort  (cost=339298.20..340026.80 rows=291441 width=76) (actual time=1489.103..1490.706 rows=12094 loops=3)
              Sort Key: (date_bin('06:00:00'::interval, telemetry_points."OccurredAtUtc", '2001-01-01 00:00:00+00'::timestamp with time zone)), telemetry_points."DeviceId", telemetry_points."MetricDefinitionId"
              Sort Method: quicksort  Memory: 1686kB
              Buffers: shared hit=12835 read=65559
              Worker 0:  Sort Method: quicksort  Memory: 1841kB
              Worker 1:  Sort Method: quicksort  Memory: 1878kB
              ->  Partial HashAggregate  (cost=272535.21..299895.78 rows=291441 width=76) (actual time=1460.960..1464.282 rows=12094 loops=3)
                    Group Key: (date_bin('06:00:00'::interval, telemetry_points."OccurredAtUtc", '2001-01-01 00:00:00+00'::timestamp with time zone)), telemetry_points."DeviceId", telemetry_points."MetricDefinitionId"
                    Planned Partitions: 16  Batches: 1  Memory Usage: 2833kB
                    Buffers: shared hit=12789 read=65559
                    Worker 0:  Batches: 1  Memory Usage: 2833kB
                    Worker 1:  Batches: 1  Memory Usage: 3089kB
                    ->  Parallel Append  (cost=0.05..141234.81 rows=1214339 width=52) (actual time=62.363..993.447 rows=1257493 loops=3)
                          Buffers: shared hit=12789 read=65559
                          Subplans Removed: 1
                          ->  Parallel Seq Scan on telemetry_points_2026_05 telemetry_points_2  (cost=0.05..41264.21 rows=541543 width=52) (actual time=70.590..803.145 rows=1299704 loops=1)
                                Filter: (("OccurredAtUtc" <= '2026-07-12 15:10:58.990082+00'::timestamp with time zone) AND ("WorkspaceId" = '22222222-2222-2222-2222-222222222222'::uuid) AND (("DeviceId")::text = ANY ('{benchmark-device-01,benchmark-device-02,benchmark-device-03,benchmark-device-04,benchmark-device-05,benchmark-device-06,benchmark-device-07,benchmark-device-08,benchmark-device-09,benchmark-device-10}'::text[])) AND ("MetricDefinitionId" = ANY ('{2694f2a2-22dd-5fbe-be97-d55a97cb350a,89285110-1c72-5dd7-b9a6-04e7c692cff9,e5cc7f64-76ca-532d-a8f2-66916bcf3919,4d50e086-0532-5227-93f0-6c2c59a6b954,f85117b0-ad3e-53b5-bd66-cf237da8a8c7,dcf981e8-4f8f-5937-b2a4-4c73c8b76972,b46a2468-6d30-55f4-b044-e562438d3c9c,d295e729-426f-59d9-a3bb-63883a76abc6,cfc932cb-2064-5c85-95e4-9c4ad1169dbc,001c3fe8-4609-5d96-b91d-b29643f25722}'::uuid[])) AND ("OccurredAtUtc" >= ('2026-07-12 15:10:58.990082+00'::timestamp with time zone - '90 days'::interval)))
                                Buffers: shared hit=3187 read=20480
                          ->  Parallel Seq Scan on telemetry_points_2026_06 telemetry_points_3  (cost=0.05..39461.07 rows=342173 width=52) (actual time=73.936..781.195 rows=1257887 loops=1)
                                Filter: (("OccurredAtUtc" <= '2026-07-12 15:10:58.990082+00'::timestamp with time zone) AND ("WorkspaceId" = '22222222-2222-2222-2222-222222222222'::uuid) AND (("DeviceId")::text = ANY ('{benchmark-device-01,benchmark-device-02,benchmark-device-03,benchmark-device-04,benchmark-device-05,benchmark-device-06,benchmark-device-07,benchmark-device-08,benchmark-device-09,benchmark-device-10}'::text[])) AND ("MetricDefinitionId" = ANY ('{2694f2a2-22dd-5fbe-be97-d55a97cb350a,89285110-1c72-5dd7-b9a6-04e7c692cff9,e5cc7f64-76ca-532d-a8f2-66916bcf3919,4d50e086-0532-5227-93f0-6c2c59a6b954,f85117b0-ad3e-53b5-bd66-cf237da8a8c7,dcf981e8-4f8f-5937-b2a4-4c73c8b76972,b46a2468-6d30-55f4-b044-e562438d3c9c,d295e729-426f-59d9-a3bb-63883a76abc6,cfc932cb-2064-5c85-95e4-9c4ad1169dbc,001c3fe8-4609-5d96-b91d-b29643f25722}'::uuid[])) AND ("OccurredAtUtc" >= ('2026-07-12 15:10:58.990082+00'::timestamp with time zone - '90 days'::interval)))
                                Buffers: shared hit=3503 read=19382
                          ->  Parallel Seq Scan on telemetry_points_2026_04 telemetry_points_1  (cost=0.05..39127.93 rows=198210 width=52) (actual time=7.629..214.995 rows=242714 loops=3)
                                Filter: (("OccurredAtUtc" <= '2026-07-12 15:10:58.990082+00'::timestamp with time zone) AND ("WorkspaceId" = '22222222-2222-2222-2222-222222222222'::uuid) AND (("DeviceId")::text = ANY ('{benchmark-device-01,benchmark-device-02,benchmark-device-03,benchmark-device-04,benchmark-device-05,benchmark-device-06,benchmark-device-07,benchmark-device-08,benchmark-device-09,benchmark-device-10}'::text[])) AND ("MetricDefinitionId" = ANY ('{2694f2a2-22dd-5fbe-be97-d55a97cb350a,89285110-1c72-5dd7-b9a6-04e7c692cff9,e5cc7f64-76ca-532d-a8f2-66916bcf3919,4d50e086-0532-5227-93f0-6c2c59a6b954,f85117b0-ad3e-53b5-bd66-cf237da8a8c7,dcf981e8-4f8f-5937-b2a4-4c73c8b76972,b46a2468-6d30-55f4-b044-e562438d3c9c,d295e729-426f-59d9-a3bb-63883a76abc6,cfc932cb-2064-5c85-95e4-9c4ad1169dbc,001c3fe8-4609-5d96-b91d-b29643f25722}'::uuid[])) AND ("OccurredAtUtc" >= ('2026-07-12 15:10:58.990082+00'::timestamp with time zone - '90 days'::interval)))
                                Rows Removed by Filter: 176549
                                Buffers: shared hit=3094 read=19816
                          ->  Parallel Seq Scan on telemetry_points_2026_07 telemetry_points_4  (cost=0.05..15301.41 rows=132412 width=52) (actual time=42.559..359.164 rows=486746 loops=1)
                                Filter: (("OccurredAtUtc" <= '2026-07-12 15:10:58.990082+00'::timestamp with time zone) AND ("WorkspaceId" = '22222222-2222-2222-2222-222222222222'::uuid) AND (("DeviceId")::text = ANY ('{benchmark-device-01,benchmark-device-02,benchmark-device-03,benchmark-device-04,benchmark-device-05,benchmark-device-06,benchmark-device-07,benchmark-device-08,benchmark-device-09,benchmark-device-10}'::text[])) AND ("MetricDefinitionId" = ANY ('{2694f2a2-22dd-5fbe-be97-d55a97cb350a,89285110-1c72-5dd7-b9a6-04e7c692cff9,e5cc7f64-76ca-532d-a8f2-66916bcf3919,4d50e086-0532-5227-93f0-6c2c59a6b954,f85117b0-ad3e-53b5-bd66-cf237da8a8c7,dcf981e8-4f8f-5937-b2a4-4c73c8b76972,b46a2468-6d30-55f4-b044-e562438d3c9c,d295e729-426f-59d9-a3bb-63883a76abc6,cfc932cb-2064-5c85-95e4-9c4ad1169dbc,001c3fe8-4609-5d96-b91d-b29643f25722}'::uuid[])) AND ("OccurredAtUtc" >= ('2026-07-12 15:10:58.990082+00'::timestamp with time zone - '90 days'::interval)))
                                Buffers: shared hit=3005 read=5881
Planning:
  Buffers: shared hit=49
Planning Time: 1.822 ms
JIT:
  Functions: 66
  Options: Inlining false, Optimization false, Expressions true, Deforming true
  Timing: Generation 24.047 ms, Inlining 0.000 ms, Optimization 6.260 ms, Emission 194.901 ms, Total 225.208 ms
Execution Time: 1624.705 ms
```

## Q7

```sql
Unique  (cost=26727.98..27458.42 rows=58106 width=60) (actual time=262.640..287.986 rows=16775 loops=1)
  Buffers: shared hit=2583, temp read=604 written=605
  ->  Sort  (cost=26727.98..26910.59 rows=73044 width=60) (actual time=262.631..277.068 rows=66650 loops=1)
        Sort Key: telemetry_points."DeviceId", telemetry_points."MetricDefinitionId", (date_bin('00:15:00'::interval, telemetry_points."OccurredAtUtc", '2001-01-01 00:00:00+00'::timestamp with time zone)), telemetry_points."OccurredAtUtc" DESC
        Sort Method: external merge  Disk: 4832kB
        Buffers: shared hit=2583, temp read=604 written=605
        ->  Append  (cost=0.43..18079.81 rows=73044 width=60) (actual time=14.428..43.224 rows=66650 loops=1)
              Buffers: shared hit=2583
              Subplans Removed: 4
              ->  Bitmap Heap Scan on telemetry_points_2026_07 telemetry_points_1  (cost=6238.32..17680.72 rows=73040 width=60) (actual time=14.426..29.200 rows=66650 loops=1)
                    Recheck Cond: (("WorkspaceId" = '22222222-2222-2222-2222-222222222222'::uuid) AND (("DeviceId")::text = ANY ('{benchmark-device-01,benchmark-device-02,benchmark-device-03,benchmark-device-04,benchmark-device-05}'::text[])) AND ("MetricDefinitionId" = ANY ('{2694f2a2-22dd-5fbe-be97-d55a97cb350a,89285110-1c72-5dd7-b9a6-04e7c692cff9,e5cc7f64-76ca-532d-a8f2-66916bcf3919,4d50e086-0532-5227-93f0-6c2c59a6b954,f85117b0-ad3e-53b5-bd66-cf237da8a8c7}'::uuid[])) AND ("OccurredAtUtc" >= ('2026-07-12 15:10:58.990082+00'::timestamp with time zone - '7 days'::interval)) AND ("OccurredAtUtc" <= '2026-07-12 15:10:58.990082+00'::timestamp with time zone))
                    Heap Blocks: exact=1238
                    Buffers: shared hit=2583
                    ->  Bitmap Index Scan on "telemetry_points_2026_07_WorkspaceId_DeviceId_MetricDefinit_idx"  (cost=0.00..6220.06 rows=73040 width=0) (actual time=14.272..14.272 rows=66650 loops=1)
                          Index Cond: (("WorkspaceId" = '22222222-2222-2222-2222-222222222222'::uuid) AND (("DeviceId")::text = ANY ('{benchmark-device-01,benchmark-device-02,benchmark-device-03,benchmark-device-04,benchmark-device-05}'::text[])) AND ("MetricDefinitionId" = ANY ('{2694f2a2-22dd-5fbe-be97-d55a97cb350a,89285110-1c72-5dd7-b9a6-04e7c692cff9,e5cc7f64-76ca-532d-a8f2-66916bcf3919,4d50e086-0532-5227-93f0-6c2c59a6b954,f85117b0-ad3e-53b5-bd66-cf237da8a8c7}'::uuid[])) AND ("OccurredAtUtc" >= ('2026-07-12 15:10:58.990082+00'::timestamp with time zone - '7 days'::interval)) AND ("OccurredAtUtc" <= '2026-07-12 15:10:58.990082+00'::timestamp with time zone))
                          Buffers: shared hit=1345
Planning:
  Buffers: shared hit=41
Planning Time: 1.694 ms
Execution Time: 293.031 ms
```

## Decisão de índice

Índice novo necessário: **NÃO**.

- Q1–Q5 e Q7 ficaram muito abaixo de 2 segundos e usaram Index Scan/Bitmap Scan nos conjuntos seletivos.
- Q6 terminou em **1624,705 ms**, abaixo do gate de 2 segundos.
- Os Parallel Seq Scans de Q6 são adequados: a consulta de 90 dias/100 séries agrega aproximadamente 4,28 milhões das 5,11 milhões de linhas, portanto quase todo o conjunto das partições podadas é relevante.
- Q6 registrou `shared hit=12675 read=65719`, proporcional às milhões de linhas efetivamente agregadas.
- Nenhum índice ou migration foi criado após a medição.
