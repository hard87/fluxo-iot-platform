param(
    [string]$OutputDir = "docker/mosquitto/certs",
    [string]$CommonName = "localhost",
    [int]$Days = 365
)

if (-not (Get-Command openssl -ErrorAction SilentlyContinue)) {
    throw "Comando 'openssl' nao encontrado. Instale OpenSSL para gerar certificados locais."
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$caKey = Join-Path $OutputDir "ca.key"
$caCert = Join-Path $OutputDir "ca.crt"
$serverKey = Join-Path $OutputDir "server.key"
$serverCsr = Join-Path $OutputDir "server.csr"
$serverCert = Join-Path $OutputDir "server.crt"
$serverExt = Join-Path $OutputDir "server.ext"

openssl genrsa -out $caKey 4096 | Out-Null
openssl req -x509 -new -nodes -key $caKey -sha256 -days $Days -subj "/CN=Fluxo Local CA" -out $caCert | Out-Null

openssl genrsa -out $serverKey 2048 | Out-Null
openssl req -new -key $serverKey -subj "/CN=$CommonName" -out $serverCsr | Out-Null

$sanType = if ($CommonName -match '^\d{1,3}(\.\d{1,3}){3}$') { "IP" } else { "DNS" }
@"
authorityKeyIdentifier=keyid,issuer
basicConstraints=CA:FALSE
keyUsage = digitalSignature, keyEncipherment
extendedKeyUsage = serverAuth
subjectAltName = $sanType:$CommonName
"@ | Set-Content -Path $serverExt -Encoding ASCII

openssl x509 -req -in $serverCsr -CA $caCert -CAkey $caKey -CAcreateserial -out $serverCert -days $Days -sha256 -extfile $serverExt | Out-Null

Write-Host "Certificados TLS locais gerados em $OutputDir"
Write-Host "Arquivos: ca.crt, server.crt, server.key"
Write-Host "Nao versione chaves reais de ambiente."
