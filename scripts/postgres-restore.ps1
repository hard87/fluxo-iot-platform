param(
    [Parameter(Mandatory = $true)]
    [string]$InputPath,
    [string[]]$ComposeFile = @("docker-compose.yml"),
    [string]$Service = "postgres",
    [string]$Database = $env:FLUXO_DB_NAME,
    [string]$Username = $env:FLUXO_DB_USER
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $InputPath)) {
    throw "Input file not found: $InputPath"
}

if ([string]::IsNullOrWhiteSpace($Database)) {
    $Database = "fluxo_db"
}

if ([string]::IsNullOrWhiteSpace($Username)) {
    $Username = "fluxo"
}

$composeArgs = @("compose")
foreach ($file in $ComposeFile) {
    $composeArgs += @("-f", $file)
}

$composeArgs += @(
    "exec",
    "-T",
    $Service,
    "psql",
    "-U",
    $Username,
    "-d",
    $Database,
    "-v",
    "ON_ERROR_STOP=1"
)

Get-Content -LiteralPath $InputPath -Raw | & docker @composeArgs
if ($LASTEXITCODE -ne 0) {
    throw "psql restore failed with exit code $LASTEXITCODE."
}

Write-Host "Restore completed from $InputPath"
