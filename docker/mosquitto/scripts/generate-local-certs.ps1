param(
    [string]$OutputDir = "docker/mosquitto/certs",
    [string]$CommonName = "localhost",
    [string[]]$AdditionalSan = @(),
    [int]$Days = 365,
    [switch]$ForceNewCa
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

# Reemitir o server.crt sem tocar na CA invalida qualquer confianca ja
# instalada em devices remotos (ex.: o Gateway Pi guarda uma copia local de
# ca.crt). So gera uma CA nova quando nao existe uma ou quando -ForceNewCa
# for explicito -- trocar a CA exige redistribuir ca.crt para todo device.
$caExists = (Test-Path $caKey) -and (Test-Path $caCert)
if ($ForceNewCa -or -not $caExists) {
    openssl genrsa -out $caKey 4096 | Out-Null
    openssl req -x509 -new -nodes -key $caKey -sha256 -days $Days -subj "/CN=Fluxo Local CA" -out $caCert | Out-Null
    Write-Host "CA nova gerada -- redistribua $caCert para todo device/gateway que confia na CA anterior."
} else {
    Write-Host "Reaproveitando CA existente em $caCert (use -ForceNewCa para gerar uma nova)."
}

openssl genrsa -out $serverKey 2048 | Out-Null
openssl req -new -key $serverKey -subj "/CN=$CommonName" -out $serverCsr | Out-Null

# subjectAltName precisa cobrir todo hostname/IP que um client vai usar para
# conectar (Node.js e outros clients MQTT TLS validam o SAN, nao so a cadeia
# de confianca da CA) -- por isso aceita SANs extras alem do CommonName.
$sans = [System.Collections.Generic.List[string]]::new()
$primaryType = if ($CommonName -match '^\d{1,3}(\.\d{1,3}){3}$') { "IP" } else { "DNS" }
$sans.Add("$primaryType`:$CommonName")
foreach ($san in $AdditionalSan) {
    $sanType = if ($san -match '^\d{1,3}(\.\d{1,3}){3}$') { "IP" } else { "DNS" }
    $entry = "$sanType`:$san"
    if (-not $sans.Contains($entry)) {
        $sans.Add($entry)
    }
}

@"
authorityKeyIdentifier=keyid,issuer
basicConstraints=CA:FALSE
keyUsage = digitalSignature, keyEncipherment
extendedKeyUsage = serverAuth
subjectAltName = $($sans -join ', ')
"@ | Set-Content -Path $serverExt -Encoding ASCII

openssl x509 -req -in $serverCsr -CA $caCert -CAkey $caKey -CAcreateserial -out $serverCert -days $Days -sha256 -extfile $serverExt | Out-Null

Write-Host "Certificados TLS locais gerados em $OutputDir"
Write-Host "Arquivos: ca.crt, server.crt, server.key"
Write-Host "SAN do server.crt: $($sans -join ', ')"
Write-Host "Nao versione chaves reais de ambiente."
