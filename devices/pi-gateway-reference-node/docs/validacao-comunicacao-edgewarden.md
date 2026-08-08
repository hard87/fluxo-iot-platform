# Validação de comunicação: Gateway `edgewarden` ↔ Plataforma Fluxo

Runbook vivo. Objetivo: permitir checar rapidamente se `edgewarden` está se comunicando com o
Fluxo **sem repetir uma varredura completa**, e registrar o histórico de problemas já investigados
para não reabri-los do zero. Atualize este arquivo (não crie um novo por data) sempre que rodar uma
nova validação ou resolver algo listado em "Pendências conhecidas".

## 1. Topologia relevante

```
Raspberry Pi "edgewarden" (Node-RED)
   -> MQTT/TLS 8883 (client id fluxo-edgewarden, user dev-gateway-pilot-91f6a6e1be-66451978-edgewarden)
   -> fluxo-mosquitto (dynamic-security, sem password_file/acl_file estáticos)
   -> fluxo-worker-ingestion (TelemetryIngestionProcessor, subscriber MQTT em 1883 interno)
   -> fluxo-postgres (fluxo_db) -> tabelas devices, telemetry_ingestion_records, telemetry_ingestion_rejections
   -> fluxo-api / portal (leitura via Telemetry Query API / Explorer)
```

- Tenant: `gateway-pilot-91f6a6e1be`
- Workspace: `66451978-ec5f-4c21-91b6-48bdde621eca`
- Device correto/ativo: `edgewarden` — id `9349f958-2805-41f8-a323-f08c7a161c1b`, categoria `Gateway`
- Tópico canônico:
  `fluxo/tenants/gateway-pilot-91f6a6e1be/workspaces/66451978-ec5f-4c21-91b6-48bdde621eca/devices/edgewarden/telemetry`
- Containers (nomes fixos em `docker-compose.yml`): `fluxo-postgres`, `fluxo-mosquitto`, `fluxo-api`,
  `fluxo-worker-ingestion`, `fluxo-portal-web`.
- IP LAN atual do Pi (confirmado 2026-08-08): `192.168.9.18`, MAC `b8-27-eb-f2-8d-70` (OUI da
  Raspberry Pi Foundation). Ver seção 2.1 para como reconfirmar — o IP muda por DHCP e não deve ser
  hardcoded em automação.
- Documentos relacionados: [ADR-0004](../../../docs/adr/0004-pi-gateway-store-and-forward.md),
  [Relatório Fase 5](../../../docs/handoff/relatorio-fase-5-piloto-fisico-gateway-pi-2026-07-31.md),
  [project-status.md](../../../docs/project-status.md), [troubleshooting.md](troubleshooting.md)
  (esse último é para problemas no lado do Pi/Node-RED; este arquivo é para validar a comunicação
  ponta a ponta a partir do host que roda o stack Fluxo).

## 2. Checagem rápida (rodar isto primeiro; leva ~1 minuto)

Rodar a partir do host Windows que hospeda o `docker-compose.yml` do Fluxo (não requer acesso SSH
ao Pi).

```bash
# 1. Containers de pé?
docker ps --format "{{.Names}}: {{.Status}}"

# 2. edgewarden conectado no broker agora? (procurar a última linha "New client connected ... fluxo-edgewarden"
#    sem um "disconnected" depois dela)
docker exec fluxo-mosquitto sh -c "tail -n 60 /mosquitto/log/mosquitto.log" | grep -i edgewarden

# 3. Mensagens sendo persistidas nos últimos minutos, sem erro?
docker logs fluxo-worker-ingestion --since 10m 2>&1 | grep -i "edgewarden\|reject\|fail:"

# 4. Estado oficial do device no banco (LastContactAtUtc deve estar a poucos segundos/minutos de "agora")
docker exec fluxo-postgres psql -U fluxo -d fluxo_db -c "SELECT \"Id\",\"Identifier\",\"IsActive\",\"LastContactAtUtc\",\"LastTelemetrySequence\", now() AT TIME ZONE 'UTC' AS db_now_utc FROM devices WHERE \"Identifier\"='edgewarden';"

# 5. Rejeições recentes (deve retornar 0 linhas em operação normal)
docker exec fluxo-postgres psql -U fluxo -d fluxo_db -c "SELECT \"ErrorType\",\"Reason\",count(*) FROM telemetry_ingestion_rejections WHERE \"ReceivedAtUtc\" > now() - interval '2 hours' AND \"Topic\" LIKE '%edgewarden%' GROUP BY 1,2;"
```

Nota Git Bash/PowerShell: caminhos absolutos tipo `/mosquitto/log/...` são reescritos pelo Git
Bash quando passados direto ao `docker exec`; por isso o comando 2 usa
`docker exec fluxo-mosquitto sh -c "..."` (o path só existe dentro do container, então não é
reescrito).

### Critério de saudável

- Os 5 containers `Up` e `(healthy)` quando aplicável.
- Última conexão `fluxo-edgewarden` no log do mosquitto sem `disconnected` subsequente.
- `LastContactAtUtc` do device no banco a no máximo ~1 min de `db_now_utc` (o flow publica a cada
  ~30s em operação normal).
- `LastTelemetrySequence` crescente entre duas checagens.
- Zero linhas na query de rejeições.

Se todos os critérios acima passarem, **não é preciso investigar mais** — a comunicação está OK.
Só escale para uma varredura completa (logs extensos, psql detalhado, revisão do `flow.json`) se
algum desses sinais falhar. Ver seção 5.

### 2.1 Descobrir/reconfirmar o IP LAN atual do Pi

Não confie no `edgewarden.local` do cache mDNS do Windows para IPv4 — ele fica desatualizado
quando o DHCP realoca o IP do Pi (ver gotcha abaixo). Para achar o IP real e atual, a fonte mais
confiável é a conexão TCP estabelecida com o broker, cruzada com o ARP:

```powershell
# Conexão ativa na porta MQTT/TLS — RemoteAddress é o IP real do Pi agora
Get-NetTCPConnection -LocalPort 8883 | Where-Object State -eq Established

# Confirma que o IP pertence a um Raspberry Pi (MAC começa com b8:27:eb, OUI da Raspberry Pi
# Foundation; Pi 4/400/CM4 podem usar outro OUI, dc:a6:32 ou e4:5f:01)
arp -a | Select-String "192.168.9."
```

Se não houver conexão `Established` (ex.: gateway offline no momento), use o IPv6 via mDNS como
alternativa — o Windows costuma manter o registro `AAAA` mais atualizado que o `A`:

```powershell
ping edgewarden        # sem ".local" — normalmente resolve AAAA (IPv6) via mDNS
ping -6 edgewarden.local
```

**Gotcha confirmado em 2026-08-08:** `Get-DnsClientCache` tinha um registro `A` para
`edgewarden.local` apontando para `192.168.9.13`, que respondia "host de destino inacessível" — IP
desatualizado, o Pi não estava mais lá. O IP real, confirmado por conexão TCP `Established` + ARP +
MAC, era `192.168.9.18`. Não conclua que o gateway está offline só porque
`ping edgewarden.local` (IPv4) falha — confirme pela conexão TCP ativa ou pelo IPv6 antes de
escalar para investigação de falha real (seção 6).

## 3. Última validação confirmada

- Data/hora: 2026-08-08, ~11:53–11:59 UTC.
- `edgewarden` conectado ao broker desde `11:53:52 UTC`, TLS 1.3
  (`TLS_AES_256_GCM_SHA384`), sem quedas depois disso.
- Sequence avançando ~1 mensagem/30s, chegou a `3557` às `11:58:55 UTC`; `LastContactAtUtc` do
  device igual ao momento da checagem.
- Zero rejeições nas últimas 2h e zero rejeições históricas para o device `9349f958-...`.
- Houve uma queda/reconexão do broker por volta de `11:51–11:53 UTC` (motivo: restart do container
  `fluxo-mosquitto`, não do lado do Pi). A fila local subiu a 13 mensagens em spool e foi drenada em
  ordem, sem duplicatas, após a reconexão — comportamento de store-and-forward funcionando como
  desenhado no ADR-0004.
- Payload mais recente já inclui métricas de sensor físico (`environment.temperature_c`,
  `environment.humidity_percent`), além das 9 métricas de diagnóstico do Gateway descritas no
  [README](../README.md). Isso é mais recente que o Relatório da Fase 5 (31/07/2026), que registrava
  nenhum sensor físico conectado — atualizar esse relatório/pendência se for revisado de novo.

**Conclusão em 2026-08-08: comunicação `edgewarden` ↔ Fluxo validada e saudável**, com as duas
pendências abaixo (não bloqueiam a comunicação, mas devem ser tratadas).

## 3.1 Incidente investigado: apagão de telemetria de ~9h após reboot (boot de 2026-08-07 23:42 -03)

Investigado via SSH em 2026-08-08 a partir de uma suspeita do usuário de que o script "se perde" e
publica métricas com rótulo de data/hora diferente da hora real. **A suspeita levou a um incidente
real e documentado abaixo — não é hipotética.**

**O que aconteceu, na ordem, com evidência:**

1. `edgewarden` reiniciou em `2026-08-07 23:42:06 -03`. Este Pi **não tem RTC** (`timedatectl status`
   mostra `RTC time: n/a`) — o relógio ao ligar depende de `fake-hwclock`/NTP, não de bateria.
2. `nodered.service` subiu imediatamente (`23:42:14 -03`), sem esperar sincronização NTP — o
   `ExecStartPre` que faria essa espera existe no unit file mas está **comentado**:
   `/usr/lib/systemd/system/nodered.service` (`# uncomment next line if you need to wait for time
   sync before starting`).
3. Em `23:42:35 -03`, `gateway-spool.js enqueue`/`next` falharam com `gateway spool is busy` — um
   lock (`state/.lock`) ficou preso, provavelmente de um encerramento anterior não limpo. O
   autorrecovery do lock (`gateway-spool.js`, função `withLock`) só dispara se uma **nova tentativa**
   encontrar o lock com mais de 30s de idade — ou seja, o autorrecovery em si não explica 9h de
   espera; ele só prova que **nenhuma nova tentativa aconteceu** nesse intervalo todo.
4. Nada mais foi logado por `nodered` (nem novo `enqueue`, nem nova tentativa de reconexão MQTT, que
   normalmente tenta a cada ~45s) entre `23:42:35 -03` e `08:48:30 -03` — **9h06min sem nenhuma
   atividade**, incluindo `fluxo-gateway-monitor.service`, que também só chegou a `active` às
   `08:48:38 -03` (8s depois), apesar de `WantedBy=multi-user.target` (deveria subir cedo no boot).
5. Timesyncd só contatou um servidor NTP às `08:48:30 -03`
   (`journalctl -u systemd-timesyncd`: `Initial clock synchronization to Sat 2026-08-08 08:48:30`).
   A coincidência de horário com o item 4 não é acaso.
6. Sequence `3536` (gerada `02:44:02 UTC`) é seguida, **sem nenhum salto de sequence**, por `3537`
   (gerada `11:48:55 UTC`) — prova de que nada foi gerado nesse meio-tempo, não que houve um "salto
   de relógio" isolado.

**Conclusão sobre a hipótese original (relógio errado nas métricas):** o relógio do Pi, checado nos
dois extremos do intervalo (mensagens 3532–3534 antes e 3537+ depois), bateu com o `ReceivedAtUtc`
do backend nos dois casos — ou seja, **o relógio em si não estava com valor absurdo**. O sintoma real
é mais sério: **todo o pipeline do gateway (flow principal, reconexão MQTT e o serviço systemd de
monitoramento) ficou parado por ~9h após esse boot específico**, e só voltou exatamente quando a
rede/NTP se restabeleceu — evidência forte de que a causa raiz foi **rede indisponível por ~9h após
o boot** (WiFi/Ethernet do Pi não subiu), não um bug isolado de timestamp. NTP, MQTT e o
`fluxo-gateway-monitor.service` são todos consumidores dessa mesma rede, o que explica os três
ficarem represados juntos e liberarem juntos.

**Mitigação aplicada nesta sessão (2026-08-08):** `gateway-spool.js` (`diagnostics()`) agora inclui
`gateway.time_synchronized` (via `timedatectl show -p NTPSynchronized --value`) em toda mensagem de
telemetria — ver seção 4 item 4 e o [README](../README.md#telemetria). Isso não impede o incidente,
mas o torna visível diretamente nos dados (filtrar telemetria com `time_synchronized=false`) sem
precisar de uma investigação SSH como esta. **Ainda não implantado no Pi** — requer rodar
`./scripts/deploy.sh` em `edgewarden` (para instalar o `gateway-spool.js` atualizado) e reiniciar o
Node-RED; ver seção 4 item 4 para status de implantação.

**Não implementado, avaliar depois:** descomentar o `ExecStartPre` de espera por NTP no
`nodered.service` bloquearia a subida do Node-RED (e portanto de toda a operação do Gateway,
incluindo o dashboard local) até a rede/NTP responder — trocaria "métrica com timestamp suspeito por
alguns minutos" por "gateway inteiro fora do ar até a rede voltar". Dado que o relógio pós-boot
provou ser confiável mesmo sem essa espera (item de conclusão acima), a métrica de telemetria
(`gateway.time_synchronized`) foi priorizada sobre bloquear o startup. Reabrir essa decisão se um
incidente futuro mostrar o relógio genuinamente errado (não apenas atrasado) logo após o boot.

## 4. Pendências conhecidas (ainda não resolvidas em 2026-08-08)

1. **Métrica `gateway.mqtt_connected` sempre `false` nos payloads, mesmo com a conexão TLS
   comprovadamente ativa.** Junto com isso, `gateway.replayed_messages` incrementa 1:1 a cada nova
   mensagem publicada (não só durante outages reais) e `gateway.queue_depth` nunca chega a `0` em
   regime estável (fica em `1`). Hipótese: o flow do Node-RED (`flow.json`) está lendo o estado de
   conexão/replay antes do evento de connect, ou está tratando toda publicação como se viesse do
   spool. **Isso não é uma falha de comunicação** — as mensagens chegam, são aceitas e a sequence
   avança normalmente — é um bug de auto-diagnóstico no lado do device. **Ainda não corrigido no
   `flow.json`** (2026-08-08: o novo `edgewarden-test-harness`, ver
   [`edgewarden-test-harness/README.md`](../../edgewarden-test-harness/README.md), contorna isso
   fazendo sua própria varredura do journal do `nodered` para saber o estado real de conexão MQTT —
   é um workaround na camada de observação do ensaio, não uma correção do bug em si).
2. **Credencial do primeiro provisionamento do `edgewarden` ainda ativa e nunca usada.** Device
   órfão `e67038d8-5879-42ef-83d1-e7c150196751` (`Identifier='edgewarden'`, `IsActive=true`,
   `LastContactAtUtc` nulo). Origem: falha de transferência do `EnvironmentFile` relatada na
   [Fase 5](../../../docs/handoff/relatorio-fase-5-piloto-fisico-gateway-pi-2026-07-31.md) (seção 9,
   item 1) — a credencial ficou irrecuperável e o device foi reprovisionado com o id correto
   `9349f958-...`, mas o órfão nunca foi revogado/desativado. Ação pendente: revogar via API/dynsec.
3. **`fluxo-worker-ingestion` loga `libgssapi_krb5.so.2: cannot open shared object file` toda vez
   que o `RejectionReprocessingWorker` sobe** (falha na primeira tentativa de conexão Npgsql logo
   após o start do container; recupera sozinho nas tentativas seguintes e passa a persistir/consultar
   normalmente). Observado em 3 restarts distintos do worker (2026-08-06, 2026-08-07, 2026-08-08),
   sempre nos primeiros ~5s de vida do container. Não afetou a ingestão do `edgewarden` em nenhuma
   das ocorrências. Não investigado a fundo — provável imagem base sem `libgssapi-krb5-2` que o
   Npgsql tenta carregar para negociação Kerberos/GSSAPI opcional.
4. ~~`gateway.time_synchronized` implementado no repo mas ainda não implantado no Pi.~~ **Resolvido
   em 2026-08-08.** Implantado via `scp` direto do `gateway-spool.js` atualizado (não foi necessário
   rodar `deploy.sh` inteiro nem reiniciar o `nodered` — o node `exec` do flow chama o script como
   processo novo a cada execução, então trocar o arquivo já basta). Confirmado chegando no Postgres:
   sequence 3992 com `gateway.time_synchronized=true`. Nessa mesma passada foram adicionadas também
   `gateway.network_interface_up`/`network_rx_bytes`/`network_tx_bytes` (lidas de `wlan0`, a
   interface real deste gateway — ver seção 1). Ver
   [`edgewarden-test-harness/docs/current-state-assessment.md`](../../edgewarden-test-harness/docs/current-state-assessment.md).
5. **Causa raiz de rede indisponível por ~9h em um boot específico não foi confirmada no nível de
   interface de rede** (sem acesso a `dmesg`/logs do driver WiFi/Ethernet nesta investigação — só
   inferida pela correlação entre NTP, MQTT e o `fluxo-gateway-monitor.service` ficarem represados
   juntos). Se o incidente se repetir, capturar `journalctl -k` e `dmesg` do boot afetado antes de
   reiniciar, para confirmar se é WiFi instável, DHCP lento ou outra causa.

## 5. Problemas já resolvidos (histórico — não reabrir sem evidência de regressão)

Consolidado do [Relatório da Fase 5](../../../docs/handoff/relatorio-fase-5-piloto-fisico-gateway-pi-2026-07-31.md)
(seção 9) e desta validação:

- `EnvironmentFile` corrompido ao normalizar CRLF na primeira transferência ao Pi → corrigido
  transportando como base64 com validação antes do restart. Deixou o device órfão do item 4.2 acima
  como resíduo (ainda pendente de revogar, não de corrigir de novo).
- Imagens API/Worker rodando antes do HEAD rejeitaram 12 mensagens V2 (`schemaVersion` numérico)
  como `PayloadInvalid` → resolvido com rebuild das imagens no HEAD correto. Sequences 1–12 ficaram
  como lacunas intencionais, nunca reutilizadas — uma lacuna nesse intervalo específico não é sinal
  de problema novo.
- `adminAuth` do Node-RED bloqueava deploy via API local (401) → deploy ajustado para parada breve e
  atualização offline do flow/credential store, preservando o hardening existente.
- MQTT plaintext 1883 exposto incorretamente → nunca esteve acessível ao Pi (loopback), comprovado
  por teste negativo; TLS 8883 é o único caminho real.
- CA inválida, credencial inválida e publicação fora da ACL do tópico → todos testados e
  corretamente rejeitados (CA/credencial: conexão recusada; tópico fora da ACL: MQTT v5 reason 135
  `Not authorized`). Esse comportamento é o esperado; um "sucesso" de qualquer um desses três casos
  seria uma regressão de segurança grave.
- Restart do Node-RED offline, reboot do Pi offline, reconexão e replay → fila cresceu e drenou
  corretamente em todos os testes físicos, sem duplicatas (índice idempotente por `Sequence`
  confirmado).

## 6. Quando reabrir uma varredura completa

Não repita esta investigação a cada pergunta de rotina — use a seção 2. Só aprofunde (logs
completos do mosquitto/worker, `psql` detalhado, revisão do `flow.json`, acesso SSH ao Pi) se algum
destes sinais aparecer:

- `LastContactAtUtc` do device parado há mais que alguns minutos sem nova mensagem.
- Qualquer linha nova em `telemetry_ingestion_rejections` para o tópico do `edgewarden`.
- Log do mosquitto mostrando `disconnected` repetido em loop curto (reconexões constantes) em vez
  de uma queda isolada seguida de reconexão estável.
- `gateway.queue_depth` crescendo de forma sustentada em vez de drenar após uma reconexão.
- Erro novo e diferente dos listados na seção 4 nos logs de `fluxo-worker-ingestion` ou
  `fluxo-mosquitto`.
- Qualquer um dos testes negativos da seção 5 (CA inválida, credencial inválida, tópico fora da
  ACL) passando quando deveria falhar — isso é incidente de segurança, não item de rotina.
- `gateway.time_synchronized = false` em telemetria recente (depois que o item 4 da seção 4 for
  implantado) — reabrir com o roteiro da seção 3.1 (journalctl do `nodered`, `systemd-timesyncd` e
  `fluxo-gateway-monitor` no boot afetado) antes de assumir que é só o boot inicial normal.

## 7. Comandos de referência completos

Usados para produzir a validação de 2026-08-08; úteis para uma varredura completa se um gatilho da
seção 6 disparar.

SSH direto no Pi (chave já confiada, `known_hosts` já tem `edgewarden`/`192.168.9.18`; ver seção 2.1
para reconfirmar o IP se mudou):

```bash
ssh -o BatchMode=yes -o ConnectTimeout=8 junior@192.168.9.18 "timedatectl status; systemctl is-active nodered fluxo-gateway-monitor"
ssh -o BatchMode=yes junior@192.168.9.18 "journalctl -u nodered -b --no-pager | tail -100"
ssh -o BatchMode=yes junior@192.168.9.18 "journalctl -u systemd-timesyncd -b --no-pager"
ssh -o BatchMode=yes junior@192.168.9.18 "node /home/junior/.node-red/fluxo-gateway/scripts/gateway-spool.js status"
```

Do lado do host que roda o stack Fluxo:

```bash
docker ps
docker logs fluxo-mosquitto --since 24h
docker exec fluxo-mosquitto sh -c "tail -n 200 /mosquitto/log/mosquitto.log"
docker logs fluxo-worker-ingestion --since 30m
docker logs fluxo-worker-ingestion -t 2>&1 | grep -i "libgssapi\|RejectionReprocessingWorker"
docker exec fluxo-postgres psql -U fluxo -d fluxo_db -c "SELECT \"Id\",\"Identifier\",\"CreatedAtUtc\",\"IsActive\",\"LastContactAtUtc\" FROM devices WHERE \"Identifier\"='edgewarden';"
docker exec fluxo-postgres psql -U fluxo -d fluxo_db -c "SELECT count(*), min(\"ReceivedAtUtc\"), max(\"ReceivedAtUtc\") FROM telemetry_ingestion_rejections WHERE \"DeviceId\"='9349f958-2805-41f8-a323-f08c7a161c1b';"
docker exec fluxo-postgres psql -U fluxo -d fluxo_db -c "SELECT \"Sequence\",\"OccurredAtUtc\",\"PayloadJson\"->'metrics'->>'gateway.mqtt_connected' AS mqtt_connected,\"PayloadJson\"->'metrics'->>'gateway.replayed_messages' AS replayed,\"PayloadJson\"->'metrics'->>'gateway.queue_depth' AS queue_depth FROM telemetry_ingestion_records WHERE \"DeviceId\"='edgewarden' ORDER BY \"Sequence\" DESC LIMIT 15;"
```

Credenciais do Postgres (`fluxo`/`fluxo_db`) vêm do `docker-compose.yml` local; não versionar a
senha aqui. A senha/credencial MQTT do device nunca deve ser colocada neste arquivo, em logs ou em
commits — segue a mesma regra do [README](../README.md) e do
[Relatório da Fase 5](../../../docs/handoff/relatorio-fase-5-piloto-fisico-gateway-pi-2026-07-31.md)
(seção 10).
