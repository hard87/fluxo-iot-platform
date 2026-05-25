# Fluxo - Guia do Piloto Real Controlado

Este guia consolida o fluxo minimo para rodar um piloto com dispositivos reais, com autenticacao MQTT por device, rastreabilidade de ingestao e operacao assistida.

## 1. Pre-requisitos

- .NET SDK 10
- PostgreSQL acessivel pela API e pelo Worker
- Broker MQTT Mosquitto
- `mosquitto_pub` e `mosquitto_passwd` disponiveis

## 2. Configuracao por ambiente (sem segredo versionado)

### API

Variaveis recomendadas:

```powershell
$env:ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=fluxo_db;Username=fluxo;Password=fluxo"
$env:Authentication__Enabled="false"
$env:DeviceStatus__OfflineAfterSeconds="120"
```

### Worker

```powershell
$env:ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=fluxo_db;Username=fluxo;Password=fluxo"
$env:MqttIngestion__BrokerHost="localhost"
$env:MqttIngestion__BrokerPort="1883"
$env:MqttIngestion__TopicFilter="fluxo/tenants/+/workspaces/+/devices/+/telemetry"
$env:MqttIngestion__ChannelCapacity="5000"
$env:MqttIngestion__ProcessingConcurrency="4"
```

## 3. Subir banco e aplicar migration

```powershell
dotnet ef database update `
  --project src/Fluxo.Infrastructure/Fluxo.Infrastructure.csproj `
  --startup-project src/Fluxo.Api/Fluxo.Api.csproj
```

Migration desta etapa:

- `20260524235507_AddDeviceProvisioningAndOperationalStatus`

## 4. Subir API e Worker

Terminal 1:

```powershell
dotnet run --project src/Fluxo.Api/Fluxo.Api.csproj
```

Terminal 2:

```powershell
dotnet run --project src/Fluxo.Worker.Ingestion/Fluxo.Worker.Ingestion.csproj
```

Health da API:

```powershell
curl http://localhost:5000/health
```

## 5. Subir Mosquitto com autenticacao por device

1. Use `docker/mosquitto/mosquitto.conf` (`allow_anonymous false`).
2. Provisione primeiro o device na API (proximo passo) para obter:
   - `credentialUsername`
   - `provisioningSecret`
   - `mqttPublishTopic`
3. Gere usuario no arquivo de senha:
   - primeira vez: `mosquitto_passwd -c passwords <credentialUsername>`
   - seguintes: `mosquitto_passwd passwords <credentialUsername>`
4. ACL minima por device:

```text
user <credentialUsername>
topic write <mqttPublishTopic>
```

5. Reinicie broker.

## 6. Provisionar novo dispositivo via API

```powershell
$body = @{
  tenantId = "acme-industria"
  workspaceId = "11111111-1111-1111-1111-111111111111"
  name = "ESP32 Lab 01"
  identifier = "esp32-lab-01"
  category = "Sensor"
} | ConvertTo-Json

curl -Method POST `
  -Uri http://localhost:5000/api/provisioning/devices `
  -ContentType "application/json" `
  -Body $body
```

Resposta importante:

- `deviceId`
- `credentialUsername`
- `provisioningSecret` (mostrar uma vez, nao persistido em plaintext)
- `mqttPublishTopic`

## 7. Configurar firmware sem segredo versionado

1. Copie `devices/esp32-reference-node/main/app_config.local.example.h` para `app_config.local.h`.
2. Preencha:
   - Wi-Fi
   - broker
   - `APP_MQTT_USERNAME`
   - `APP_MQTT_PASSWORD` (valor de `provisioningSecret`)
3. Nunca commitar `app_config.local.h`.

## 8. Publicar telemetria de teste

```powershell
mosquitto_pub -h localhost -p 1883 `
  -u "<credentialUsername>" -P "<provisioningSecret>" `
  -t "fluxo/tenants/acme-industria/workspaces/11111111-1111-1111-1111-111111111111/devices/esp32-lab-01/telemetry" `
  -m '{"schemaVersion":"1.0","tenantId":"acme-industria","workspaceId":"11111111-1111-1111-1111-111111111111","deviceId":"esp32-lab-01","messageType":"telemetry","timestampUtc":"2026-05-24T12:00:00Z","sequence":1,"metrics":{"temperature":24.5,"humidity":60.2}}'
```

## 9. Consultar dados recebidos e status operacional

1. Detalhe de provisionamento/status:

```powershell
curl http://localhost:5000/api/provisioning/devices/<deviceId>
```

2. Telemetria por device:

```powershell
curl http://localhost:5000/api/devices/<deviceId>/telemetry
```

## 10. Validar rejeicoes de ingestao

SQL:

```sql
select "ReceivedAtUtc", "ErrorType", "Reason", "Topic"
from telemetry_ingestion_rejections
order by "ReceivedAtUtc" desc
limit 100;
```

Duplicidade esperada (idempotencia por tenant/workspace/device/sequence):

```sql
select "TenantId","WorkspaceId","DeviceId","Sequence",count(*)
from telemetry_ingestion_records
group by 1,2,3,4
having count(*) > 1;
```

Resultado esperado: zero linhas.

## 11. Observabilidade operacional (metricas preparadas)

No Worker (`Meter`: `Fluxo.Worker.Ingestion`):

- `fluxo_ingestion_messages_received`
- `fluxo_ingestion_messages_enqueued`
- `fluxo_ingestion_messages_dequeued`
- `fluxo_ingestion_messages_persisted`
- `fluxo_ingestion_messages_rejected`
- `fluxo_ingestion_messages_duplicate`
- `fluxo_ingestion_failures_database`
- `fluxo_ingestion_failures_transient`
- `fluxo_ingestion_failures_processing`
- `fluxo_ingestion_buffer_size` (gauge)
- `fluxo_ingestion_mqtt_reconnections`
- `fluxo_ingestion_processing_duration_ms` (histogram)

Isso ja e compativel com pipeline OpenTelemetry/Prometheus.

## 12. Retencao e particionamento (politica inicial do piloto)

Carga de referencia:

- 100 msg/s medio
- 8.640.000 mensagens/dia

Politica inicial recomendada para piloto:

1. Manter 14 dias de `telemetry_ingestion_records` no banco principal.
2. Manter `telemetry_ingestion_rejections` por 30 dias.
3. Rodar limpeza diaria por `ReceivedAtUtc`.
4. Exportar bruto para storage frio antes da limpeza, se necessario.

Preparacao de particionamento:

- A migration atual inclui comentario tecnico em `telemetry_ingestion_records` indicando `ReceivedAtUtc` como chave de particionamento futura (`RANGE` temporal).

## 13. Diagnostico rapido de falhas

1. `401/Not authorized` no MQTT: usuario/senha ou ACL incorretos.
2. Mensagem nao aparece na API:
   - validar se topic bate com payload;
   - validar se device esta provisionado e ativo;
   - consultar `telemetry_ingestion_rejections`.
3. Backlog crescendo:
   - monitorar `fluxo_ingestion_buffer_size`;
   - revisar `ProcessingConcurrency`, latencia de banco e retries.
4. Status `offline`:
   - sem telemetria recente acima de `DeviceStatus:OfflineAfterSeconds`.

## 14. Validacao automatizada

Rodar suite completa:

```powershell
dotnet test Fluxo.slnx
```

Teste de integracao com PostgreSQL real (opt-in):

```powershell
$env:FLUXO_TESTS_POSTGRES_ADMIN_CONNECTION="Host=localhost;Port=5432;Database=postgres;Username=<user>;Password=<pass>"
dotnet test tests/Fluxo.IntegrationTests/Fluxo.IntegrationTests.csproj --filter "FullyQualifiedName~PostgreSql"
```
