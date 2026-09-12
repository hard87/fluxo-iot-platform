# Shared by start/stop. Name follows the Compose project, not the checkout path, so
# different checkouts and Windows sessions serialize access to the same resources.
# Fail closed on access errors; the OS releases ownership if the process dies.
function Enter-TestPostgresLifecycleLock {
    param([Parameter(Mandatory = $true)][string] $Project)

    $mutex = New-Object System.Threading.Mutex($false, "Global\Fluxo.TestPostgres.Lifecycle.$Project")
    $acquired = $false
    try {
        try { $acquired = $mutex.WaitOne(0) }
        catch [System.Threading.AbandonedMutexException] { $acquired = $true }
        if (-not $acquired) {
            throw "Lifecycle operation already in progress for '$Project'; refusing concurrent start/stop. Retry after it finishes."
        }
        return $mutex
    }
    catch {
        $mutex.Dispose()
        throw
    }
}
