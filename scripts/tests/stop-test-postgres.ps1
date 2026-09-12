<#
.SYNOPSIS
    Stops and removes a disposable PostgreSQL used by the integration tests.

.DESCRIPTION
    Tears down ONLY the compose project of the selected profile:
        -Profile transactional -> project 'fluxo-tests'
        -Profile durability    -> project 'fluxo-tests-durability'

    The pilot stack lives in a different compose project with different container, port and
    volume names and is not touched. The two test profiles are likewise independent, so
    stopping one cannot disturb a run using the other.

    Deliberately does NOT use docker system prune, docker volume prune, or any other global
    cleanup command: those would reach the pilot's fluxo_postgres_data and the Mosquitto
    runtime state.

.EXAMPLE
    .\scripts\tests\stop-test-postgres.ps1
    .\scripts\tests\stop-test-postgres.ps1 -Profile durability
#>
[CmdletBinding()]
param(
    [ValidateSet('transactional', 'durability')]
    [string] $Profile = 'transactional',

    # Tear down even if another test run still holds the server lease. Only for recovering a
    # server whose holder is known to be gone.
    [switch] $Force
)

$ErrorActionPreference = 'Stop'

$profiles = @{
    transactional = @{
        Project   = 'fluxo-tests'
        File      = 'docker\test\docker-compose.tests.yml'
        Container = 'fluxo-tests-postgres'
    }
    durability    = @{
        Project   = 'fluxo-tests-durability'
        File      = 'docker\test\docker-compose.durability.yml'
        Container = 'fluxo-tests-postgres-durability'
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

    # Refuse to destroy a server another run is still using. Every active run holds the lease in
    # SHARED mode, so acquiring it EXCLUSIVELY succeeds only when nobody is using the server.
    # The lease lives on each run's session, so a crashed run releases it automatically.
    $ErrorActionPreference = 'Continue'
    $runningNames = (docker ps --filter "name=$($selected.Container)" --filter 'status=running' --format '{{.Names}}')
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect Docker; refusing teardown.' }
    $isRunning = @($runningNames) -contains $selected.Container

    $leaseProcess = $null
    try {
        if ($isRunning -and -not $Force) {
            # Keep stdin open: psql retains the exclusive session lock until Docker stops
            # PostgreSQL. A one-shot psql invocation releases it before compose down.
            $startInfo = New-Object System.Diagnostics.ProcessStartInfo
            $startInfo.FileName = 'docker'
            $startInfo.Arguments = "exec -i $($selected.Container) psql -X -qAt -v ON_ERROR_STOP=1 -U fluxo_test -d fluxo_test_admin"
            $startInfo.UseShellExecute = $false
            $startInfo.CreateNoWindow = $true
            $startInfo.RedirectStandardInput = $true
            $startInfo.RedirectStandardOutput = $true
            $startInfo.RedirectStandardError = $true
            $leaseProcess = [System.Diagnostics.Process]::Start($startInfo)
            $leaseErrors = $leaseProcess.StandardError.ReadToEndAsync()
            $leaseProcess.StandardInput.WriteLine("SELECT pg_try_advisory_lock(hashtext('fluxo:tests:server-lease'));")
            $leaseProcess.StandardInput.Flush()
            $reply = $leaseProcess.StandardOutput.ReadLineAsync()
            if (-not $reply.Wait(15000)) { throw 'Lease check timed out; refusing teardown.' }
            $free = $reply.Result
            if ($free -eq 'f') {
                throw "Refusing to tear down '$($selected.Container)': another test run is still using it."
            }
            if ($free -ne 't' -or $leaseProcess.HasExited) {
                throw 'Exclusive lease was not confirmed; refusing teardown.'
            }

            # Stop while the exclusive lock is still held. Waiting test runs cannot acquire
            # their shared lease before shutdown; removal only follows a successful stop.
            docker stop $selected.Container
            if ($LASTEXITCODE -ne 0) { throw 'Docker stop failed; refusing resource removal.' }
        }
    }
    finally {
        if ($null -ne $leaseProcess) {
            $leaseProcess.StandardInput.Close()
            if (-not $leaseProcess.WaitForExit(5000)) { $leaseProcess.Kill() }
            $leaseProcess.Dispose()
        }
    }
    $ErrorActionPreference = 'Stop'

    Write-Host "Removing disposable test PostgreSQL -- profile '$Profile' (project '$($selected.Project)' only)..." -ForegroundColor Cyan

    # -v removes volumes owned by THIS compose project only: none for the transactional profile
    # (tmpfs), and the project-scoped postgres_data for the durability profile.
    #
    # 'Continue' because Windows PowerShell 5.1 promotes a native command's stderr to a
    # terminating error under 'Stop', and docker compose reports progress on stderr.
    $ErrorActionPreference = 'Continue'
    docker compose -p $selected.Project -f $ComposeFile down -v --remove-orphans
    $composeExit = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'

    if ($composeExit -ne 0) {
        throw "docker compose down failed with exit code $composeExit."
    }

    # Assignment rather than Remove-Item Env:\... -- the latter reads as a filesystem path to some
    # tooling and can be refused.
    $env:FLUXO_TESTS_POSTGRES_ADMIN_CONNECTION = $null
    $env:FLUXO_TESTS_REQUIRE_POSTGRES = $null

    Write-Host 'Done. Pilot stack untouched.' -ForegroundColor Green

}
finally {
    $lifecycleLock.ReleaseMutex()
    $lifecycleLock.Dispose()
}
