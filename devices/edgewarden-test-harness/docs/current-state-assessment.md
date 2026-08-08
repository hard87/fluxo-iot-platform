# Avaliação do estado atual — antes do harness de ensaio de 24h

Levantamento feito em 2026-08-08, via SSH direto no Pi (`junior@192.168.9.18`) e leitura do código
em `Fluxo/devices/pi-gateway-reference-node/`, seguindo a seção 36 da especificação do ensaio
("primeira ação do agente: inspecionar antes de implementar"). Não duplica o runbook de comunicação
já existente — ver
[`validacao-comunicacao-edgewarden.md`](../../pi-gateway-reference-node/docs/validacao-comunicacao-edgewarden.md)
para o histórico de incidentes e a checagem rápida de comunicação.

## 1. Hardware e SO

- Raspberry Pi 3 Model B Plus Rev 1.3.
- RAM: 905 MiB total. Em operação normal (Node-RED ativo, ~130 MiB RSS): ~716 MiB disponível.
- Swap: 904 MiB em `zram0` (comprimido em RAM) — não há swap em cartão SD, não conta para desgaste
  de escrita do cartão.
- Disco: `/dev/mmcblk0p2`, ext4, 29 GB total, 24 GB livres (14% usado).
- **Sem RTC** (`timedatectl status` → `RTC time: n/a`). Relógio ao ligar depende de
  `fake-hwclock`/NTP. Ver seção 3.1 do runbook de comunicação para o incidente real já causado por
  isso (~9h de apagão de telemetria em um boot).
- systemd 257.13-1~deb13u1 (Debian 13/trixie) — suporta `StateDirectory=`/`LogsDirectory=` em
  unidades de serviço (cria e ajusta dono automaticamente para o `User=` do serviço).
- Journald **persistente** (`/var/log/journal` existe) — sobrevive a reboot, pôde ser usado nesta
  sessão para reconstruir o incidente de boot de dias atrás.

## 2. Rede

- Interface real em uso: **`wlan0` (WiFi)**. `eth0` está `NO-CARRIER`/`DOWN` — não há cabo conectado.
- Rota para o Fluxo (`192.168.9.7`) sai por `wlan0`.
- MAC `wlan0`: `b8:27:eb:f2:8d:70` (OUI Raspberry Pi Foundation — usado nesta sessão para confirmar
  o IP do Pi via ARP quando o cache mDNS estava desatualizado).
- Estatísticas disponíveis sem dependência nova: `/sys/class/net/wlan0/statistics/{rx_bytes,tx_bytes}`
  e `/sys/class/net/wlan0/operstate`.
- **Implicação para o ensaio**: por ser WiFi (não Ethernet), quedas de rede são um cenário real e
  não hipotético neste gateway — reforça a prioridade dos cenários A/D/E da especificação do ensaio
  (perda de comunicação, Fluxo indisponível, reinício durante backlog).

## 3. Software já instalado (reuso, sem instalar nada novo)

| Ferramenta | Versão | Uso previsto no harness |
|---|---|---|
| Node.js | v22.23.2 | Runtime do harness (mesmo runtime do Node-RED já em produção) |
| `node:sqlite` (`node:sqlite`, módulo embutido) | experimental, mas funcional sem compilação nativa (testado nesta sessão: `CREATE TABLE` ok) | Estado do harness (`TestSession`, `Checkpoint`, `Event`) |
| npm | 10.9.8 | Não deve ser necessário — harness não terá dependências externas de npm |
| Python 3 | 3.13.5 | Já usado só pelo script inline de leitura do sensor HDC1080 (`gateway-spool.js`); não será usado no harness |
| `sqlite3` (CLI) | **não instalado** | Não é necessário — leitura/escrita via `node:sqlite`; para inspeção manual, usar `node -e` com `node:sqlite` em vez de instalar o pacote `sqlite3` do sistema |

## 4. Pipeline de telemetria existente (Fase 5, já provado — não será recriado)

Localização: `Fluxo/devices/pi-gateway-reference-node/`.

- **Captura e publicação**: `flow.json` (Node-RED) — inject a cada `FLUXO_PUBLISH_INTERVAL_SECONDS`
  (30s hoje) → `gateway-spool.js enqueue` → MQTT QoS 1 → aguarda PUBACK (`complete` node) →
  `gateway-spool.js ack`.
- **Persistência local**: `gateway-spool.js` — arquivo por mensagem em `state/spool/`, sequence
  monotônica em `state/sequence.json`/`sequence.backup.json` (checkpoint atômico:
  arquivo temporário + `fsync` + `rename`), lock com auto-recuperação após 30s de inatividade.
- **Já comprovado fisicamente na Fase 5** (`relatorio-fase-5-piloto-fisico-gateway-pi-2026-07-31.md`):
  reboot do Pi offline, restart do Node-RED offline, reconexão com replay em ordem, zero duplicatas.
- **Idempotência do lado do Fluxo**: `telemetry_ingestion_records` tem constraint única
  `UX_telemetry_ingestion_records_tenant_workspace_device_sequence` em
  `(TenantId, WorkspaceId, DeviceId, Sequence)` — confirmado no schema do Postgres nesta sessão
  (`\d telemetry_ingestion_records`). Reenviar a mesma `sequence` não gera linha duplicada.
- **Conclusão**: as seções 7, 8, 10, 17 (parcial) e 18 da especificação do ensaio já têm
  implementação real e testada. O harness novo **usa** esse pipeline (via `gateway-spool.js status`
  e o journal do `nodered`), não o substitui.

## 5. O que NÃO existe hoje (trabalho novo real)

- Qualquer noção de "sessão de ensaio", `test_id`, tempo acumulado entre reinícios, ou máquina de
  estados do ensaio em si — `gateway-spool.js` não sabe o que é um "teste de 24h", só sabe publicar
  e reter mensagens.
- Detecção de shutdown limpo vs sujo.
- Categorização de erros de rede/retry (DNS/timeout/recusado/auth) — hoje só existem os contadores
  `replayedMessages`/`droppedMessages`.
- Qualquer CLI de observabilidade (`status`/`events`/`report`).
- Qualquer geração de relatório final.

## 6. Motor de alertas do Fluxo (ADR-0002) — ainda não implementado

`docs/project-status.md` lista Produto Fase 3 (alertas com estado e delivery, incluindo
`WebhookDeliveryWorker` com proteção SSRF) como **"PRÓXIMA"**, não concluída. Confirmado lendo
`docs/adr/0002-alert-evaluation-state-and-delivery.md` — o mecanismo de webhook descrito lá (SSRF,
retry por status HTTP, at-least-once) não tem código correspondente ainda no worker.

**Implicação**: a seção 14 da especificação do ensaio (alertas externos via webhook/Telegram/e-mail)
não tem nada pronto para reaproveitar do lado do Fluxo, e implementá-la do zero significaria
adiantar trabalho da Fase 3 do produto só para um teste de gateway. Decisão: **fora do escopo do v1
do harness** — eventos ficam locais (SQLite + `edgewarden-test events`), sem entrega externa.

## 7. Permissões e usuário de serviço

- `nodered.service` e `fluxo-gateway-monitor.service` já rodam como `User=junior`, `Group=junior`.
- `junior` tem sudo sem senha (`sudo -n true` retornou OK) — usado nesta sessão só para
  `systemctl stop/start nodered`, não para nada destrutivo.
- `/var/lib` e `/var/log` são só do root (`drwxr-xr-x root root`) — o novo serviço deve usar
  `StateDirectory=edgewarden-test`/`LogsDirectory=edgewarden-test` na unit (systemd cria
  `/var/lib/edgewarden-test` e `/var/log/edgewarden-test` já com dono `junior:junior`), evitando
  `sudo mkdir`/`chown` manual.

## 8. Estado da captura no momento deste levantamento

`nodered.service` foi parado às ~15:23 UTC de 2026-08-08 (a pedido explícito do usuário, para
realinhar o ensaio) e restaurado logo em seguida para não deixar o piloto físico sem dados durante
o desenvolvimento do harness — nada no design do harness exige a captura parada para ser construído
e testado localmente.

## 9. Consequência direta para o plano de implementação

Ver [`implementation-plan.md`](implementation-plan.md) (a ser criado) e o plano de tarefas já
aprovado nesta sessão. Resumo: construir só a camada de orquestração do ensaio
(`edgewarden-test-harness/`, Node.js + `node:sqlite`, systemd próprio), reaproveitando
`gateway-spool.js`/`flow.json` com extensões pontuais (métricas de rede, `time_synchronized` — este
último já implementado em 2026-08-08, pendente de deploy).
