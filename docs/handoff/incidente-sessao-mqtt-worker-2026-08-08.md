# Incidente e correção — sessão MQTT do worker expirava sem `SessionExpiryInterval`

## Resumo

Descoberto em 2026-08-08 durante ensaios de resiliência do gateway físico `edgewarden`
(`Fluxo/devices/edgewarden-test-harness/`). Mensagens de telemetria publicadas por um device
podiam ser aceitas pelo broker (PUBACK MQTT QoS 1) e **nunca chegarem ao Postgres** — nem aceitas,
nem rejeitadas — sempre que o `fluxo-mosquitto` reiniciava com o `fluxo-worker-ingestion` ainda se
reconectando. Corrigido no mesmo dia. Validado com um teste reproduzindo a mesma corrida que
causava a perda: zero mensagens perdidas após a correção.

## Causa raiz

`MqttTelemetryIngestionWorker.cs` conectava com `WithCleanSession(false)` mas **sem**
`WithSessionExpiryInterval(...)`. Em MQTT 5, esses dois conceitos são separados:

- `CleanStart=false` (o que `WithCleanSession(false)` define) só diz ao broker "não comece uma
  sessão nova se já existir uma válida para este client ID".
- **`SessionExpiryInterval`** é quem determina se essa sessão sobrevive à desconexão, e por quanto
  tempo. Sem essa propriedade, o valor default do protocolo é `0` — a sessão expira **no instante**
  da desconexão, independentemente de `CleanStart`.

Resultado prático: toda vez que o worker desconectava (por qualquer motivo — restart do broker,
restart do próprio worker, blip de rede), o broker descartava imediatamente sua assinatura e
qualquer mensagem enfileirada para ele. Isso explica por que o log do `mosquitto` mostrava
`Restored 0 clients` / `Restored 0 subscriptions` em **todo** restart observado nesta sessão: não
havia, de fato, nada persistente para restaurar — a "sessão persistente" que o código pretendia
criar nunca existiu de verdade.

Mecanismo da perda, passo a passo:

1. Broker reinicia (`docker stop`/`docker start`, ou qualquer outro motivo).
2. O device volta a se conectar e publica seu backlog quase imediatamente.
3. O worker também reconecta, mas leva alguns segundos a mais (delay de reconexão + handshake +
   `SubscribeAsync`).
4. Nesses segundos, o broker aceita as publicações (QoS 1 → PUBACK) — mas, sem sessão persistente
   do worker, não há para onde rotear/enfileirar a mensagem. Ela é descartada silenciosamente.
5. O device, ao receber o PUBACK, remove a mensagem da sua fila local achando que foi entregue —
   comportamento correto do lado dele, dado o contrato QoS 1.

## Evidência

- Drill de 5 min do EdgeWarden: 2 sequences perdidas (4055, 4056), gap de ~7s entre o device
  reconectar e o worker resubscrever.
- Drill de 15 min (reboot real do gateway durante backlog): 3 sequences perdidas (4091–4093), mesma
  assinatura.
- Teste de validação pós-fix, reproduzindo a mesma corrida (device reconectando ~3s antes do
  worker): **0 sequences perdidas**, mensagens retidas no broker e entregues assim que o worker
  resubscreveu (delay de entrega de ~97s, não perda).

Detalhe completo, incluindo os relatórios brutos (`report.js`) de cada drill:
[`edgewarden-test-harness/docs/test-plan.md`](../../devices/edgewarden-test-harness/docs/test-plan.md).

## Correção

- `src/Fluxo.Worker.Ingestion/Options/MqttIngestionOptions.cs`: nova propriedade
  `SessionExpiryIntervalSeconds` (default `3600`).
- `src/Fluxo.Worker.Ingestion/Workers/MqttTelemetryIngestionWorker.cs`: adicionado
  `.WithSessionExpiryInterval(_options.SessionExpiryIntervalSeconds)` ao builder de opções de
  conexão.
- `appsettings.json`, `appsettings.Development.json`, `docker-compose.yml`: valor explícito
  (`3600`s / `MqttIngestion__SessionExpiryIntervalSeconds`), configurável via
  `FLUXO_MQTT_INGESTION_SESSION_EXPIRY_SECONDS`.
- 71 testes unitários seguem verdes; não há teste automatizado dedicado para este comportamento
  (depende de um broker real para observar corretamente — coberto pelo teste manual de corrida
  acima, não por unit test).

**Por que 3600s**: cobre reconexão após restart/manutenção do broker sem reter recursos
indefinidamente para um worker genuinamente morto. Não é a defesa primária contra perda de dado —
essa continua sendo o store-and-forward local de cada gateway — é uma correção de uma lacuna
específica na camada de transporte MQTT que o store-and-forward do gateway não tinha como enxergar
(do ponto de vista do device, o PUBACK é uma confirmação válida).

## Efeito colateral encontrado durante a correção (não relacionado à causa raiz)

Reconstruir a imagem do worker (`docker compose build worker` + `up -d worker`) fez o `docker
compose` recriar também o `fluxo-mosquitto`, que voltou a escutar em `127.0.0.1:8883` em vez de
`0.0.0.0:8883` — a exposição à LAN que o piloto físico (`edgewarden`) precisa. Essa exposição só
existia como variável de ambiente exportada manualmente em algum momento anterior, nunca persistida
no `.env` do projeto — por isso sumiu na primeira recriação do container feita a partir de uma
sessão de shell nova. Corrigido adicionando `FLUXO_MQTT_TLS_PUBLIC_BIND=0.0.0.0` ao `.env` do
projeto, para não depender de alguém lembrar de exportar a variável de novo. `1883` (plaintext)
permanece intencionalmente só em loopback.

## Pendência remanescente (não bloqueante)

Não existe teste automatizado (integração) que reproduza esta corrida especificamente
(broker restart + timing de reconexão do worker vs. um publisher). O teste manual descrito acima
é reproduzível seguindo `docs/runbook.md` do `edgewarden-test-harness`, mas não roda em CI.
Candidato a teste de integração futuro, fora do escopo desta correção pontual.
