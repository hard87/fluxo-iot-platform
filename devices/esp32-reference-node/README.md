# esp32-reference-node

Firmware de referencia (Sprint 2) para validar ingestao ponta a ponta no Fluxo.

## O que faz
- Conecta em Wi-Fi (STA).
- Conecta em broker MQTT com TLS (`mqtts://`).
- Publica telemetria a cada 10s no topico da plataforma:
  - `fluxo/tenants/acme-industria/workspaces/11111111-1111-1111-1111-111111111111/devices/esp32-lab-01/telemetry`
- Envia `sequence` incremental com checkpoint em NVS (continua apos reboot).
- Envia `rssi` real do Wi-Fi.
- Envia `temperature`, `humidity` e `battery` simulados.

## Configuracao
Fluxo recomendado para nao versionar segredos:
1. Copie `main/app_config.local.example.h` para `main/app_config.local.h`.
2. Preencha credenciais locais:
   - `APP_WIFI_SSID`
   - `APP_WIFI_PASSWORD`
   - `APP_MQTT_BROKER_URI` (usar `mqtts://<host>:8883`)
   - `APP_MQTT_USERNAME`
   - `APP_MQTT_PASSWORD` (segredo retornado no provisionamento da API)
   - `APP_MQTT_CA_CERT_PEM` (conteudo da `ca.crt` em string C)
3. Mantenha `app_config.local.h` fora de versionamento (ja ignorado no `.gitignore`).

`app_config.h` fica apenas com placeholders e defaults sem segredo real.

Seguranca:
- nao versione credenciais reais no repositorio;
- use TLS com certificado valido do broker;
- nao use fallback para `mqtt://` sem criptografia.

Importante: o host usado em `APP_MQTT_BROKER_URI` deve ser o mesmo nome usado no certificado TLS do broker.

## Certificado CA para o firmware
1. Gere os certificados locais do Mosquitto:

```powershell
powershell -ExecutionPolicy Bypass -File docker/mosquitto/scripts/generate-local-certs.ps1 -CommonName broker.fluxo.local
```

2. Converta `docker/mosquitto/certs/ca.crt` para formato com `\n` e cole em `APP_MQTT_CA_CERT_PEM`:

```powershell
$ca = (Get-Content docker/mosquitto/certs/ca.crt -Raw).Replace("`r","").Replace("`n","\n")
$ca
```

3. Garanta que `APP_MQTT_BROKER_URI` usa o mesmo host do `-CommonName` (ex.: `mqtts://broker.fluxo.local:8883`).

## Timestamp UTC
O firmware tenta sincronizar UTC via NTP (`pool.ntp.org`) ao iniciar.
Se nao sincronizar no timeout inicial, continua publicando com aviso em log ate sincronizar.

## Build e flash
```bash
idf.py set-target esp32
idf.py build
idf.py -p <PORTA_SERIAL> flash monitor
```
