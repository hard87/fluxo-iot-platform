# Fluxo Pi Gateway Reference Node

Gateway físico de referência para Raspberry Pi com Node-RED. Ele publica diagnóstico real do
Linux no Telemetry Schema V2 e mantém `sequence` e mensagens pendentes em disco. Não simula
sensor: sensores locais serão acrescentados somente quando houver hardware identificado.

## Arquitetura

O `edgewarden` é um único `DeviceCategory.Gateway`. O flow gera uma mensagem, reserva e persiste
seu `sequence`, grava a mensagem no spool e só então tenta publicar. O node MQTT nativo usa TLS e
QoS 1. O node `complete`, acionado pelo callback de publicação/PUBACK, remove o arquivo do spool.
Se houver falha entre PUBACK e remoção, a mensagem será reenviada com o mesmo `sequence`; a
idempotência do backend absorve a duplicata.

O tópico é sempre:

```text
fluxo/tenants/<tenantId>/workspaces/<workspaceId>/devices/<deviceIdentifier>/telemetry
```

## Pré-requisitos

- Raspberry Pi Linux com Node.js 18+ e Node-RED 5.x;
- node MQTT nativo do Node-RED (nenhum pacote adicional);
- saída TCP para o broker em `8883`;
- device provisionado pela API com `category: 3`;
- CA do broker copiada para o Pi.

## Provisionamento e configuração

Siga `docs/piloto-real-controlado.md`, usando o endpoint
`POST /api/workspaces/{workspaceId}/devices/provision` e `category: 3`. Copie
`environment.example` para `/home/junior/.node-red/environment`, substitua apenas localmente os
placeholders e aplique:

```sh
chmod 600 /home/junior/.node-red/environment
chown junior:junior /home/junior/.node-red/environment
```

O serviço deve continuar referenciando
`EnvironmentFile=-/home/junior/.node-red/environment`. Nunca grave a senha MQTT, JWT, chave
privada ou o arquivo `environment` real no repositório.

O deploy também instala e habilita `fluxo-gateway-monitor.service`. Esse monitor inicia em todo
boot, registra diagnósticos no journal a cada 15 minutos e reinicia automaticamente em caso de
falha, sem interferir no ciclo de captura/publicação do Node-RED. O intervalo pode ser ajustado com
`FLUXO_MONITOR_INTERVAL_SECONDS`, com mínimo de 60 segundos.

`FLUXO_MQTT_PORT` precisa ser `8883`; o validador rejeita qualquer fallback sem TLS. O hostname ou
IP configurado deve existir no SAN do certificado. `FLUXO_MQTT_CA_PATH` aponta para a CA pública,
não para uma chave privada.

## Instalação e importação do flow

Copie esta pasta para o Pi e execute como `junior`:

```sh
chmod +x scripts/*.sh scripts/*.js
./scripts/deploy.sh
```

O deploy valida o ambiente, instala os arquivos em
`/home/junior/.node-red/fluxo-gateway`, para brevemente o serviço e atualiza o flow e o credential
store criptografado. Isso preserva o `adminAuth` existente e os outros flows. Reinicie ou valide o
serviço com:

```sh
sudo systemctl restart nodered
systemctl status nodered --no-pager
journalctl -u nodered --since "30 minutes ago" --no-pager
```

## Telemetria

O payload canônico contém `schemaVersion: 2`, `sequence`, `occurredAtUtc` e métricas reais:

- `gateway.uptime_sec`;
- `gateway.cpu_temperature_c`, quando disponível;
- `gateway.memory_used_percent`;
- `gateway.disk_used_percent`;
- `gateway.load_1m`;
- `gateway.queue_depth`;
- `gateway.mqtt_connected`;
- `gateway.replayed_messages`;
- `gateway.dropped_messages`;
- `gateway.time_synchronized` — `true`/`false` conforme `timedatectl show -p NTPSynchronized`;
  omitida se a checagem falhar. O Pi não tem RTC (`RTC time: n/a`); esta métrica existe para que um
  incidente de sincronização NTP atrasada seja visível nos próprios dados, sem exigir SSH manual no
  Pi. Ver `docs/validacao-comunicacao-edgewarden.md` (runbook do lado plataforma) seção sobre o
  incidente de 2026-08-08 que motivou esta métrica.
- `gateway.network_interface_up`, `gateway.network_rx_bytes`, `gateway.network_tx_bytes` — lidas de
  `/sys/class/net/$FLUXO_NET_IFACE/{operstate,statistics/rx_bytes,statistics/tx_bytes}`.
  `FLUXO_NET_IFACE` tem default `wlan0` porque este Pi usa WiFi (`eth0` está `NO-CARRIER` neste
  gateway); ajuste a variável se o hardware mudar para Ethernet. Omitidas se a leitura falhar.

As `MetricDefinition` são descobertas pelo pipeline V2 e estabilizam seu tipo após a primeira
mensagem aceita.

## Persistência, limites e recuperação

O estado fica em `/home/junior/.node-red/fluxo-gateway/state` (modo `700`). Cada mensagem é um
arquivo, ordenado por `sequence`, e não contém credenciais. `sequence.json` e
`sequence.backup.json` são high-water marks gravados atomicamente antes da mensagem. Escritas usam
arquivo temporário, `fsync` e `rename`.

Defaults: 10.000 mensagens, 50 MiB, idade máxima de 7 dias, replay serial a cada 250 ms e backoff
de 5 s. Ao atingir quantidade/tamanho, a mensagem mais nova é descartada e o contador/log é
atualizado; mensagens expiradas são removidas da mais antiga para a mais nova. Consulte sem
mostrar segredos:

```sh
./scripts/diagnostics.sh
node scripts/gateway-spool.js status
```

As negações de CA, credencial e ACL podem ser verificadas sem revelar valores com
`scripts/test-negative-mqtt.sh`.

Consulte o monitor contínuo sem criar arquivos de log adicionais:

```sh
systemctl status fluxo-gateway-monitor --no-pager
journalctl -u fluxo-gateway-monitor --since "1 hour ago" --no-pager
```

Se um dos checkpoints estiver corrompido, o maior valor válido entre checkpoint, backup e spool é
usado. Se ambos estiverem corrompidos e não houver mensagem no spool, o processo falha fechado:
recupere manualmente um sequence maior que o último persistido no backend. Não apague os
checkpoints para “resolver” uma colisão.

## Teste de desconexão e reboot

1. confirme fila vazia e telemetria chegando;
2. interrompa somente o acesso ao broker, sem alterar o firewall do Pi;
3. aguarde mensagens e confirme crescimento em `gateway-spool.js status`;
4. reinicie Node-RED e depois o Pi ainda offline;
5. confirme a mesma fila e sequence crescente;
6. restaure o broker e acompanhe replay/ingestão até fila zero;
7. confirme no backend ausência de colisões para mensagens diferentes.

Para o ensaio de 24 horas, registre início/fim, RSS do Node-RED, carga, temperatura, disco, maior
fila, contadores de replay/descarte e rejeições do backend. Não declare sucesso antes das 24 horas
completas. O coletor local pode ser iniciado em uma sessão persistente com:

```sh
nohup ./scripts/monitor-24h.sh >/home/junior/.node-red/fluxo-gateway/monitoring/launcher.log 2>&1 &
```

## Segurança e limitações

Nenhuma porta de entrada nova é necessária. A UI do Node-RED pode permanecer bloqueada pelo UFW;
o deploy usa `127.0.0.1:1880` no próprio Pi. A implementação atual representa sensores como
submétricas do Gateway; devices filhos com identidade/credencial próprias dependem de futura
delegação MQTT ou gerenciamento de múltiplas credenciais. Veja o ADR-0004 e
`docs/troubleshooting.md`.
