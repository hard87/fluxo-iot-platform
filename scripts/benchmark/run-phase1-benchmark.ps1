param(
    [string]$WriterStrategy = "BinaryCopy",
    [double]$PostgresCpus = 2.0,
    [string]$PostgresMemory = "2g",
    [double]$WorkerCpus = 2.0,
    [string]$WorkerMemory = "1g",
    [string]$OutputDir = "docs/benchmarks/phase1-normalized-2026-07-11",
    [int]$DrainTimeoutSeconds = 180,
    [string[]]$ProfilesToRun = @(),
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$repo = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$compose = Join-Path $repo "scripts\benchmark\docker-compose.phase1.yml"
$out = Join-Path $repo $OutputDir
New-Item -ItemType Directory -Force -Path $out | Out-Null

$env:POSTGRES_CPUS = $PostgresCpus.ToString([Globalization.CultureInfo]::InvariantCulture)
$env:POSTGRES_MEMORY = $PostgresMemory
$env:WORKER_CPUS = $WorkerCpus.ToString([Globalization.CultureInfo]::InvariantCulture)
$env:WORKER_MEMORY = $WorkerMemory
$env:TELEMETRY_POINT_WRITER_STRATEGY = $WriterStrategy

$profiles = @(
    @{
        Name = "legacy"
        SchemaVersion = 1
        Devices = 100
        MessagesPerDevice = 300
        MetricsPerMessage = 5
        BooleanMetrics = 0
        TextMetrics = 0
        Seed = 42
        IntervalSeconds = 1.0
        ExpectedRecords = 30000
        ExpectedPoints = 150000
        Preseed = $false
    },
    @{
        Name = "v2-light"
        SchemaVersion = 2
        Devices = 100
        MessagesPerDevice = 300
        MetricsPerMessage = 5
        BooleanMetrics = 0
        TextMetrics = 0
        Seed = 42
        IntervalSeconds = 1.0
        ExpectedRecords = 30000
        ExpectedPoints = 150000
        Preseed = $false
    },
    @{
        Name = "v2-medium-copy"
        SchemaVersion = 2
        Devices = 100
        MessagesPerDevice = 300
        MetricsPerMessage = 16
        BooleanMetrics = 2
        TextMetrics = 1
        Seed = 42
        IntervalSeconds = 1.0
        ExpectedRecords = 30000
        ExpectedPoints = 480000
        Preseed = $false
    },
    @{
        Name = "v2-dense-copy"
        SchemaVersion = 2
        Devices = 100
        MessagesPerDevice = 300
        MetricsPerMessage = 32
        BooleanMetrics = 4
        TextMetrics = 2
        Seed = 42
        IntervalSeconds = 1.0
        ExpectedRecords = 30000
        ExpectedPoints = 960000
        Preseed = $true
    }
)

function Invoke-Compose {
    param([string[]]$Arguments)
    & docker compose -f $compose @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose failed: $($Arguments -join ' ')"
    }
}

function Wait-ContainerHealthy {
    param([string]$Container, [int]$TimeoutSeconds = 60)
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $status = & docker inspect --format "{{.State.Health.Status}}" $Container 2>$null
        if ($status -eq "healthy") { return }
        Start-Sleep -Seconds 1
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    throw "Timed out waiting for $Container health."
}

function Invoke-PostgresScalar {
    param([string]$Sql)
    $result = $Sql | & docker exec -i fluxo-phase1-bench-postgres psql -U fluxo -d fluxo_bench -t -A
    if ($LASTEXITCODE -ne 0) {
        throw "psql failed: $Sql"
    }
    return ($result | Select-Object -Last 1).Trim()
}

function Invoke-PostgresJson {
    param([string]$Sql)
    $raw = Invoke-PostgresScalar $Sql
    return $raw | ConvertFrom-Json
}

function Convert-DockerCpuPercentToCores {
    param([string]$CpuPercent)
    return ([double]($CpuPercent.TrimEnd("%")) / 100.0)
}

function Convert-DockerMemoryToBytes {
    param([string]$Value)
    $v = $Value.Trim()
    if ($v -match "^([0-9.]+)([KMGTP]?i?B)$") {
        $n = [double]$matches[1]
        switch ($matches[2]) {
            "B" { return [int64]$n }
            "kB" { return [int64]($n * 1000) }
            "KB" { return [int64]($n * 1000) }
            "KiB" { return [int64]($n * 1024) }
            "MB" { return [int64]($n * 1000 * 1000) }
            "MiB" { return [int64]($n * 1024 * 1024) }
            "GB" { return [int64]($n * 1000 * 1000 * 1000) }
            "GiB" { return [int64]($n * 1024 * 1024 * 1024) }
            "TB" { return [int64]($n * 1000 * 1000 * 1000 * 1000) }
            "TiB" { return [int64]($n * 1024 * 1024 * 1024 * 1024) }
        }
    }
    return 0
}

function Get-StorageSnapshot {
    $sql = @"
SELECT json_build_object(
  'record_data', pg_relation_size('telemetry_ingestion_records'),
  'record_index', pg_indexes_size('telemetry_ingestion_records'),
  'point_data', COALESCE((SELECT SUM(pg_relation_size(c.oid)) FROM pg_inherits i JOIN pg_class c ON c.oid = i.inhrelid JOIN pg_class p ON p.oid = i.inhparent WHERE p.relname = 'telemetry_points'), 0),
  'point_index', COALESCE((SELECT SUM(pg_indexes_size(c.oid)) FROM pg_inherits i JOIN pg_class c ON c.oid = i.inhrelid JOIN pg_class p ON p.oid = i.inhparent WHERE p.relname = 'telemetry_points'), 0),
  'definition_data', pg_relation_size('metric_definitions'),
  'definition_index', pg_indexes_size('metric_definitions'),
  'audit_data', pg_relation_size('metric_definition_discovery_audits'),
  'audit_index', pg_indexes_size('metric_definition_discovery_audits'),
  'wal_lsn', pg_current_wal_lsn()
)::text;
"@
    return Invoke-PostgresJson $sql
}

function Get-Counts {
    $sql = @"
SELECT json_build_object(
  'records', (SELECT COUNT(*) FROM telemetry_ingestion_records),
  'points', (SELECT COUNT(*) FROM telemetry_points),
  'definitions', (SELECT COUNT(*) FROM metric_definitions),
  'audits', (SELECT COUNT(*) FROM metric_definition_discovery_audits),
  'rejections', (SELECT COUNT(*) FROM telemetry_ingestion_rejections)
)::text;
"@
    return Invoke-PostgresJson $sql
}

function Get-WalBytes {
    param([string]$StartLsn, [string]$EndLsn)
    return [int64](Invoke-PostgresScalar "SELECT pg_wal_lsn_diff('$EndLsn', '$StartLsn')::bigint")
}

function Add-PreseedMetricDefinitions {
    param($Profile, [string]$WorkspaceId)

    $numeric = [int]$Profile.MetricsPerMessage - [int]$Profile.BooleanMetrics - [int]$Profile.TextMetrics
    $values = New-Object System.Collections.Generic.List[string]
    for ($i = 0; $i -lt $numeric; $i++) {
        $key = "numeric_{0:00}" -f $i
        $values.Add("(gen_random_uuid(), '$WorkspaceId', 'bench', '$key', '$key', 'Numeric', 'Discovered', true, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)")
    }
    for ($i = 0; $i -lt [int]$Profile.BooleanMetrics; $i++) {
        $key = "boolean_{0:00}" -f $i
        $values.Add("(gen_random_uuid(), '$WorkspaceId', 'bench', '$key', '$key', 'Boolean', 'Discovered', true, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)")
    }
    for ($i = 0; $i -lt [int]$Profile.TextMetrics; $i++) {
        $key = "text_{0:00}" -f $i
        $values.Add("(gen_random_uuid(), '$WorkspaceId', 'bench', '$key', '$key', 'Text', 'Discovered', true, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)")
    }

    if ($values.Count -eq 0) { return }
    $sql = "INSERT INTO metric_definitions (`"Id`", `"WorkspaceId`", `"TenantId`", `"MetricKey`", `"DisplayName`", `"ValueType`", `"Status`", `"IsQueryable`", `"IsAlertable`", `"CreatedAtUtc`", `"UpdatedAtUtc`") VALUES " + ($values -join ",") + " ON CONFLICT (`"WorkspaceId`", `"MetricKey`") DO NOTHING"
    Invoke-PostgresScalar $sql | Out-Null
}

function Add-BenchmarkWorkspaceAndDevices {
    param($Profile, [string]$Tenant, [string]$WorkspaceId, [string]$DevicePrefix)

    $workspaceName = $Profile.Name
    Invoke-PostgresScalar "INSERT INTO workspaces (`"Id`", `"TenantId`", `"Name`", `"IsActive`", `"CreatedAtUtc`") VALUES ('$WorkspaceId', '$Tenant', '$workspaceName', true, CURRENT_TIMESTAMP)" | Out-Null

    $values = New-Object System.Collections.Generic.List[string]
    for ($i = 1; $i -le [int]$Profile.Devices; $i++) {
        $device = "{0}-{1:000}" -f $DevicePrefix, $i
        $values.Add("(gen_random_uuid(), '$Tenant', '$WorkspaceId', '$device', '$device', 'Sensor', true, CURRENT_TIMESTAMP)")
    }
    $sql = "INSERT INTO devices (`"Id`", `"TenantId`", `"WorkspaceId`", `"Name`", `"Identifier`", `"Category`", `"IsActive`", `"CreatedAtUtc`") VALUES " + ($values -join ",")
    Invoke-PostgresScalar $sql | Out-Null
}

function Start-StatsSample {
    param([string]$ProfileName)
    $path = Join-Path $out "$ProfileName.stats.csv"
    "timestamp,container,cpu_cores,memory_bytes" | Set-Content -Path $path -Encoding UTF8
    return $path
}

function Add-StatsSample {
    param([string]$Path)
    $timestamp = [DateTimeOffset]::UtcNow.ToString("O")
    $raw = & docker stats --no-stream --format "{{json .}}" fluxo-phase1-bench-postgres fluxo-phase1-bench-worker
    foreach ($line in $raw) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $item = $line | ConvertFrom-Json
        $cores = Convert-DockerCpuPercentToCores $item.CPUPerc
        $memUsed = ($item.MemUsage -split "/")[0].Trim()
        $memBytes = Convert-DockerMemoryToBytes $memUsed
        "$timestamp,$($item.Name),$cores,$memBytes" | Add-Content -Path $Path -Encoding UTF8
    }
}

function Get-StatsSummary {
    param([string]$Path)
    $rows = Import-Csv $Path
    $summary = @{}
    foreach ($container in @("fluxo-phase1-bench-postgres", "fluxo-phase1-bench-worker")) {
        $values = @($rows | Where-Object { $_.container -eq $container })
        $cpu = @($values | ForEach-Object { [double]$_.cpu_cores })
        $mem = @($values | ForEach-Object { [double]$_.memory_bytes })
        $summary[$container] = @{
            cpu_avg = if ($cpu.Count) { ($cpu | Measure-Object -Average).Average } else { 0 }
            cpu_max = if ($cpu.Count) { ($cpu | Measure-Object -Maximum).Maximum } else { 0 }
            memory_avg = if ($mem.Count) { ($mem | Measure-Object -Average).Average } else { 0 }
            memory_max = if ($mem.Count) { ($mem | Measure-Object -Maximum).Maximum } else { 0 }
        }
    }
    return $summary
}

function Get-LatestBenchmarkMetrics {
    $previous = $ErrorActionPreference
    $logs = @()
    try {
        $ErrorActionPreference = "Continue"
        $logs = & docker logs --tail 2000 fluxo-phase1-bench-worker 2>$null
    }
    finally {
        $ErrorActionPreference = $previous
    }
    $line = $logs | Where-Object { $_ -match "BENCHMARK_METRICS" } | Select-Object -Last 1
    if (-not $line) {
        return @{ p50 = 0; p95 = 0; p99 = 0; max_buffer = 0 }
    }
    $result = @{}
    foreach ($name in @("p50_ms", "p95_ms", "p99_ms", "max_buffer")) {
        if ($line -match "$name=([0-9.]+)") {
            $result[$name] = [double]::Parse($matches[1], [Globalization.CultureInfo]::InvariantCulture)
        }
    }
    return @{
        p50 = $result["p50_ms"]
        p95 = $result["p95_ms"]
        p99 = $result["p99_ms"]
        max_buffer = [int]$result["max_buffer"]
    }
}

function Save-DockerLogsBestEffort {
    param([string]$Container, [string]$Path)
    $previous = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        & docker logs --tail 2000 $Container *> $Path
    }
    catch {
        "Failed to collect docker logs for $Container`: $($_.Exception.Message)" | Set-Content -Path $Path -Encoding UTF8
    }
    finally {
        $ErrorActionPreference = $previous
    }
}

function Wait-ForCounts {
    param($Profile, [string]$StatsPath)
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($DrainTimeoutSeconds)
    do {
        Add-StatsSample $StatsPath
        $counts = Get-Counts
        if ([int64]$counts.records -eq [int64]$Profile.ExpectedRecords -and [int64]$counts.points -eq [int64]$Profile.ExpectedPoints) {
            return $counts
        }
        Start-Sleep -Seconds 1
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    throw "Timed out waiting for $($Profile.Name): records=$($counts.records)/$($Profile.ExpectedRecords), points=$($counts.points)/$($Profile.ExpectedPoints), rejections=$($counts.rejections)"
}

function Invoke-Migrations {
    $connectionString = "Host=127.0.0.1;Port=15432;Database=fluxo_bench;Username=fluxo;Password=fluxo_bench"
    $previous = $env:ConnectionStrings__DefaultConnection
    try {
        $env:ConnectionStrings__DefaultConnection = $connectionString
        & dotnet ef database update --project (Join-Path $repo "src\Fluxo.Infrastructure\Fluxo.Infrastructure.csproj") --startup-project (Join-Path $repo "src\Fluxo.Api\Fluxo.Api.csproj")
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet ef database update failed"
        }
    }
    finally {
        $env:ConnectionStrings__DefaultConnection = $previous
    }
}

function Invoke-Profile {
    param($Profile)

    Write-Host "=== profile $($Profile.Name) ==="
    foreach ($suffix in @(
        "devices.json",
        "provision.log",
        "simulator.log",
        "simulator.log.err",
        "worker.log",
        "postgres.log",
        "stats.csv",
        "json"
    )) {
        Remove-Item (Join-Path $out "$($Profile.Name)-$suffix") -ErrorAction SilentlyContinue
    }
    Remove-Item (Join-Path $out "$($Profile.Name).json") -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $out "$($Profile.Name).stats.csv") -ErrorAction SilentlyContinue

    Invoke-Compose @("down", "-v", "--remove-orphans")
    Invoke-Compose @("up", "-d", "postgres")
    Wait-ContainerHealthy "fluxo-phase1-bench-postgres"
    Invoke-Migrations
    if ($SkipBuild) {
        Invoke-Compose @("up", "-d", "mosquitto", "api", "worker")
    } else {
        Invoke-Compose @("up", "-d", "--build", "mosquitto", "api", "worker")
    }

    $tenant = "bench-$($Profile.Name)".ToLowerInvariant()
    $prefix = "bench-$($Profile.Name)"
    $workspaceId = [guid]::NewGuid().ToString()
    Add-BenchmarkWorkspaceAndDevices $Profile $tenant $workspaceId $prefix
    Write-Host "workspace profile=$($Profile.Name) workspaceId=$workspaceId"

    if ($Profile.Preseed) {
        Add-PreseedMetricDefinitions $Profile $workspaceId
    }

    $before = Get-StorageSnapshot
    $statsPath = Start-StatsSample $Profile.Name
    $simLog = Join-Path $out "$($Profile.Name)-simulator.log"

    $simArgs = @(
        (Join-Path $repo "scripts\mqtt-device-simulator.py"),
        "--schema-version", $Profile.SchemaVersion,
        "--tenant-id", $tenant,
        "--workspace-id", $workspaceId,
        "--device-prefix", $prefix,
        "--devices", $Profile.Devices,
        "--host", "localhost",
        "--port", "11883",
        "--interval-seconds", $Profile.IntervalSeconds,
        "--messages-per-device", $Profile.MessagesPerDevice,
        "--metrics-per-message", $Profile.MetricsPerMessage,
        "--boolean-metrics", $Profile.BooleanMetrics,
        "--text-metrics", $Profile.TextMetrics,
        "--seed", $Profile.Seed
    )

    $started = [DateTimeOffset]::UtcNow
    $process = Start-Process -FilePath "python" -ArgumentList $simArgs -NoNewWindow -PassThru -RedirectStandardOutput $simLog -RedirectStandardError "$simLog.err"
    while (-not $process.HasExited) {
        Add-StatsSample $statsPath
        Start-Sleep -Seconds 1
    }
    $process.WaitForExit()
    $exitCode = $process.ExitCode
    if ($null -eq $exitCode -and (Test-Path $simLog)) {
        $summary = Get-Content $simLog -Tail 5 | Where-Object { $_ -match "summary published=.* errors=0 " } | Select-Object -Last 1
        if ($summary) { $exitCode = 0 }
    }
    if ($exitCode -ne 0) {
        $simErr = "$simLog.err"
        $detail = if (Test-Path $simErr) { Get-Content $simErr -Raw } else { "" }
        throw "simulator failed for $($Profile.Name); exit=$exitCode; see $simLog $detail"
    }
    $published = [DateTimeOffset]::UtcNow

    $counts = Wait-ForCounts $Profile $statsPath
    $drained = [DateTimeOffset]::UtcNow
    $after = Get-StorageSnapshot
    $wal = Get-WalBytes $before.wal_lsn $after.wal_lsn
    $metrics = Get-LatestBenchmarkMetrics
    $stats = Get-StatsSummary $statsPath

    $publishToDrain = ($drained - $started).TotalSeconds
    $recovery = ($drained - $published).TotalSeconds
    $recordData = [int64]$after.record_data - [int64]$before.record_data
    $recordIndex = [int64]$after.record_index - [int64]$before.record_index
    $pointData = [int64]$after.point_data - [int64]$before.point_data
    $pointIndex = [int64]$after.point_index - [int64]$before.point_index
    $definitionOverhead = ([int64]$after.definition_data - [int64]$before.definition_data) + ([int64]$after.definition_index - [int64]$before.definition_index)
    $auditOverhead = ([int64]$after.audit_data - [int64]$before.audit_data) + ([int64]$after.audit_index - [int64]$before.audit_index)

    $result = [ordered]@{
        profile = $Profile.Name
        writer = $WriterStrategy
        workspace_id = $workspaceId
        resource_budget = @{
            postgres_cpus = $PostgresCpus
            postgres_memory = $PostgresMemory
            worker_cpus = $WorkerCpus
            worker_memory = $WorkerMemory
        }
        p50_ms = $metrics.p50
        p95_ms = $metrics.p95
        p99_ms = $metrics.p99
        throughput_records_per_sec = [math]::Round(([double]$counts.records / $publishToDrain), 3)
        backlog_max = $metrics.max_buffer
        publish_to_drain_seconds = [math]::Round($publishToDrain, 3)
        recovery_seconds = [math]::Round($recovery, 3)
        records = [int64]$counts.records
        points = [int64]$counts.points
        rejections = [int64]$counts.rejections
        wal_bytes = $wal
        postgres_cpu_cores_avg = [math]::Round($stats["fluxo-phase1-bench-postgres"].cpu_avg, 4)
        postgres_cpu_cores_max = [math]::Round($stats["fluxo-phase1-bench-postgres"].cpu_max, 4)
        postgres_normalized_cpu_avg = [math]::Round($stats["fluxo-phase1-bench-postgres"].cpu_avg / $PostgresCpus, 4)
        postgres_normalized_cpu_max = [math]::Round($stats["fluxo-phase1-bench-postgres"].cpu_max / $PostgresCpus, 4)
        worker_cpu_cores_avg = [math]::Round($stats["fluxo-phase1-bench-worker"].cpu_avg, 4)
        worker_cpu_cores_max = [math]::Round($stats["fluxo-phase1-bench-worker"].cpu_max, 4)
        worker_normalized_cpu_avg = [math]::Round($stats["fluxo-phase1-bench-worker"].cpu_avg / $WorkerCpus, 4)
        worker_normalized_cpu_max = [math]::Round($stats["fluxo-phase1-bench-worker"].cpu_max / $WorkerCpus, 4)
        postgres_memory_avg_bytes = [int64]$stats["fluxo-phase1-bench-postgres"].memory_avg
        postgres_memory_max_bytes = [int64]$stats["fluxo-phase1-bench-postgres"].memory_max
        worker_memory_avg_bytes = [int64]$stats["fluxo-phase1-bench-worker"].memory_avg
        worker_memory_max_bytes = [int64]$stats["fluxo-phase1-bench-worker"].memory_max
        record_data_bytes = $recordData
        record_index_bytes = $recordIndex
        point_data_bytes = $pointData
        point_index_bytes = $pointIndex
        metric_definition_overhead_bytes = $definitionOverhead
        discovery_audit_overhead_bytes = $auditOverhead
        bytes_per_record_incremental = [math]::Round((($recordData + $recordIndex) / [double]$counts.records), 3)
        bytes_per_point_incremental = [math]::Round(($pointData / [double]$counts.points), 3)
        index_bytes_per_point_incremental = [math]::Round(($pointIndex / [double]$counts.points), 3)
    }

    $resultPath = Join-Path $out "$($Profile.Name).json"
    $result | ConvertTo-Json -Depth 8 | Set-Content -Path $resultPath -Encoding UTF8
    Save-DockerLogsBestEffort "fluxo-phase1-bench-worker" (Join-Path $out "$($Profile.Name)-worker.log")
    Save-DockerLogsBestEffort "fluxo-phase1-bench-postgres" (Join-Path $out "$($Profile.Name)-postgres.log")
    return $result
}

try {
    $all = New-Object System.Collections.Generic.List[object]
    $selectedProfiles = if ($ProfilesToRun.Count -gt 0) {
        @($profiles | Where-Object { $ProfilesToRun -contains $_.Name })
    } else {
        $profiles
    }
    foreach ($profile in $selectedProfiles) {
        $all.Add((Invoke-Profile $profile))
    }
    $all | ConvertTo-Json -Depth 8 | Set-Content -Path (Join-Path $out "summary.json") -Encoding UTF8
    $all | Export-Csv -Path (Join-Path $out "summary.csv") -NoTypeInformation -Encoding UTF8
}
finally {
    Invoke-Compose @("down", "-v", "--remove-orphans")
}
