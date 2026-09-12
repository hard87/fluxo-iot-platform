<#
.SYNOPSIS
    Starts a disposable PostgreSQL for the integration tests.

.DESCRIPTION
    Brings up one of two profiles under its own compose project, waits for health, verifies
    the disposable-environment marker, and prints the connection string to export.

    -Profile transactional (default)
        Transactions, locks, SKIP LOCKED, constraints, isolation, concurrency, migrations.
        fsync/synchronous_commit/full_page_writes are OFF and data lives in tmpfs, so it is
        fast but makes NO durability guarantee.

    -Profile durability
        Crash recovery, restart survival, "crash between persist and send".
        fsync/synchronous_commit/full_page_writes are ON and data lives in a named volume
        scoped to its own compose project.

    Using the transactional profile for a durability test would make the assertion vacuous:
    committed data is expected to be lost there on an unclean stop.

    Neither profile shares a container name, port, volume or network with the pilot stack
    (fluxo-postgres, host 5433, volume fluxo_postgres_data, network fluxo_default).

.EXAMPLE
    .\scripts\tests\start-test-postgres.ps1
    .\scripts\tests\start-test-postgres.ps1 -Profile durability

.NOTES
    Never use global Docker cleanup (docker system prune / docker volume prune) with this.
    stop-test-postgres.ps1 removes only the compose project of the selected profile.
#>
[CmdletBinding()]
param(
    [ValidateSet('transactional', 'durability')]
    [string] $Profile = 'transactional',

    [int] $TimeoutSeconds = 90
)

$ErrorActionPreference = 'Stop'

$profiles = @{
    transactional = @{
        Project   = 'fluxo-tests'
        File      = 'docker\test\docker-compose.tests.yml'
        Container = 'fluxo-tests-postgres'
        Port      = 55432
    }
    durability    = @{
        Project   = 'fluxo-tests-durability'
        File      = 'docker\test\docker-compose.durability.yml'
        Container = 'fluxo-tests-postgres-durability'
        Port      = 55433
    }
}

$selected = $profiles[$Profile]
$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$ComposeFile = Join-Path $RepoRoot $selected.File

# Acquire before any inspection and retain through the entire operation, including
# the interval after PostgreSQL stops and before Compose removes its resources.
. (Join-Path $PSScriptRoot 'test-postgres-lifecycle-lock.ps1')
$lifecycleLock = Enter-TestPostgresLifecycleLock -Project $selected.Project
try {
    if (-not (Test-Path $ComposeFile)) {
        throw "Compose file not found: $ComposeFile"
    }

    Write-Host "Starting disposable test PostgreSQL -- profile '$Profile' (project '$($selected.Project)')..." -ForegroundColor Cyan

    # Windows PowerShell 5.1 turns a native command's stderr into terminating ErrorRecords when
    # $ErrorActionPreference is 'Stop'. docker compose writes its progress to stderr, so native
    # calls are made with 'Continue' and judged by $LASTEXITCODE instead.
    $ErrorActionPreference = 'Continue'
    docker compose -p $selected.Project -f $ComposeFile up -d
    $composeExit = $LASTEXITCODE

    # Wait for health rather than sleeping a fixed amount.
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $health = ''

    if ($composeExit -eq 0) {
        while ((Get-Date) -lt $deadline) {
            $health = (docker inspect --format '{{.State.Health.Status}}' $selected.Container 2>$null)

            if ($health -eq 'healthy') { break }

            if ($health -eq 'unhealthy') {
                docker logs --tail 40 $selected.Container
                break
            }

            Start-Sleep -Milliseconds 500
        }
    }

    # Prove the marker is present; the test guards depend on it.
    $marker = ''
    if ($health -eq 'healthy') {
        $marker = docker exec $selected.Container psql -U fluxo_test -d fluxo_test_admin -tAc `
            "SELECT to_regclass('public.fluxo_disposable_test_marker') IS NOT NULL"
    }
    $ErrorActionPreference = 'Stop'

    if ($composeExit -ne 0) {
        throw "docker compose up failed with exit code $composeExit."
    }

    if ($health -ne 'healthy') {
        throw "$($selected.Container) did not become healthy within $TimeoutSeconds seconds (last status: '$health')."
    }

    if ("$marker".Trim() -ne 't') {
        throw "Disposable-environment marker table is missing. Did docker/test/init/00-marker.sql run?"
    }

    $connectionString = "Host=127.0.0.1;Port=$($selected.Port);Database=fluxo_test_admin;Username=fluxo_test;Password=fluxo_test_local_only"
    $env:FLUXO_TESTS_POSTGRES_ADMIN_CONNECTION = $connectionString

    $storage = if ($Profile -eq 'durability') { 'named volume, fsync on' } else { 'tmpfs, fsync off' }

    Write-Host ''
    Write-Host "Ready. Marker verified. Profile '$Profile' ($storage)." -ForegroundColor Green
    Write-Host "FLUXO_TESTS_POSTGRES_ADMIN_CONNECTION is set for THIS shell only."

    if ($Profile -eq 'transactional') {
        Write-Host 'This profile makes NO durability guarantee. Use -Profile durability for crash-recovery tests.' -ForegroundColor Yellow
    }

    Write-Host ''
    Write-Host 'For another shell:' -ForegroundColor Yellow
    Write-Host "  `$env:FLUXO_TESTS_POSTGRES_ADMIN_CONNECTION = '$connectionString'"
    Write-Host ''
    Write-Host 'To make missing infrastructure fail instead of skip:' -ForegroundColor Yellow
    Write-Host "  `$env:FLUXO_TESTS_REQUIRE_POSTGRES = '1'"

}
finally {
    $lifecycleLock.ReleaseMutex()
    $lifecycleLock.Dispose()
}
