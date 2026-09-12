<#
.SYNOPSIS
    Deterministic start/stop regression against an absent disposable test profile.
.DESCRIPTION
    Uses real Docker operations. Intercepts docker stop only to launch a second
    PowerShell process after stop returns and before compose down can execute.
    No sleep-based race and no Force bypass. Refuses preexisting test containers.
#>
[CmdletBinding()]
param(
    [ValidateSet('transactional', 'durability')]
    [string] $Profile = 'transactional'
)

$ErrorActionPreference = 'Stop'
$LifecycleTest = [pscustomobject]@{ DockerExecutable = $null; StartScript = $null; TestProfile = $null; Events = $null; InjectStart = $false }
$LifecycleTest.DockerExecutable = (Get-Command docker -CommandType Application | Select-Object -First 1).Source
$LifecycleTest.StartScript = Join-Path $PSScriptRoot 'start-test-postgres.ps1'
$StopScript = Join-Path $PSScriptRoot 'stop-test-postgres.ps1'
$LifecycleTest.TestProfile = $Profile
$container = if ($Profile -eq 'durability') { 'fluxo-tests-postgres-durability' } else { 'fluxo-tests-postgres' }
$existing = & $LifecycleTest.DockerExecutable ps -a --format '{{.Names}}'
if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect Docker.' }
if (@($existing) -contains $container) { throw "Test requires absent container '$container'." }

$LifecycleTest.Events = New-Object 'System.Collections.Generic.List[string]'
$LifecycleTest.InjectStart = $false
$lease = $null
$started = $false

function docker {
    $arguments = @($args)
    if ($arguments -contains 'down') { $LifecycleTest.Events.Add('compose-down') }
    & $LifecycleTest.DockerExecutable @arguments
    $nativeExit = $LASTEXITCODE
    if ($arguments[0] -eq 'stop' -and $nativeExit -eq 0 -and $LifecycleTest.InjectStart) {
        $LifecycleTest.InjectStart = $false
        $LifecycleTest.Events.Add('docker-stop-completed')
        $command = "& '$($LifecycleTest.StartScript.Replace("'", "''"))' -Profile '$($LifecycleTest.TestProfile)'"
        $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
        $info = New-Object System.Diagnostics.ProcessStartInfo
        $info.FileName = (Get-Process -Id $PID).Path
        $info.Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand $encoded"
        $info.UseShellExecute = $false
        $info.CreateNoWindow = $true
        $info.RedirectStandardOutput = $true
        $info.RedirectStandardError = $true
        $child = [System.Diagnostics.Process]::Start($info)
        try {
            $stdout = $child.StandardOutput.ReadToEndAsync()
            $stderr = $child.StandardError.ReadToEndAsync()
            if (-not $child.WaitForExit(15000)) {
                $child.Kill()
                throw 'Concurrent start did not finish within the deadline.'
            }
            if ($child.ExitCode -eq 0 -or $stderr.Result -notmatch 'Lifecycle operation already in progress') {
                throw "Concurrent start was not refused by the lifecycle lock: $($stdout.Result) $($stderr.Result)"
            }
            $LifecycleTest.Events.Add('concurrent-start-refused')
            Write-Host 'PASS: start refused between docker stop and compose down.'
        }
        finally { $child.Dispose() }
    }
    Set-Variable -Name LASTEXITCODE -Value $nativeExit -Scope 1
}

try {
    $started = $true
    & $LifecycleTest.StartScript -Profile $Profile

    # Confirm the advisory lock still protects a live suite and that an error
    # releases the external lifecycle mutex before the next stop attempt.
    $info = New-Object System.Diagnostics.ProcessStartInfo
    $info.FileName = $LifecycleTest.DockerExecutable
    $info.Arguments = "exec -i $container psql -X -qAt -v ON_ERROR_STOP=1 -U fluxo_test -d fluxo_test_admin"
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardInput = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $lease = [System.Diagnostics.Process]::Start($info)
    $leaseErrors = $lease.StandardError.ReadToEndAsync()
    $lease.StandardInput.WriteLine("SELECT pg_try_advisory_lock_shared(hashtext('fluxo:tests:server-lease'));")
    $lease.StandardInput.Flush()
    $reply = $lease.StandardOutput.ReadLineAsync()
    if (-not $reply.Wait(15000) -or $reply.Result -ne 't') { throw 'Could not acquire test suite lease.' }
    try {
        & $StopScript -Profile $Profile
        throw 'Stop incorrectly ignored the active suite.'
    }
    catch {
        if ($_.Exception.Message -notmatch 'another test run is still using it') { throw }
        Write-Host 'PASS: active suite advisory lock still refuses stop.'
    }
    $running = & $LifecycleTest.DockerExecutable inspect --format '{{.State.Running}}' $container
    if ($LASTEXITCODE -ne 0 -or $running -ne 'true') { throw 'Active suite container was stopped.' }
    $lease.StandardInput.Close()
    if (-not $lease.WaitForExit(5000)) { throw 'Test lease did not exit.' }
    $lease.Dispose()
    $lease = $null

    $LifecycleTest.Events.Clear()
    $LifecycleTest.InjectStart = $true
    & $StopScript -Profile $Profile
    if (($LifecycleTest.Events -join ',') -ne 'docker-stop-completed,concurrent-start-refused,compose-down') {
        throw "Wrong lifecycle order: $($LifecycleTest.Events -join ',')"
    }

    # A refused start can retry after teardown has completely released its mutex.
    & $LifecycleTest.StartScript -Profile $Profile
    Write-Host 'PASS: start succeeds after teardown releases the external mutex.'
}
finally {
    $LifecycleTest.InjectStart = $false
    if ($null -ne $lease) {
        $lease.StandardInput.Close()
        if (-not $lease.WaitForExit(5000)) { $lease.Kill() }
        $lease.Dispose()
    }
    if ($started) { & $StopScript -Profile $Profile }
}

$remaining = & $LifecycleTest.DockerExecutable ps -a --format '{{.Names}}'
if ($LASTEXITCODE -ne 0 -or @($remaining) -contains $container) { throw 'Test container remained after cleanup.' }
Write-Host "PASS: lifecycle regression complete ($Profile); disposable container removed."
