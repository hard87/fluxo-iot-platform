# Fluxo - Guia do Piloto Real Controlado

Este guia descreve o caminho minimo para operar um piloto controlado com ate 100 dispositivos simultaneos, usando JWT no portal/API, MQTT com credenciais por device e backup/restore basico.

## 1. Pre-requisitos

- Docker + Docker Compose
- .NET SDK 10
- Python 3.10+ para o simulador MQTT
- `mosquitto_pub` e `mosquitto_passwd` para validacao manual
- OpenSSL para gerar certificados MQTT locais

## 2. Preparar `.env`

```powershell
Copy-Item .env.example .env
```

Edite `.env` antes de subir o ambiente:

- troque `FLUXO_AUTH_SIGNING_KEY`;
- troque senhas de PostgreSQL e MQTT;
- mantenha `FLUXO_MQTT_PUBLIC_BIND=127.0.0.1` no dev;
- para controlled-prod, configure `FLUXO_PORTAL_ORIGIN` e `VITE_API_BASE_URL`;
- nao versione `.env`.

## 3. Subir ambiente

Dev local:

```powershell
docker compose build
docker compose up -d
```

Producao controlada minima:

```powershell
docker compose -f docker-compose.controlled-prod.yml build
docker compose -f docker-compose.controlled-prod.yml up -d
```

No perfil controlled-prod, MQTT publica apenas `8883`; `1883` fica sem porta publicada no host.

## 4. Aplicar migrations

```powershell
dotnet ef database update `
  --project src/Fluxo.Infrastructure/Fluxo.Infrastructure.csproj `
  --startup-project src/Fluxo.Api/Fluxo.Api.csproj
```

Health da API:

```powershell
Invoke-RestMethod http://localhost:5000/health
```

## 5. Registrar usuario e obter JWT

```powershell
$api = "http://localhost:5000"
$email = "operador@example.test"
$password = "TroqueEstaSenha123!"

Invoke-RestMethod `
  -Method POST `
  -Uri "$api/api/auth/register" `
  -ContentType "application/json" `
  -Body (@{ email = $email; password = $password } | ConvertTo-Json)

$login = Invoke-RestMethod `
  -Method POST `
  -Uri "$api/api/auth/login" `
  -ContentType "application/json" `
  -Body (@{ email = $email; password = $password } | ConvertTo-Json)

$token = $login.accessToken
$headers = @{ Authorization = "Bearer $token" }
```

Todos os endpoints protegidos abaixo usam:

```powershell
$headers = @{ Authorization = "Bearer <token>" }
```

## 6. Criar workspace

```powershell
$workspace = Invoke-RestMethod `
  -Method POST `
  -Uri "$api/api/workspaces" `
  -Headers $headers `
  -ContentType "application/json" `
  -Body (@{
    name = "Piloto ACME"
    tenantId = "acme-industria"
  } | ConvertTo-Json)

$workspaceId = $workspace.id
```

Listar workspaces:

```powershell
Invoke-RestMethod -Method GET -Uri "$api/api/workspaces" -Headers $headers
```

## 7. Provisionar dispositivo

`category = 1` representa `Sensor`.

```powershell
$device = Invoke-RestMethod `
  -Method POST `
  -Uri "$api/api/workspaces/$workspaceId/devices/provision" `
  -Headers $headers `
  -ContentType "application/json" `
  -Body (@{
    name = "ESP32 Lab 01"
    identifier = "esp32-lab-01"
    category = 1
  } | ConvertTo-Json)

$deviceId = $device.deviceId
$deviceIdentifier = $device.deviceIdentifier
$credentialUsername = $device.credentialUsername
$provisioningSecret = $device.provisioningSecret
$mqttTopic = $device.mqttPublishTopic
```

Guarde `provisioningSecret` apenas no cofre/ambiente do device. Ele e exibido no provisionamento/rotacao e nao deve ir para o Git.

## 8. Credenciais do Mosquitto

Nao ha nada a fazer aqui: o passo 7 (provisionar dispositivo) ja criou automaticamente o
usuario e a ACL correspondentes no broker (plugin `dynamic-security`), usando o
`credentialUsername`/`provisioningSecret`/`mqttPublishTopic` retornados na mesma resposta.
Veja [docs/mqtt-tls-e-credenciais.md](mqtt-tls-e-credenciais.md) para detalhes do mecanismo.

## 9. Configurar firmware ESP32 de referencia

1. Copie o arquivo local:

```powershell
Copy-Item devices/esp32-reference-node/main/app_config.local.example.h `
  devices/esp32-reference-node/main/app_config.local.h
```

2. Configure:

- Wi-Fi;
- `APP_MQTT_BROKER_URI` como `mqtts://broker.fluxo.local:8883`;
- `APP_MQTT_USERNAME` com `credentialUsername`;
- `APP_MQTT_PASSWORD` com `provisioningSecret`;
- `APP_MQTT_CA_CERT_PEM` com a CA do broker.

`app_config.local.h` esta ignorado no Git.

## 10. Validar publicacao MQTT

Com TLS:

```powershell
$payload = @{
  schemaVersion = "1.0"
  tenantId = "acme-industria"
  workspaceId = $workspaceId
  deviceId = $deviceIdentifier
  messageType = "telemetry"
  timestampUtc = (Get-Date).ToUniversalTime().ToString("o")
  sequence = 1
  firmwareVersion = "esp32-reference"
  metrics = @{
    temperature = 24.5
    humidity = 60.2
    battery = 3.92
    rssi = -55
    uptimeSec = 120
  }
} | ConvertTo-Json -Depth 5 -Compress

mosquitto_pub `
  -h broker.fluxo.local `
  -p 8883 `
  --cafile docker/mosquitto/certs/ca.crt `
  -u $credentialUsername `
  -P $provisioningSecret `
  -t $mqttTopic `
  -m $payload
```

Dev local sem TLS deve ficar restrito a `127.0.0.1:1883`.

## 11. Consultar telemetria

```powershell
Invoke-RestMethod `
  -Method GET `
  -Uri "$api/api/workspaces/$workspaceId/devices/$deviceId/telemetry?page=1&pageSize=20" `
  -Headers $headers
```

Detalhe de provisionamento:

```powershell
Invoke-RestMethod `
  -Method GET `
  -Uri "$api/api/workspaces/$workspaceId/devices/$deviceId/provisioning" `
  -Headers $headers
```

## 12. Simular 10, 50 e 100 dispositivos

Valide primeiro o payload sem broker:

```powershell
python scripts/mqtt-device-simulator.py `
  --dry-run `
  --tenant-id acme-industria `
  --workspace-id $workspaceId
```

Como o broker usa `dynamic-security`, cada device simulado precisa da sua propria credencial
(nao ha mais senha compartilhada). Provisione os devices pela API primeiro:

```powershell
python scripts/provision-simulated-devices.py `
  --email $email --password $password `
  --tenant-id acme-industria --workspace-name SimuladorPiloto `
  --device-prefix sim-device --devices 10 --output devices-10.json
```

O script imprime o `workspaceId` e o comando do simulador ja com `--credentials-file` correto:

```powershell
python scripts/mqtt-device-simulator.py `
  --host localhost `
  --port 1883 `
  --tenant-id acme-industria `
  --workspace-id <workspaceId-impresso-acima> `
  --device-prefix sim-device `
  --devices 10 `
  --interval-seconds 1 `
  --messages-per-device 20 `
  --credentials-file devices-10.json
```

Para 50 ou 100 dispositivos, repita com `--devices 50`/`--devices 100` em ambos os scripts.
Detalhes e resultado de referencia com 100 devices em
[simulador-dispositivos-mqtt.md](simulador-dispositivos-mqtt.md).

## 13. Backup e restore

Backup dev:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/postgres-backup.ps1
```

Backup controlled-prod:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/postgres-backup.ps1 `
  -ComposeFile docker-compose.controlled-prod.yml
```

Restore:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/postgres-restore.ps1 `
  -InputPath backups/postgres/<arquivo>.sql
```

Teste restore em ambiente descartavel antes do piloto.

## 14. Validar rejeicoes e idempotencia

SQL:

```sql
select "ReceivedAtUtc", "ErrorType", "Reason", "Topic"
from telemetry_ingestion_rejections
order by "ReceivedAtUtc" desc
limit 100;
```

Duplicidade esperada por tenant/workspace/device/sequence:

```sql
select "TenantId","WorkspaceId","DeviceId","Sequence",count(*)
from telemetry_ingestion_records
group by 1,2,3,4
having count(*) > 1;
```

Resultado esperado: zero linhas.

## 15. Encerrar ambiente

Dev:

```powershell
docker compose down
```

Controlled-prod:

```powershell
docker compose -f docker-compose.controlled-prod.yml down
```

Use `down -v` somente quando quiser remover volumes persistentes.

## 16. Validacao minima antes do piloto

```powershell
dotnet test Fluxo.slnx
cd portal-web
npm audit --omit=dev
npm run build
cd ..
docker compose config
docker compose -f docker-compose.controlled-prod.yml config
git ls-files docker/mosquitto/data/mosquitto.db
```

O ultimo comando nao deve retornar nada.
