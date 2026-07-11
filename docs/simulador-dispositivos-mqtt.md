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

## Credenciais: cada device precisa da sua propria

Desde que o broker passou a usar o plugin `dynamic-security`
(ver [mqtt-tls-e-credenciais.md](mqtt-tls-e-credenciais.md)), cada device tem usuario **e
senha proprios** gerados no provisionamento — nao existe mais uma senha compartilhada por
`--username-template`/`--password`. Para simular N devices reais, provisione-os primeiro pela
API com `scripts/provision-simulated-devices.py`, que gera um `--credentials-file` consumivel
pelo simulador:

```powershell
python scripts/provision-simulated-devices.py `
  --api http://localhost:5000 `
  --email loadtest@example.test `
  --password "TrocaEssaSenha123!" `
  --tenant-id acme-industria `
  --workspace-name LoadTest `
  --device-prefix sim-device `
  --devices 10 `
  --output devices-10.json
```

O script imprime o comando pronto do simulador ao final (com `--workspace-id` e
`--credentials-file` corretos).

## Teste local com 10 dispositivos

```powershell
python scripts/mqtt-device-simulator.py `
  --host localhost `
  --port 1883 `
  --tenant-id acme-industria `
  --workspace-id <workspaceId-impresso-pelo-script-de-provisionamento> `
  --device-prefix sim-device `
  --devices 10 `
  --interval-seconds 1 `
  --messages-per-device 20 `
  --credentials-file devices-10.json
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
  --workspace-id <workspaceId> `
  --devices 10 `
  --interval-seconds 1 `
  --messages-per-device 20 `
  --credentials-file devices-10.json
```

Use `--insecure-tls` somente em laboratorio quando estiver testando certificado self-signed sem CA configurada.

## Carga de referencia

- 10 dispositivos: validacao rapida de contrato.
- 50 dispositivos: validacao de estabilidade local.
- 100 dispositivos: validacao minima antes do piloto controlado.

Exemplo para 100 dispositivos sustentando ~100 msg/s por 5 minutos (1 msg/s por device):

```powershell
python scripts/provision-simulated-devices.py `
  --email loadtest@example.test --password "TrocaEssaSenha123!" `
  --tenant-id acme-industria --workspace-name LoadTest `
  --device-prefix sim-device --devices 100 --output devices-100.json

python scripts/mqtt-device-simulator.py `
  --host localhost --port 1883 `
  --tenant-id acme-industria --workspace-id <workspaceId> `
  --device-prefix sim-device --devices 100 `
  --interval-seconds 1 --messages-per-device 300 `
  --credentials-file devices-100.json
```

Resultado de referencia (2026-07-11, ambiente local Docker Desktop/Windows): 100 devices,
30000 mensagens publicadas em ~303s, 0 erros/reconexoes no simulador, 30000/30000 persistidas
no worker, 0 rejeicoes, todos os devices com exatamente 300 mensagens sequenciais (sem
gaps/duplicatas). Detalhes em `docs/sprint-2/sprint-2-technical-document.md`.

O resumo final mostra mensagens publicadas, erros, reconexoes e tempo total. O simulador nao altera o firmware ESP32 de referencia.
