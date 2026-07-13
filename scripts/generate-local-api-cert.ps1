param(
    [string]$OutputPath = "certs/fluxo-api-dev.pfx",
    [string]$Password = "changeit"
)

$directory = Split-Path $OutputPath -Parent
if (-not [string]::IsNullOrWhiteSpace($directory)) {
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
}

dotnet dev-certs https --clean | Out-Null
dotnet dev-certs https -ep $OutputPath -p $Password | Out-Null
dotnet dev-certs https --trust | Out-Null

Write-Host "Certificado HTTPS local da API gerado em: $OutputPath"
Write-Host "Nao versione arquivos .pfx reais."
