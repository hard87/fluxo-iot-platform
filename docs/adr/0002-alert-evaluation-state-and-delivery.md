# ADR-0002 — Motor de alertas: estado persistido e entrega desacoplada

## Status

Aceito em 11 jul 2026 (Fase 0 do roadmap). Depende do ADR-0001 (`TelemetryPoint`/
`MetricDefinition`) estar implementado — o worker de avaliação lê dessa tabela.

## Contexto

A primeira proposta de motor de alertas (nota estratégica interna de 11 jul 2026) descrevia um
`AlertEvaluationWorker` fazendo polling de "janela recente" e chamando `WebhookUrl` diretamente
via `HttpClient` a partir do próprio worker de avaliação. Isso tem dois problemas sérios:

1. **Sem estado persistido**: "dispara quando temperatura > 80 por 5 minutos" não sobrevive a um
   restart do worker sem um cursor e um registro de quando a violação começou.
2. **SSRF**: aceitar uma URL fornecida pelo cliente (`WebhookUrl`) e acessá-la diretamente do
   servidor, sem validação de destino, é a superfície clássica de Server-Side Request Forgery
   (OWASP). Um prompt que diga apenas "chame o `WebhookUrl` via `HttpClient`" está incompleto e
   não deve ser executado como está.

## Decisão

### Modelo

```
AlertRule                    AlertRuleState                AlertEvent              WebhookDelivery
- Id                          - AlertRuleId                 - Id                    - Id
- WorkspaceId                 - DeviceId                     - AlertRuleId           - AlertEventId
- Name                        - FirstViolationAtUtc?         - DeviceId              - TargetUrl
- MetricDefinitionId          - LastObservedAtUtc            - MetricDefinitionId    - Status
- DeviceId?  (null = qualquer - LastValueNumeric?             - TriggeredAtUtc        - AttemptCount
   device do workspace)       - LastValueBoolean?             - ResolvedAtUtc?        - NextAttemptAtUtc
- Operator (>,>=,<,<=,        - LastValueText?                - TriggerValue...       - LastError
   inside/outside range)      - IsFiring                      - ResolutionValue...    - DeliveredAtUtc?
- Threshold? / ThresholdHigh? - LastTriggeredAtUtc?           - Status (Firing|Resolved) - DeliveryId (Guid,
- DurationSeconds             - UpdatedAtUtc                  - Severity                  estável p/ dedupe)
- Hysteresis?
- CooldownSeconds
- Severity
- IsEnabled

AlertEvaluationWorkItem
------------------------
- Id
- WorkspaceId
- IngestionRecordId       (FK TelemetryIngestionRecord — 1 work item por ingestion record aceito)
- Status                  (Pending|Claimed|Completed|Failed|DeadLetter)
- AttemptCount
- CreatedAtUtc
- ClaimedAtUtc?
- ClaimedBy?              (identificador da instância do worker, observabilidade)
- NextAttemptAtUtc?       (backoff entre tentativas)
- CompletedAtUtc?
- LastError?
```

**Correção (ARCH-002, 11 jul 2026): operador `NoData` removido do escopo deste ADR.** A versão
anterior incluía `NoData` na lista de operadores de `AlertRule`, mas o motor descrito aqui só é
acionado pela chegada de novos `TelemetryPoint` — ausência de telemetria não gera
`TelemetryPoint`, não gera evento novo, e portanto não existe gatilho para um operador `NoData`
dentro deste design. Ver seção "Stale/no-data — Fase 4, não Fase 3" abaixo para onde essa
capacidade vive.

- `Equals` exato para `double` não entra no MVP — comparações numéricas usam operadores
  direcionais ou range. Regras booleanas usam `IsTrue`/`IsFalse`, com validação impedindo
  operador incompatível com o `ValueType` da métrica.
- Uma regra `DeviceId=null` (aplica a qualquer device do workspace) mantém um `AlertRuleState`
  **separado por device** — é assim que se sabe qual device disparou o `AlertEvent`.
- Restrição lógica/índice parcial impede mais de um `AlertEvent` com `Status=Firing` para o
  mesmo par `(AlertRuleId, DeviceId)`.

### Trabalho transacional, não cursor temporal (ARCH-001, correção 11 jul 2026)

**Problema com o design anterior**: ADR-0001 aceita `OccurredAtUtc` atrasado em até 30 dias por
padrão (telemetria late-arriving de um device que esteve offline). O cursor `(OccurredAtUtc, Id)`
anterior assumia que "processar em ordem de `OccurredAtUtc`" é equivalente a "descobrir trabalho
novo em ordem de chegada" — não é. Um `TelemetryPoint` criado **depois** do cursor já ter avançado
além do seu próprio `OccurredAtUtc` (porque chegou atrasado) nunca seria varrido pela página
seguinte do cursor, que já está à frente desse timestamp. Esse ponto nunca seria apresentado ao
motor de alertas. `AlertEvaluationCursor` é **removido inteiramente** deste ADR e substituído por
uma fila de trabalho transacional.

**`AlertEvaluationWorkItem`** — granularidade por `TelemetryIngestionRecord`, não por
`TelemetryPoint` individual nem por lote arbitrário. Motivo: `TelemetryIngestionRecord` já é a
unidade atômica da transação de ingestão (ADR-0001, "Fluxo transacional de ingestão") — 1 a 64
`TelemetryPoint` de uma mesma mensagem são sempre criados juntos, então avaliá-los juntos em um
único work item é natural e não exige nova coordenação. Um lote maior (N ingestion records por
work item) economizaria linhas de fila, mas complica retry parcial (um record com erro bloquearia
outros records saudáveis no mesmo item) sem ganho de throughput comprovado — não adotado sem
evidência de benchmark.

```sql
BEGIN;
INSERT INTO TelemetryIngestionRecord (...) ON CONFLICT (DeviceId, Sequence) DO NOTHING RETURNING Id;
-- se inseriu (não é duplicata):
INSERT INTO TelemetryPoint (...) -- N linhas, multi-row
INSERT INTO AlertEvaluationWorkItem (WorkspaceId, IngestionRecordId, Status)
  VALUES (..., ..., 'Pending');
COMMIT;
```

A criação do work item é atômica com a persistência da telemetria aceita — se a mensagem for
rejeitada ou for duplicata, nenhum work item é criado.

**Consumo (claim concorrente entre instâncias)**:

```sql
WITH claimed AS (
  SELECT Id FROM AlertEvaluationWorkItem
  WHERE Status = 'Pending'
     OR (Status = 'Claimed' AND ClaimedAtUtc < now() - interval '60 seconds')  -- lease expirado
  ORDER BY CreatedAtUtc, Id   -- ordem de criação do work item, NUNCA OccurredAtUtc
  FOR UPDATE SKIP LOCKED
  LIMIT @batchSize
)
UPDATE AlertEvaluationWorkItem
SET Status = 'Claimed', ClaimedAtUtc = now(), ClaimedBy = @workerInstanceId,
    AttemptCount = AttemptCount + 1
FROM claimed
WHERE AlertEvaluationWorkItem.Id = claimed.Id
RETURNING AlertEvaluationWorkItem.*;
```

- **Ordem de consumo**: `(CreatedAtUtc, Id)` do próprio work item — ou seja, ordem de chegada real
  ao sistema, não o `OccurredAtUtc` semântico do evento. Isso é o que resolve o ARCH-001: um ponto
  atrasado gera um work item **quando é persistido**, não quando "deveria" ter sido persistido, e
  portanto sempre aparece na fila de trabalho pendente independentemente de quão velho seja seu
  `OccurredAtUtc`.
  - `OccurredAtUtc` continua sendo o tempo semântico usado *dentro* da avaliação de cada regra
    (duração da violação, gap de amostra, ordem temporal de `AlertRuleState`) — só não é mais usado
    para descobrir *que* trabalho existe.
- **Concorrência entre instâncias**: `FOR UPDATE SKIP LOCKED` garante que duas instâncias do
  `AlertEvaluationWorker` nunca reivindicam o mesmo work item — uma pula as linhas já bloqueadas
  pela outra.
- **Recuperação após crash**: um work item `Claimed` cuja `ClaimedAtUtc` é mais antiga que o
  lease (`AlertEvaluation:WorkItemLeaseSeconds`, default 60s — deve ser maior que o tempo máximo
  esperado de processamento de um item) volta a ser elegível para claim por qualquer instância,
  incluindo a mesma que travou e reiniciou. Não há passo de "liberação explícita": o lease vencido
  é a própria condição de recuperação.
- **Poison work item / dead letter**: `AttemptCount` incrementado a cada claim. Ao atingir
  `AlertEvaluation:MaxAttempts` (default 5) sem chegar a `Completed`, o item vira
  `Status=DeadLetter` — visível no dashboard operacional, não bloqueia claim de nenhum outro item
  (cada work item é uma linha independente; `SKIP LOCKED` já impede que um item travado impeça
  outras instâncias de seguir para o próximo). Nenhum retry automático depois de `DeadLetter`;
  requer ação manual (reprocessamento assistido, fora do escopo desta fase).
- **Falha de uma regra não bloqueia as outras**: dentro do processamento de um work item, cada
  `AlertRule` aplicável aos `TelemetryPoint` do `IngestionRecordId` é avaliada em um bloco
  try/catch independente. Uma exceção ao avaliar a regra X é logada e não impede a avaliação da
  regra Y no mesmo work item. O work item só é marcado `Failed` (elegível a retry via
  `NextAttemptAtUtc` com backoff) se ocorrer uma falha **transacional/infraestrutural** (ex. banco
  indisponível no meio do commit) — nesse caso todas as regras daquele item são re-tentadas no
  próximo claim, e a transação de estado (`AlertRuleState`/`AlertEvent`) garante que isso é seguro
  de repetir (ver idempotência abaixo).
- **Idempotência de `AlertEvent`**: reprocessar o mesmo work item (após crash ou retry) é seguro
  porque a mutação de `AlertRuleState`/`AlertEvent` é baseada em **estado atual**, não em soma/
  contagem — reavaliar a mesma regra com o mesmo `TelemetryPoint` produz a mesma transição de
  estado (ou nenhuma, se já applied). O índice parcial único em
  `(AlertRuleId, DeviceId) WHERE Status='Firing'` impede duplicar `AlertEvent` de disparo.
- Cache de regras ativas por workspace, com invalidação em CRUD de `AlertRule`.
- `DurationSeconds` usa `FirstViolationAtUtc` — só dispara quando a condição continua válida até
  o limiar.
- Se o gap entre amostras exceder `max(2 × ExpectedIntervalSec, DurationSeconds)`, a continuidade
  é quebrada e `FirstViolationAtUtc` é resetado.
  **`ExpectedIntervalSec` nulo** (métrica recém-descoberta, `Status=Discovered`, ainda sem
  intervalo calibrado): default global configurável `AlertEvaluation:DefaultExpectedIntervalSec`
  (5 minutos) é usado no cálculo de gap até haver amostras suficientes para calibrar
  `ExpectedIntervalSec` automaticamente (calibração automática é Fase 4, fora do escopo desta
  fase — até lá, o default fixo se aplica sem exceção).
- Hysteresis evita flapping: ex. dispara `>80` com `hysteresis=2`; resolve quando `<=78`.

### Late-arriving telemetry e alertas — política fechada (ARCH-001)

Cenário de referência: cursor/fila já processou um `IngestionRecordId` com `OccurredAtUtc` de
hoje; depois chega (e é aceito por ADR-0001) um ponto com `OccurredAtUtc` de ontem, de um device
que esteve offline. Política, sem ambiguidade:

- O `TelemetryPoint` atrasado **é sempre persistido e sempre visível** em consultas históricas
  (Explorer, análise retrospectiva) — isso é comportamento de ADR-0001, não deste ADR.
- Para o motor de alertas: o work item correspondente **é sempre criado e sempre avaliado** (é
  isso que ARCH-001 corrige). Mas, ao avaliar, cada regra aplica um **guard de monotonicidade**:
  a mutação de `AlertRuleState` (`LastObservedAtUtc`, `LastValue*`, `FirstViolationAtUtc`,
  `IsFiring`) só ocorre se o `OccurredAtUtc` do ponto sendo avaliado for **maior ou igual** ao
  `LastObservedAtUtc` já persistido no `AlertRuleState` daquele `(AlertRuleId, DeviceId)`.
- Um ponto atrasado cujo `OccurredAtUtc` é anterior ao `LastObservedAtUtc` atual **não altera o
  estado corrente do alerta** (não reabre, não fecha, não reseta `FirstViolationAtUtc`, não move
  `IsFiring`). Ele é registrado em log como "ponto histórico avaliado, estado de alerta não
  alterado por chegar fora de ordem" — auditável, mas sem efeito colateral no estado ativo.
- Consequência explícita: um `AlertEvent` já `Resolved` **não reabre** por causa de um ponto
  atrasado antigo; um `AlertEvent` `Firing` **não é retroativamente "des-disparado"** por uma
  leitura atrasada que teria, em teoria, mudado o resultado se tivesse chegado a tempo. O motor de
  alertas reflete o que era observável no momento em que os dados chegaram, não uma reconstrução
  retroativa perfeita — essa é uma escolha deliberada de simplicidade para o MVP, não uma omissão.
  Reabertura retroativa de alertas fica fora de escopo (não há gate de fase definido para isso;
  se um cliente piloto pedir, é uma decisão de produto nova, não uma correção deste ADR).

### Stale/no-data — Fase 4, não Fase 3 (ARCH-002, correção 11 jul 2026)

**Opção escolhida: A.** `NoData` sai completamente do motor de alertas (`AlertRule`/
`AlertEvaluationWorker`) descrito neste ADR. Motivo: o motor aqui é acionado por
`AlertEvaluationWorkItem`, que só existe quando um `TelemetryPoint` é persistido. Ausência de
telemetria não persiste nada e portanto não pode disparar um mecanismo cujo único gatilho é
"chegou dado novo" — manter `NoData` como operador de `AlertRule` sem um segundo mecanismo
temporal independente deixaria uma decisão crítica sem dono, exatamente o tipo de lacuna que esta
revisão existe para fechar.

Stale/no-data pertence à **Fase 4** ("Inteligência operacional barata", já prevista em
`mvp-scope.md`), como mecanismo **completamente separado** do motor de alertas orientado a
work item:

- **Worker responsável**: `DeviceStaleEvaluationWorker` (novo, Fase 4) — um `BackgroundService`
  com `PeriodicTimer`, seguindo o mesmo padrão já usado por `RejectionReprocessingWorker`
  (`src/Fluxo.Worker.Ingestion/Workers/RejectionReprocessingWorker.cs`), não o padrão orientado a
  work item deste ADR.
- **Periodicidade**: configurável (`DeviceStaleEvaluation:IntervalSeconds`, default 60s) —
  independente da chegada de telemetria.
- **Fonte de `last_seen`**: `Device.LastContactAtUtc`, campo que **já existe** hoje em
  `Fluxo.Domain/Entities/Device.cs` e já alimenta `Device.GetOperationalStatus(now, offlineAfter)`
  — a Fase 4 estende esse mecanismo já existente em vez de introduzir uma tabela paralela.
- **Fórmula** (já descrita em `mvp-scope.md`, reafirmada aqui como contrato):
  `now - last_seen > max(2 × expected_interval, stale_threshold)`.
- **`ExpectedIntervalSec` nulo**: mesmo default global usado no motor de alertas
  (`AlertEvaluation:DefaultExpectedIntervalSec`, 5 minutos) — um único valor de configuração
  compartilhado entre os dois mecanismos, para não haver dois defaults divergentes no sistema.
- **Estado persistido**: reutiliza os campos já existentes em `Device` (`LastContactAtUtc`) para
  o cálculo; o **evento** de stale/voltou-a-responder é persistido em uma tabela nova e específica
  (`DeviceHealthEvent`, fora do escopo de `AlertEvent`/`AlertRule` — health de device não é uma
  métrica com `ValueType`, é um estado derivado de ausência de dado, então não se encaixa no
  modelo de `AlertRule` deste ADR). Detalhamento de `DeviceHealthEvent` fica para o handoff de
  Fase 4, não desta revisão.
- **Hysteresis temporal**: transição para "stale" exige que a condição continue verdadeira por
  `DeviceStaleEvaluation:ConfirmationCycles` varreduras consecutivas (default 2) antes de gerar
  evento — evita flapping por atraso de rede pontual. Transição de volta a "online" ocorre
  imediatamente no primeiro `LastContactAtUtc` novo (não precisa de confirmação — dado chegando é
  fato positivo direto).
- **Restart**: seguro porque todo o estado usado (`Device.LastContactAtUtc`) já é persistido; o
  worker recalcula do zero a cada ciclo, sem cursor próprio.
- **Múltiplas instâncias**: a varredura em si é idempotente (ler `Device.LastContactAtUtc` e
  comparar não muda estado), mas a **criação do evento** de stale precisa da mesma proteção contra
  duplicata do `AlertEvent`: índice parcial único em `DeviceHealthEvent (DeviceId) WHERE
  Status='Stale'`, mesma técnica já usada para `AlertEvent`. Duas instâncias rodando o mesmo ciclo
  concorrentemente tentam o mesmo `INSERT`; a segunda falha por violação de unique constraint e
  trata isso como no-op esperado (não como erro).
- Este mecanismo **não** compartilha worker, tabela de estado ou lógica com
  `AlertEvaluationWorker`/`AlertEvaluationWorkItem` — as duas fases não competem para implementar
  a mesma capacidade de formas diferentes.

### Webhooks são entrega, não avaliação

`AlertEvaluationWorker` **não** chama `HttpClient`. Ao disparar um evento, cria
`WebhookDelivery`. Um `WebhookDeliveryWorker` separado envia, com timeout, retry limitado,
backoff e circuit breaker (usar os handlers de resiliência padrão do .NET para `HttpClient`, não
retry infinito ad hoc).

### Semântica de entrega — at-least-once (ARCH-007B, correção 11 jul 2026)

**Webhook delivery é at-least-once, nunca exactly-once.** Cenário de referência: o servidor remoto
recebe o POST e responde `200`, mas o Fluxo encerra antes de persistir `Delivered`; no restart, o
`WebhookDelivery` continua `Pending`/`InFlight` e é reenviado. O consumidor pode receber o mesmo
evento mais de uma vez — isso é declarado explicitamente, não escondido atrás de "raramente
acontece".

**Identificadores estáveis** (headers HTTP em toda entrega):

- `X-Fluxo-Delivery-Id`: identifica a **tentativa lógica de entrega** — estável entre retries do
  mesmo `WebhookDelivery` (não muda a cada tentativa HTTP, muda por evento).
- `X-Fluxo-Event-Id`: identifica o `AlertEvent` de origem — é o identificador que o consumidor usa
  para deduplicação (um mesmo `AlertEvent` pode gerar novas entregas se o estado mudar, ex.
  `Firing` → `Resolved`, mas o `EventId` do disparo original permanece o mesmo em ambas).
- `X-Fluxo-Event-Type`: ex. `alert.firing`, `alert.resolved` — permite roteamento no lado do
  consumidor sem parsear o corpo.
- `X-Fluxo-Timestamp`: `occurredAtUtc` do evento, para o consumidor detectar entregas fora de
  ordem (webhooks de eventos diferentes podem chegar fora de ordem entre si por causa de retry).

**Payload mínimo versionado**:

```json
{
  "schemaVersion": 1,
  "eventId": "...",
  "deliveryId": "...",
  "eventType": "alert.firing",
  "occurredAtUtc": "...",
  "workspaceId": "...",
  "deviceId": "...",
  "ruleId": "...",
  "metricKey": "...",
  "status": "firing",
  "value": { "type": "numeric", "numeric": 81.4 }
}
```

**Deduplicação do consumidor**: `eventId` — o mesmo `AlertEvent` nunca gera dois `eventId`
diferentes para o mesmo estado; o consumidor que já processou um `eventId` com um dado
`eventType`/`status` pode ignorar reentregas com segurança.

**Retry por status HTTP**:

| Resposta | Comportamento |
|---|---|
| `2xx` | Sucesso — `Status=Delivered`, sem retry. |
| `3xx` | **Não seguido** (redirects desabilitados, ver SSRF abaixo) — tratado como falha não
  retryable, `Status=Failed` após esgotar tentativas de conexão (não é erro transitório: o
  destino está mal configurado). |
| `408` | Retryable — tratado como timeout. |
| `429` | Retryable — respeita `Retry-After` se presente (capado em `WebhookDelivery:MaxRetryAfterSeconds`, default 300s); senão usa backoff padrão. |
| `4xx` (exceto 408/429) | **Não retryable** — erro do lado do consumidor que não se resolve
  reenviando o mesmo payload (ex. 401/404/422); vai direto para `Status=Failed` após 1 tentativa,
  visível como erro de configuração do cliente, não como falha de infraestrutura. |
| `5xx` | Retryable — erro do lado do consumidor, mas potencialmente transitório. |
| Timeout de conexão/leitura | Retryable. |

**Timeout**: 5 segundos totais por tentativa (já definido no ADR original, mantido).
**Máximo de tentativas**: 5 (já definido, mantido) — depois `Status=DeadLetter`, visível no
portal. Backoff exponencial com jitter entre tentativas retryable (handlers de resiliência padrão
do .NET, não retry ad hoc).

### SSRF — mecanismo concreto (ARCH-007A, correção 11 jul 2026)

A versão anterior deste ADR listava requisitos (bloquear localhost/privado, validar DNS,
"revalidar destino no momento da conexão") sem fechar **como** isso é implementado em .NET — isso
deixaria para o implementador decidir sozinho o único ponto onde a proteção contra DNS rebinding
realmente importa: garantir que o IP validado é o IP efetivamente usado pelo socket TCP.

**Target framework confirmado em código**: `net10.0` (`Fluxo.Worker.Ingestion.csproj` e demais
projetos do repositório) — `SocketsHttpHandler.ConnectCallback` está disponível desde .NET 5 e é
a forma correta e suportada de controlar a conexão de socket subjacente sem perder validação de
TLS/SNI baseada no hostname original.

**Mecanismo fechado**:

```
1. parse da URI (esquema, host, porta)
2. validar esquema: HTTPS obrigatório (HTTP só com ambiente=Development E flag explícita)
3. resolver hostname via DNS (Dns.GetHostAddressesAsync) -> lista de IPs
4. validar CADA IP retornado contra: loopback (IPAddress.IsLoopback), link-local
   (IPv4 169.254.0.0/16, IPv6 fe80::/10), ranges privados IPv4 (10.0.0.0/8, 172.16.0.0/12,
   192.168.0.0/16), IPv6 loopback (::1), IPv6 site-local/ULA (fc00::/7), e faixa conhecida de
   metadata de nuvem (169.254.169.254 já cai em link-local — não depende de lista de hostname)
5. se QUALQUER IP retornado for proibido -> rejeitar o destino inteiro (não tentar "escolher só o
   IP bom" entre vários — se o hostname resolve para um IP privado em qualquer resposta de DNS,
   é tratado como configuração de destino proibida)
6. selecionar/congelar o primeiro IP válido da lista como destino de conexão
7. usar SocketsHttpHandler.ConnectCallback para a requisição desse HttpClient: o callback recebe
   o SocketsHttpConnectionContext (que já carrega o DnsEndPoint original) e conecta explicitamente
   ao IP congelado no passo 6 — NUNCA deixando o HttpClient/Kestrel resolver o hostname de novo
   internamente. Isso fecha o TOCTOU/DNS rebinding: entre a validação (passo 3-5) e a conexão
   real (passo 7) não há uma segunda resolução de DNS que um atacante possa influenciar.
8. o hostname original é preservado para SNI/validação de certificado TLS (SslClientAuthentication-
   Options.TargetHost) — a conexão física vai para o IP congelado, mas o handshake TLS continua
   validando o certificado contra o hostname público, então certificados legítimos continuam
   funcionando.
9. HttpClientHandler/SocketsHttpHandler configurado com AllowAutoRedirect=false (redirects
   tratados como falha, nunca seguidos automaticamente).
```

Implementação concreta: um `SocketsHttpHandler` dedicado para o `HttpClient` de webhook
(registrado via `IHttpClientFactory`, não `new HttpClient()` solto), com `ConnectCallback`
custom que executa os passos 3-7 acima a cada conexão — a revalidação acontece **por conexão**,
não uma vez no cadastro do `WebhookDelivery`, o que fecha o cenário de TOCTOU mesmo se o DNS mudar
entre o momento em que a regra foi criada e o momento em que o webhook é efetivamente disparado.

**Testes obrigatórios** (nomes de teste, não descrição vaga):

1. `Webhook_TargetLocalhost_IsBlocked` — URL apontando para `localhost`/`127.0.0.1` é rejeitada
   antes de qualquer tentativa de conexão.
2. `Webhook_TargetPrivateIPv4_IsBlocked` — destino que resolve para `10.0.0.0/8`, `172.16.0.0/12`
   ou `192.168.0.0/16` é rejeitado.
3. `Webhook_TargetLoopbackIPv6_IsBlocked` — `::1` é rejeitado.
4. `Webhook_TargetLinkLocal_IsBlocked` — link-local IPv4 (`169.254.0.0/16`) e IPv6 (`fe80::/10`)
   são rejeitados — cobre também o endpoint de metadata de nuvem `169.254.169.254` **pela
   classificação de endereço**, não por uma lista textual de hostnames conhecidos.
5. `Webhook_RemoteRedirectsToPrivateTarget_IsBlocked` — endpoint público que responde com
   `3xx` apontando para um destino privado é bloqueado porque `AllowAutoRedirect=false` impede o
   `HttpClient` de seguir o redirect automaticamente; o teste confirma que o destino do redirect
   nunca é conectado.
6. `Webhook_DnsRebinding_ConnectsOnlyToValidatedAddress` — simula resolução DNS que muda entre a
   validação e a tentativa de conexão (ex. via resolver fake injetado no teste); o teste
   inspeciona o `ConnectCallback` para confirmar que o socket conectado corresponde ao endereço
   validado no passo 4-6 do mecanismo, nunca a uma segunda resolução.
7. Log de auditoria: host e resultado (`Allowed`/`Blocked`+motivo) são logados; headers de
   autenticação e secrets configurados para a entrega **nunca** aparecem em log.

## Consequências

**Positivas**: estado sobrevive a restart; descoberta de trabalho não depende de `OccurredAtUtc`
(fecha ARCH-001, late-arriving telemetry sempre gera work item); `FOR UPDATE SKIP LOCKED` permite
múltiplas instâncias sem coordenação externa; SSRF fechado desde o design com mecanismo concreto
de conexão (`ConnectCallback`), não como patch depois; avaliação e entrega desacopladas permitem
escalar/depurar cada uma independentemente; webhook delivery declarado at-least-once evita
prometer uma garantia que HTTP não pode cumprir.

**Custos**: mais tabelas e um segundo worker (`WebhookDeliveryWorker`) em vez de um único processo
fazendo tudo; `AlertEvaluationWorkItem` substitui um único cursor por uma linha por ingestion
record (mais linhas, mas cada uma é pequena e de vida curta — completada ou dead-letter, não
crescimento indefinido); guard de monotonicidade em `AlertRuleState` exige checar
`OccurredAtUtc >= LastObservedAtUtc` a cada mutação, não só avançar cegamente.

**Lacunas fechadas nesta revisão (11 jul 2026) — nenhuma delegada ao implementador**: mecanismo de
descoberta de trabalho (`AlertEvaluationWorkItem` + `FOR UPDATE SKIP LOCKED`), política de
late-arriving telemetry (guard de monotonicidade), escopo de `NoData` (Fase 4, mecanismo
`DeviceStaleEvaluationWorker` separado), mecanismo concreto de SSRF (`ConnectCallback`), semântica
de entrega de webhook (at-least-once, `Delivery-Id`/`Event-Id`, tabela de retry por status HTTP).
Default de `ExpectedIntervalSec` para métricas `Discovered` (`AlertEvaluation:
DefaultExpectedIntervalSec`, 5 minutos) já fechado acima, compartilhado entre motor de alertas e
`DeviceStaleEvaluationWorker` — não é mais uma lacuna aberta.

## Referências

- OWASP — SSRF Prevention Cheat Sheet: https://cheatsheetseries.owasp.org/cheatsheets/Server_Side_Request_Forgery_Prevention_Cheat_Sheet.html
- Microsoft .NET — HTTP resilience: https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience
- Microsoft .NET — `SocketsHttpHandler.ConnectCallback`: https://learn.microsoft.com/en-us/dotnet/api/system.net.http.socketshttphandler.connectcallback
- PostgreSQL — `FOR UPDATE`/`SKIP LOCKED`: https://www.postgresql.org/docs/current/sql-select.html#SQL-FOR-UPDATE-SHARE
- `Fluxo_Documento_Confronto_Arquitetural_MVP.docx` (seção 07)
- `src/Fluxo.Worker.Ingestion/Workers/RejectionReprocessingWorker.cs` (precedente de worker
  periódico reutilizado para `DeviceStaleEvaluationWorker`, Fase 4)
