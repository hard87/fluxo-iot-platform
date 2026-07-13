# ADR-0003 — Telemetry Query API (Fase 2)

## Status

Aceito em 11 jul 2026. Depende do ADR-0001 (`TelemetryPoint`/`MetricDefinition`, Fase 1,
implementada e validada — ver `docs/handoff/relatorio-fase-1-schema-v2-2026-07-11.md` e
`docs/benchmarks/phase1-normalized-2026-07-11.md`). Não depende do ADR-0002 (alertas) — Query API
é leitura pura, motor de alertas fora de escopo desta fase.

## Contexto

A Fase 1 provou que o Fluxo recebe e persiste telemetria genérica tipada. Não existe hoje nenhum
endpoint que devolva `TelemetryPoint` pelo novo schema — verificado em
`Fluxo.Api/Controllers/TelemetryController.cs` e `PortalDevicesController.cs`: os únicos
endpoints de leitura de telemetria (`GET /api/devices/{id}/telemetry`,
`GET /api/workspaces/{id}/devices/{deviceId}/telemetry`) devolvem `TelemetryIngestionRecord` bruto
paginado (`PayloadJson` cru), não `TelemetryPoint` agregável por métrica. O gap já estava
registrado no relatório da Fase 1: "a consulta HTTP de telemetria protegida ainda não foi
exercitada para o novo caminho V2".

A Fase 2 fecha esse gap com um único endpoint de consulta (`Telemetry Query API`) e a primeira
tela de produto (`Telemetry Explorer`) — não um dashboard configurável, não alertas, não rollup,
não IA.

## Decisão

### Endpoint

```
POST /api/workspaces/{workspaceId}/telemetry/query
```

`workspaceId` vem exclusivamente da rota (nunca do corpo) — elimina por construção qualquer
caminho onde o corpo da requisição possa reivindicar um workspace diferente do autorizado.

### Request

```csharp
public sealed record TelemetryQueryRequest(
    IReadOnlyList<Guid> DeviceIds,
    IReadOnlyList<string> MetricKeys,
    DateTime FromUtc,
    DateTime ToUtc,
    string Aggregation,   // "raw" | "avg" | "min" | "max" | "sum" | "count" | "last"
    string? Bucket        // "1m" | "5m" | "15m" | "1h" | "6h" | "1d" | null (null só quando Aggregation="raw")
);
```

`Bucket` é validado contra uma allow-list fixa no servidor (nunca interpolado em SQL livre) e
mapeado internamente para o `interval` do PostgreSQL via dicionário fixo:

```csharp
private static readonly IReadOnlyDictionary<string, string> BucketIntervals = new Dictionary<string, string>
{
    ["1m"] = "1 minute", ["5m"] = "5 minutes", ["15m"] = "15 minutes",
    ["1h"] = "1 hour", ["6h"] = "6 hours", ["1d"] = "1 day"
};
```

### Response

```csharp
public sealed record TelemetryQueryResponse(
    Guid WorkspaceId, DateTime FromUtc, DateTime ToUtc, string Aggregation, string? Bucket,
    IReadOnlyList<TelemetrySeriesResponse> Series, TelemetryQueryMeta Meta);

public sealed record TelemetrySeriesResponse(
    Guid DeviceId, string MetricKey, string ValueType, string? CanonicalUnit, string? SemanticType,
    IReadOnlyList<TelemetryPointResponse> Points, bool Truncated);

public sealed record TelemetryPointResponse(
    DateTime TimestampUtc,     // OccurredAtUtc (raw) ou início do bucket (agregado)
    double? NumericValue, bool? BooleanValue, string? TextValue,
    int? SampleCount);          // null em raw; contagem de amostras no bucket em agregado

public sealed record TelemetryQueryMeta(int TotalPoints, int MaxPointsAllowed, long ExecutionTimeMs);
```

Um `TelemetryPointResponse` usa os mesmos três slots nuláveis tipados da entidade `TelemetryPoint`
(`NumericValue`/`BooleanValue`/`TextValue`) — consistente com a convenção já estabelecida em
ADR-0001, em vez de um campo `value: any` genérico. Exatamente um slot é preenchido por ponto,
conforme o `ValueType` da série.

### Clarificação pós-Fase 2 — `aggregation=count` (12 jul 2026)

A implementação e os testes da Fase 2 confirmaram uma exceção localizada à regra de
exactly-one-value-slot do DTO de resposta:

- em `raw`, `avg`, `min`, `max`, `sum` e `last`, os slots de valor seguem o contrato tipado da
  série;
- em `count`, o resultado semântico vive em `SampleCount` e esse campo é obrigatório em cada
  ponto retornado;
- em pontos de `count`, `NumericValue`, `BooleanValue` e `TextValue` podem permanecer nulos;
- esta exceção pertence somente ao `TelemetryPointResponse`. Ela não altera o CHECK de
  exactly-one-value-slot da tabela `TelemetryPoint`, a persistência ou o Schema V2, e não cria
  migration.

Evidência: `TelemetryQueryRepository` projeta `count(*)` em `sample_count` e `NULL` nos três
slots para essa agregação; a matriz ValueType×Aggregation aceita `count` para Numeric, Boolean e
Text. O comportamento entregue na Fase 2 permanece inalterado.

**Ordenação**: `Series` na ordem `(DeviceIds × MetricKeys)` do request (produto cartesiano na
ordem declarada pelo cliente, determinístico). Dentro de cada série, `Points` em ordem cronológica
ascendente (`TimestampUtc` crescente) — nunca descendente; é o formato que qualquer biblioteca de
gráfico de série temporal espera sem transformação adicional.

**Partição sem dados**: uma série cujo device/métrica não tem nenhum `TelemetryPoint` no período
retorna `Points: []` — não é erro. Postgres já faz partition pruning nativamente para partições
fora do intervalo (`WHERE OccurredAtUtc BETWEEN ...` sobre tabela particionada por `RANGE`); não é
necessário tratamento especial no código de aplicação além de aceitar um resultado vazio como
válido.

### Matriz ValueType × Aggregation (fechada, não delegável)

| Aggregation | Numeric | Boolean | Text |
|---|:---:|:---:|:---:|
| `raw` | sim | sim | sim |
| `avg` | sim | não | não |
| `min` | sim | não | não |
| `max` | sim | não | não |
| `sum` | sim | não | não |
| `count` | sim | sim | sim |
| `last` | sim | sim | sim |

Decisão fechada sobre `true_ratio` para `Boolean`: **não entra no MVP**. Motivo: exigiria uma
semântica adicional (proporção de tempo/amostras em `true` dentro do bucket) que não é um simples
agregado SQL de uma coluna — depende de decidir se é "amostras true / amostras totais" (ponderação
por contagem, já coberto por `count` + leitura do cliente) ou "tempo em true / duração do bucket"
(ponderação temporal, exige interpolação entre amostras — fora de escopo do Explorer). Fica
`DEFERRED` para Fase 4 (inteligência operacional), onde já existe o conceito de métricas
derivadas/health score e essa semântica pode ser decidida junto com os outros insights baratos.
Sem gatilho de reabertura antes disso — o Explorer funciona plenamente para Boolean com
`raw`/`count`/`last`.

Requisição com `Aggregation` incompatível com o `ValueType` de qualquer `MetricKey` resolvida é
rejeitada **inteira** com `400 Bad Request` nomeando a métrica ofensora — não silenciosamente
ignorada nem parcialmente executada (mesmo princípio de atomicidade já usado em ADR-0001 para
ingestão).

### Guardrails (limites explícitos, não sugestões)

| Limite | Valor | Motivo |
|---|---|---|
| Devices por requisição | 10 | mantém o produto cartesiano `devices × metrics` controlável |
| Métricas por requisição | 10 | idem |
| Séries por requisição (`devices × metrics`) | 25 | protege contra o caso 10×10=100 sem proibir combinações razoáveis (ex. 5×5, 10×2) |
| Período máximo para `raw` | 24 horas | acima disso, o cliente deve agregar — dado bruto de múltiplos dias é caso de exportação, fora do escopo do Explorer |
| Período máximo para agregado | 90 dias | alinhado ao horizonte de retenção hot considerado no benchmark da Fase 1 |
| Pontos totais retornados | 20.000 | teto absoluto de resposta; ver fórmulas de aplicação abaixo |
| Bucket mínimo por período | tabela abaixo | evita ex. 90 dias com bucket de 1 minuto (130k+ buckets por série) |
| Timeout de comando | 8 segundos (`NpgsqlCommand.CommandTimeout`) | consulta de leitura ad-hoc, não hot path; mais folgado que os 5s já usados para webhook (ADR-0002) porque agrega mais dados por chamada |
| Cancellation | `HttpContext.RequestAborted` propagado até o comando Npgsql | cliente fecha aba/navega — consulta cara não continua rodando órfã |

**Piso de bucket por período** (linha vencedora = a primeira cujo período comporta a faixa
solicitada; período maior que 90 dias é rejeitado antes de chegar aqui pelo limite acima):

| Período solicitado (`ToUtc - FromUtc`) | Bucket mínimo permitido |
|---|---|
| ≤ 6 horas | `1m` |
| ≤ 24 horas | `5m` |
| ≤ 7 dias | `15m` |
| ≤ 30 dias | `1h` |
| ≤ 90 dias | `6h` |

Bucket solicitado mais fino que o piso da faixa → `400 Bad Request`, mensagem nomeando o piso
mínimo permitido para aquele período. `1d` continua selecionável para qualquer período ≥ 6h (mais
grosso que qualquer piso, portanto sempre permitido).

**Aplicação do limite de 20.000 pontos**:

- **Agregado**: contável antes de executar a query — `bucketCount = ceil((ToUtc - FromUtc) / bucketDuration)`;
  `estimatedPoints = bucketCount * seriesCount`. Se `estimatedPoints > 20000`, rejeita com
  `400 Bad Request` **antes** de tocar o banco — é aritmética barata, não há motivo para pagar o
  custo de uma query que sabemos que vai estourar o limite.
- **Raw**: não é possível estimar sem consultar (depende de quantas amostras existem de fato no
  período). Em vez de estimar, aplica-se um `LIMIT` por série:
  `perSeriesLimit = 20000 / seriesCount` (divisão inteira; com o teto de 25 séries o piso é 800,
  não precisa de proteção adicional contra valor degenerado). Se uma série atingir o limite, a
  resposta inclui essa série com `Truncated=true` e o cliente é instruído (pela UI, não pelo
  contrato) a estreitar o período ou reduzir a seleção — não há paginação/cursor no MVP.

### Erros de domínio e códigos HTTP

Reaproveita as exceções de aplicação já existentes (`Fluxo.Application/Common/Exceptions/`) e o
`ExceptionHandlingMiddleware` já existente (`Fluxo.Api/Middleware/ExceptionHandlingMiddleware.cs`)
— **sem** reescrever o middleware para os casos já cobertos:

| Violação | Exceção | HTTP |
|---|---|---|
| `DeviceIds`/`MetricKeys` vazio, exceder limites de contagem, `FromUtc >= ToUtc`, `Aggregation` desconhecida, `Bucket` fora da allow-list, bucket abaixo do piso, aggregation incompatível com `ValueType`, estimativa de pontos agregados > 20.000 | `ValidationException` (novo subtipo `TelemetryQueryValidationException : ValidationException`, ver abaixo) | `400` |
| Device referenciado não existe ou não pertence ao `workspaceId` da rota | `NotFoundException` | `404` |
| `MetricKey` referenciada não existe ou não pertence ao `workspaceId` da rota | `NotFoundException` | `404` |
| Usuário não é membro do workspace | `NotFoundException` (mesma convenção já usada em `GetAuthorizedWorkspaceUseCase` — não `403`, para não confirmar a existência do workspace a quem não é membro) | `404` |
| Timeout de execução (`CommandTimeout` estourado) | `QueryTimeoutException` (novo, não deriva das 5 exceções existentes) | `504` |

**Extensão necessária, explícita para não deixar o Codex inventar o formato**:

1. `TelemetryQueryValidationException : ValidationException` adiciona uma propriedade
   `string ErrorCode` (ex. `"TOO_MANY_DEVICES"`, `"TOO_MANY_METRICS"`, `"TOO_MANY_SERIES"`,
   `"RANGE_TOO_LARGE_FOR_RAW"`, `"RANGE_TOO_LARGE_FOR_AGGREGATE"`, `"BUCKET_BELOW_MINIMUM"`,
   `"BUCKET_NOT_ALLOWED_FOR_RAW"`, `"BUCKET_REQUIRED_FOR_AGGREGATE"`,
   `"AGGREGATION_INCOMPATIBLE_WITH_VALUE_TYPE"`, `"ESTIMATED_POINTS_EXCEEDS_LIMIT"`).
2. `ExceptionHandlingMiddleware.WriteProblemDetailsAsync` ganha uma linha adicional: se a exceção
   é `TelemetryQueryValidationException`, escrever `problem.Extensions["errorCode"] = ex.ErrorCode`
   (além do `traceId` já existente) — não altera o `switch` de status HTTP, que já trata
   `ValidationException` (e subtipos, por padrão de tipo em C#) como `400`.
3. Novo `QueryTimeoutException` (em `Fluxo.Application/Common/Exceptions/`, mesmo padrão de
   arquivo único por exceção já usado) e um novo `case QueryTimeoutException => (StatusCodes.
   Status504GatewayTimeout, "Query timeout", "...")` no mesmo `switch` do middleware — única
   alteração necessária no middleware existente.
4. `portal-web/src/types/index.ts`: `ApiProblem` ganha `errorCode?: string` opcional.

### Isolamento — workspace como fronteira

Reaproveita exatamente o padrão já estabelecido em `GetAuthorizedWorkspaceUseCase`/
`GetAuthorizedDeviceUseCase` (`Fluxo.Application/UseCases/Portal/`), sem inventar um novo
mecanismo de autorização:

1. `GetAuthorizedWorkspaceUseCase.ExecuteAsync(userId, workspaceId, WorkspaceMembershipRole.Viewer, ct)`
   — usuário precisa ser ao menos `Viewer` do workspace da rota. Não-membro → `NotFoundException`
   (404), replicando a convenção anti-enumeração já usada nesse use case.
2. **Novo** `GetAuthorizedDevicesUseCase` (variante em lote de `GetAuthorizedDeviceUseCase`):
   `SELECT * FROM devices WHERE Id = ANY(@deviceIds) AND WorkspaceId = @workspaceId`; se a
   contagem retornada for menor que `DeviceIds.Count`, algum id não existe ou pertence a outro
   workspace → `NotFoundException` nomeando o(s) id(s) ausente(s) — nunca revela se o id existe em
   outro workspace, apenas que "não foi encontrado neste workspace".
3. **Novo** `ResolveMetricDefinitionsUseCase`: `SELECT * FROM metric_definitions WHERE WorkspaceId
   = @workspaceId AND MetricKey = ANY(@metricKeys)`; mesma regra — contagem menor que
   `MetricKeys.Count` → `NotFoundException` nomeando a(s) chave(s) ausente(s).
4. **Não-negociável, defesa em profundidade**: a query final contra `telemetry_points` inclui
   `WHERE WorkspaceId = @workspaceId` **sempre**, com `@workspaceId` vindo do resultado da
   autorização (passo 1), nunca do corpo da requisição — mesmo que os passos 2-3 já tenham
   validado ownership, a query de leitura nunca confia apenas nisso. Um bug futuro nos passos 2-3
   não vira vazamento cross-workspace porque o filtro final também restringe.

**Testes negativos obrigatórios** (nomes fechados, não descrição vaga):

1. `Query_UserNotMemberOfWorkspace_Returns404NotFound`
2. `Query_DeviceIdFromOtherWorkspace_Returns404NotFound`
3. `Query_MetricKeyFromOtherWorkspace_Returns404NotFound` — cenário específico: duas
   `MetricDefinition` com o mesmo `MetricKey` textual (ex. `"temperature_c"`) em dois workspaces
   diferentes (permitido pela unique key `(WorkspaceId, MetricKey)`, ver ADR-0001); a query no
   workspace A nunca resolve para o `MetricDefinitionId` do workspace B.
4. `Query_ValidRequest_NeverReturnsPointsFromOtherWorkspace` — teste de dados: seed em dois
   workspaces com séries temporais sobrepostas no tempo; consulta no workspace A não retorna
   nenhum `TelemetryPoint` cujo `WorkspaceId` seja B, mesmo que device/metric IDs colidam em forma
   (não podem colidir em valor — são `Guid`, mas o teste prova o filtro, não a impossibilidade de
   colisão).
5. `Query_PartialDeviceOwnership_RejectsEntireRequestNotJustMissingDevice` — 1 de 2 devices
   pertence a outro workspace → toda a requisição falha com 404, não uma resposta parcial.

### Estratégia de query e índices (medir, não pré-otimizar)

Índice real hoje em `telemetry_points` (verificado em
`src/Fluxo.Infrastructure/Migrations/20260711172401_AddTelemetrySchemaV2.cs`):

```
IX_telemetry_points_WorkspaceId_DeviceId_MetricDefinitionId_Oc~
  ON telemetry_points (WorkspaceId, DeviceId, MetricDefinitionId, OccurredAtUtc)
PK_telemetry_points (OccurredAtUtc, Id)
IX_telemetry_points_MetricDefinitionId (MetricDefinitionId)
IX_telemetry_points_IngestionRecordId (IngestionRecordId)
```

Não há `DESC` no índice composto (diferente do que a redação conceitual original do ADR-0001
sugeria) e não há colunas `INCLUDE` para `NumericValue`/`BooleanValue`/`TextValue` (logo, nenhuma
consulta da Fase 2 é index-only scan — sempre há acesso à heap). **Nenhum índice novo é criado
nesta fase antecipadamente.** A Fase 2 mede primeiro.

**Query shapes obrigatórios para `EXPLAIN (ANALYZE, BUFFERS)`** (todos com
`WHERE WorkspaceId = @w AND OccurredAtUtc BETWEEN @from AND @to` mais os filtros abaixo):

| # | Forma | Filtros adicionais | Aggregation | Bucket | Período |
|---|---|---|---|---|---|
| Q1 | 1 device × 1 métrica, raw | `DeviceId = @d`, `MetricDefinitionId = @m` | raw | — | 24h |
| Q2 | 5 devices × 1 métrica, raw | `DeviceId IN (5)`, `MetricDefinitionId = @m` | raw | — | 24h |
| Q3 | 1 device × 5 métricas, raw | `DeviceId = @d`, `MetricDefinitionId IN (5)` | raw | — | 24h |
| Q4 | 10 devices × 10 métricas, raw (pior caso, com `perSeriesLimit`) | `DeviceId IN (10)`, `MetricDefinitionId IN (10)` | raw | — | 24h |
| Q5 | 1 device × 1 métrica, agregado típico | `DeviceId = @d`, `MetricDefinitionId = @m` | `avg`/`min`/`max`/`sum`/`count` | `1h` | 30d |
| Q6 | 10 devices × 10 métricas, agregado (pior caso combinado) | `DeviceId IN (10)`, `MetricDefinitionId IN (10)` | `avg` | `6h` | 90d |
| Q7 | `last` por bucket (via `DISTINCT ON` ou `array_agg` ordenado) | 5 devices × 5 métricas | `last` | `15m` | 7d |

**Volume mínimo de dados exigido para a medição ser representativa** (não medir em banco vazio):

- ≥ 5.000.000 de linhas em `telemetry_points` no workspace de benchmark;
- span de ≥ 4 partições mensais (para exercitar partition pruning de verdade, não só uma
  partição);
- ao menos um par `(DeviceId, MetricDefinitionId)` com ≥ 500.000 linhas (estressa Q1 com volume
  realista de uma série muito ativa, não uma série pequena que sempre daria índice rápido por
  seletividade artificial).

**Geração desse volume**: o simulador MQTT (`scripts/mqtt-device-simulator.py`) sempre grava
`occurredAtUtc = now()` (verificado no código — `payload_for` usa `datetime.now(timezone.utc)`),
não tem como retroceder data. Rodar o simulador em tempo real por meses para gerar histórico é
inviável. **Decisão**: um script novo e separado,
`scripts/benchmark/seed-telemetry-query-benchmark.py`, insere diretamente em
`telemetry_ingestion_records`/`telemetry_points` (via `COPY` binário, reaproveitando o mesmo
padrão de `NpgsqlBinaryCopyTelemetryPointWriter`, não uma nova abstração) com `OccurredAtUtc`
retroativo distribuído pelas partições necessárias. Não passa por MQTT nem pelo processor de
ingestão — isso já foi validado no benchmark da Fase 1; aqui o objetivo é só popular dado para
medir o lado de leitura. Detalhamento de parâmetros no handoff (seção de benchmark).

**Quando um novo índice é justificável**: só depois de rodar Q1-Q7 no volume acima com
`EXPLAIN (ANALYZE, BUFFERS)` e **apenas se** o plano mostrar uma das evidências abaixo — não por
intuição:

- `Seq Scan` em vez de `Index Scan`/`Index Only Scan` sobre `telemetry_points` para qualquer
  Q1-Q7 no volume medido;
- tempo de execução de Q6 (pior caso agregado) acima de 2 segundos (25% do `CommandTimeout` de
  8s, margem para variação de produção);
- `Buffers: shared read` desproporcional ao número de linhas realmente relevantes ao resultado
  (sinal de que o índice não está sendo seletivo o suficiente, obrigando a visitar páginas em
  excesso).

Candidatos **a avaliar apenas se o gate acima disparar** (não implementar preventivamente):
`INCLUDE (NumericValue, BooleanValue, TextValue)` no índice composto (troca tamanho de índice por
eliminar o heap fetch); reordenar para `(WorkspaceId, MetricDefinitionId, DeviceId, OccurredAtUtc)`
se o padrão real de uso do Explorer favorecer mais "1 métrica, muitos devices" do que o inverso
(só decidível observando uso real, não nesta revisão).

**Não introduzir TimescaleDB. Não implementar rollup/continuous aggregate.** A Query API consulta
`telemetry_points` raw diretamente em todos os casos — inclusive para os agregados (`avg`, `sum`
etc. são calculados on-the-fly via `GROUP BY date_bin(...)` a cada requisição, não lidos de uma
tabela pré-agregada). Isso é aceitável no MVP porque os pisos de bucket already limitam o número
de linhas agregadas por chamada; rollup físico fica candidato de fase futura, condicionado a
medição real mostrar necessidade.

### Biblioteca de gráficos (nova dependência — justificada)

`portal-web/package.json` hoje (verificado): `react`, `react-dom`, `react-router-dom` — nenhuma
biblioteca de visualização de dados. Decisão: **`recharts`** (`^2.x`).

- **Problema resolvido**: séries temporais (linha), representação de estado (Boolean) e
  composição de múltiplas séries/painéis em React declarativo, com tooltip/legenda prontos, sem
  construir primitivas de SVG/canvas do zero.
- **Peso/impacto**: ~110 KB gzip (recharts + dependências internas `d3-shape`/`d3-scale`/
  `d3-array`) — aceitável para um portal operacional autenticado (não é página pública sensível a
  SEO/bounce rate).
- **Manutenção**: ativamente mantida, uso amplo no ecossistema React, API estável desde a v2.
- **Motivo de não usar solução existente**: não há nenhuma primitiva de visualização no projeto
  hoje — não é uma escolha entre reaproveitar algo já presente vs. adicionar dependência, é greenfield.
- **Alternativa rejeitada**: `uPlot`/`lightweight-charts` (baseadas em canvas, bundle menor,
  melhor performance em dezenas de milhares de pontos). Rejeitadas para o MVP porque os guardrails
  desta ADR já limitam o volume típico de resposta (pisos de bucket mantêm agregados na casa de
  poucas centenas de pontos por série mesmo em 90 dias; `raw` é limitado a 24h e a um
  `perSeriesLimit` ≥ 800) — o teto de performance de SVG não é um problema prático no volume que o
  próprio contrato permite. A API declarativa do `recharts` reduz o risco de implementação para o
  Codex frente a construir tooltip/legenda/sincronização de hover manualmente em canvas. Reavaliar
  se uso real do Explorer expuser volumes que estressem SVG — não antecipado agora.

## Consequências

**Positivas**: um único endpoint cobre os quatro tipos de dispositivo do critério de produto
(`temperature_c`, `current_a`, `door_open`, `machine_state`) sem fork de backend; guardrails
fechados evitam que o Explorer vire vetor de negação de serviço contra o Postgres; isolamento
reaproveita 100% do mecanismo de autorização já existente, sem superfície nova de bug.

**Custos**: query agregada usa `date_bin`/`GROUP BY` a cada chamada (sem rollup físico) — aceitável
nos volumes permitidos pelos pisos de bucket, mas não escala arbitrariamente; se o piloto real
mostrar uso pesado de agregados de 90 dias, rollup físico volta à mesa como decisão futura,
não desta fase.

**`DEFERRED`**: `true_ratio` para Boolean — Fase 4, sem gatilho de data definido, decisão vigente é
não incluir. Paginação/cursor para `raw` truncado — decisão vigente é sinalizar `Truncated=true` e
pedir ao usuário para refinar o período; cursor de continuação fica para quando/se clientes reais
pedirem exportação de grandes volumes (fora do escopo de "Explorer", que é exploração interativa,
não extração em massa).

## Referências

- ADR-0001: `docs/adr/0001-telemetry-schema-v2.md`
- Relatório Fase 1: `docs/handoff/relatorio-fase-1-schema-v2-2026-07-11.md`
- Benchmark normalizado: `docs/benchmarks/phase1-normalized-2026-07-11.md`
- PostgreSQL — `date_bin`: https://www.postgresql.org/docs/current/functions-datetime.html
- PostgreSQL — `EXPLAIN`: https://www.postgresql.org/docs/current/sql-explain.html
- PostgreSQL — `DISTINCT ON`: https://www.postgresql.org/docs/current/sql-select.html#SQL-DISTINCT
- recharts: https://recharts.org/
