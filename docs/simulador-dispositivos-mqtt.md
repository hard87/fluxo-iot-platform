# Simulador MQTT de Dispositivos

O simulador em `scripts/mqtt-device-simulator.py` publica payloads compativeis com o worker atual:

```text
fluxo/tenants/<tenantId>/workspaces/<workspaceId>/devices/<deviceId>/telemetry
```

Payload gerado:

- `schemaVersion`
- `tenantId`
- `workspaceId`
- `deviceId`
- `messageType=telemetry`
- `timestampUtc`
- `sequence`
- `firmwareVersion`
- `metrics.temperature`, `humidity`, `battery`, `rssi`, `uptimeSec`

## Validar contrato sem broker

```powershell
python scripts/mqtt-device-simulator.py `
  --dry-run `
  --tenant-id acme-industria `
  --workspace-id 11111111-1111-1111-1111-111111111111
```

## Teste local com 10 dispositivos

```powershell
python scripts/mqtt-device-simulator.py `
  --host localhost `
  --port 1883 `
  --tenant-id acme-industria `
  --workspace-id 11111111-1111-1111-1111-111111111111 `
  --device-prefix sim-device `
  --devices 10 `
  --interval-seconds 1 `
  --messages-per-device 20 `
  --username-template "dev-acme-{device}" `
  --password-env FLUXO_SIMULATOR_MQTT_PASSWORD
```

## Teste TLS em 8883

```powershell
python scripts/mqtt-device-simulator.py `
  --host broker.fluxo.local `
  --port 8883 `
  --tls `
  --ca-file docker/mosquitto/certs/ca.crt `
  --tls-server-name broker.fluxo.local `
  --tenant-id acme-industria `
  --workspace-id 11111111-1111-1111-1111-111111111111 `
  --devices 10 `
  --interval-seconds 1 `
  --messages-per-device 20 `
  --username-template "dev-acme-{device}" `
  --password-env FLUXO_SIMULATOR_MQTT_PASSWORD
```

Use `--insecure-tls` somente em laboratorio quando estiver testando certificado self-signed sem CA configurada.

## Carga de referencia

- 10 dispositivos: validacao rapida de contrato.
- 50 dispositivos: validacao de estabilidade local.
- 100 dispositivos: validacao minima antes do piloto controlado.

Exemplo para 100 dispositivos:

```powershell
python scripts/mqtt-device-simulator.py `
  --host broker.fluxo.local `
  --port 8883 `
  --tls `
  --ca-file docker/mosquitto/certs/ca.crt `
  --tls-server-name broker.fluxo.local `
  --tenant-id acme-industria `
  --workspace-id 11111111-1111-1111-1111-111111111111 `
  --device-prefix sim-device `
  --devices 100 `
  --interval-seconds 1 `
  --messages-per-device 60 `
  --username-template "dev-acme-{device}" `
  --password-env FLUXO_SIMULATOR_MQTT_PASSWORD
```

O resumo final mostra mensagens publicadas, erros, reconexoes e tempo total. O simulador nao altera o firmware ESP32 de referencia.
