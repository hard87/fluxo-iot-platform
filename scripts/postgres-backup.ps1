param(
    [string[]]$ComposeFile = @("docker-compose.yml"),
    [string]$Service = "postgres",
    [string]$Database = $env:FLUXO_DB_NAME,
    [string]$Username = $env:FLUXO_DB_USER,
    [string]$OutputDirectory = "backups/postgres"
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Database)) {
    $Database = "fluxo_db"
}

if ([string]::IsNullOrWhiteSpace($Username)) {
    $Username = "fluxo"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$outputPath = Join-Path $OutputDirectory "fluxo-postgres-$timestamp.sql"

$composeArgs = @("compose")
foreach ($file in $ComposeFile) {
    $composeArgs += @("-f", $file)
}

$composeArgs += @(
    "exec",
    "-T",
    $Service,
    "pg_dump",
    "-U",
    $Username,
    "-d",
    $Database,
    "--no-owner",
    "--no-privileges"
)

$dump = & docker @composeArgs
if ($LASTEXITCODE -ne 0) {
    throw "pg_dump failed with exit code $LASTEXITCODE."
}

$dump | Set-Content -LiteralPath $outputPath -Encoding UTF8
Write-Host "Backup written to $outputPath"
