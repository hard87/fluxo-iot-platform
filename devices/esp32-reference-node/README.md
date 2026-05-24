# esp32-reference-node

Firmware de referencia (Sprint 2) para validar ingestao ponta a ponta no Fluxo.

## O que faz
- Conecta em Wi-Fi (STA).
- Conecta em broker MQTT.
- Publica telemetria a cada 10s no topico da plataforma:
  - `fluxo/tenants/acme-industria/workspaces/11111111-1111-1111-1111-111111111111/devices/esp32-lab-01/telemetry`
- Envia `sequence` incremental.
- Envia `rssi` real do Wi-Fi.
- Envia `temperature`, `humidity` e `battery` simulados.

## Configuracao
Edite [`main/app_config.h`](./main/app_config.h):
- `APP_WIFI_SSID`
- `APP_WIFI_PASSWORD`
- `APP_MQTT_BROKER_URI`

Seguranca:
- nao versione credenciais reais no repositorio;
- use valores locais para laboratorio e mantenha-os fora de commit.

Importante: para ESP32, `localhost` nao funciona como broker remoto.
Use o IP da maquina onde o broker esta rodando (ex.: `mqtt://192.168.1.10:1883`).

## Timestamp UTC
O firmware tenta sincronizar UTC via NTP (`pool.ntp.org`) ao iniciar.
Se nao sincronizar no timeout inicial, continua publicando com aviso em log ate sincronizar.

## Build e flash
```bash
idf.py set-target esp32
idf.py build
idf.py -p <PORTA_SERIAL> flash monitor
```
