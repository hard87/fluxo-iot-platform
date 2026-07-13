# ADR-0001 — Contrato de telemetria V2 e schema chave-valor tipado

## Status

Aceito em 11 jul 2026 (Fase 0 do roadmap). Substitui a versão simplificada do ADR-0001
("Dictionary&lt;string,double&gt; genérico") descrita na nota estratégica interna do mesmo dia.
Essa versão simplificada foi confrontada e corrigida em
`Fluxo_Documento_Confronto_Arquitetural_MVP.docx` — este ADR formaliza a versão corrigida.
Nenhuma migration deve ser executada antes deste ADR estar aprovado e do baseline de carga
(seção "Benchmark obrigatório") estar registrado com números reais.

## Contexto

O schema atual (`TelemetryIngestionRecord` em
`Fluxo/src/Fluxo.Domain/Entities/TelemetryIngestionRecord.cs`) tem 5 colunas tipadas fixas
(`Temperature`, `Humidity`, `Battery`, `Rssi`, `UptimeSec`) e o contrato de ingestão
(`IncomingTelemetryMetrics` em `Fluxo.Worker.Ingestion/Models/IncomingTelemetryMessage.cs`) é uma
POCO fixa com os mesmos 5 campos — ambos hardcoded para um perfil de sensor ambiental. Um device
industrial (corrente, tensão, vibração), um rastreador de ativos (posição, estados textuais como
`machine_state`) ou qualquer estado booleano (`door_open`) não têm onde ir de forma tipada e
consultável.

A primeira proposta de correção (`Dictionary<string,double>`) foi rejeitada por só suportar
valores numéricos — não representa booleanos nem texto curto, então não é de fato genérica.

## Decisão

### Envelope canônico (V2)

```json
{
  "schemaVersion": 2,
  "sequence": 12345,
  "occurredAtUtc": "2026-07-11T14:20:00Z",
  "metrics": {
    "temperature_c": 24.6,
    "door_open": true,
    "machine_state": "running",
    "current_a": 8.13
  }
}
```

No backend, `metrics` é desserializado como `Dictionary<string, JsonElement>` (ou abstração
equivalente). O processor valida e converte cada valor para `Numeric`, `Boolean` ou `Text`.
Arrays, objetos aninhados e `null` dentro de `metrics` não são suportados no MVP
(`UnsupportedMetricValueType`). O contrato legado (5 campos fixos) continua aceito por um ciclo
de compatibilidade via adaptador, convertido internamente para V2; todo desenvolvimento novo
mira o contrato V2.

### Guardrails do contrato (obrigatórios, não delegáveis ao agente)

| Regra | Limite/decisão | Comportamento na violação |
|---|---|---|
| `schemaVersion` | obrigatório, valor `2` | versão desconhecida → rejeição explícita |
| Payload MQTT | máximo 32 KB | acima do limite → `PayloadTooLarge` |
| `sequence` | `Int64` positivo, obrigatório | duplicata por device → idempotência (`ON CONFLICT DO NOTHING`); negativo/ausente → rejeição |
| `occurredAtUtc` | UTC; futuro máx. +5 min; atraso máx. padrão 30 dias (configurável) | fora da janela → rejeição/quarentena `TimestampOutsideWindow` |
| métricas por mensagem | 1 a 64 | zero ou >64 → rejeição |
| `MetricKey` | 1-64 chars, lowercase, `^[a-z][a-z0-9._-]{0,63}$` | chave inválida → rejeição do payload inteiro |
| `Number` | double finito; `NaN`/`Infinity` proibidos | valor inválido → rejeição |
| `Boolean` | `true`/`false` JSON | persistido em `BooleanValue` |
| `Text` | UTF-8, até 256 caracteres | maior → rejeição; nunca truncar silenciosamente |
| Estruturas | arrays/objetos/`null` não suportados em `metrics` | `UnsupportedMetricValueType` |
| Atomicidade | mensagem inteira aceita ou rejeitada | nunca persistir metade das métricas de um mesmo ingestion record |

### Modelo de dados

```
TelemetryIngestionRecord         MetricDefinition                  TelemetryPoint
- Id                              - Id                               - Id
- TenantId                        - WorkspaceId                      - TenantId
- WorkspaceId                     - TenantId  (denormalizado,        - WorkspaceId
- DeviceId                          não-chave, ver invariante         - DeviceId
- Sequence                          tenant/workspace abaixo)          - MetricDefinitionId
- OccurredAtUtc                   - MetricKey                        - OccurredAtUtc
- ReceivedAtUtc                   - DisplayName                      - NumericValue?
- PayloadJson                     - ValueType   (Numeric|Boolean|Text)- BooleanValue?
- SchemaVersion                   - SemanticType?  (temperature, ...) - TextValue?
                                   - CanonicalUnit? (°C, A, V, g, ...) - IngestionRecordId
                                   - ExpectedIntervalSec?
                                   - MinExpectedValue? / MaxExpectedValue?
                                   - Status      (Discovered|Active|Ignored)
                                   - IsQueryable / IsAlertable
                                   - CreatedAtUtc / UpdatedAtUtc
```

- **Correção (revisão arquitetural de 11 jul 2026, ARCH-003)**: a redação anterior deste ADR
  atribuía duas responsabilidades distintas à mesma `CHECK` constraint. Uma constraint de linha
  (`CHECK`) só enxerga a própria linha — ela não pode validar o `ValueType` de uma
  `MetricDefinition` em outra tabela sem um `CHECK` referenciando subquery (não suportado por
  `CHECK` no PostgreSQL) ou um trigger. As duas responsabilidades são separadas explicitamente:
  - `TelemetryPoint.CHECK` garante **apenas** exactly-one-value-slot:
    `(NumericValue IS NOT NULL)::int + (BooleanValue IS NOT NULL)::int + (TextValue IS NOT NULL)::int = 1`.
  - Compatibilidade com `MetricDefinition.ValueType` (`Numeric` → `NumericValue`, etc.) é
    responsabilidade exclusiva do processor de ingestão, usando o `ValueType` já resolvido/cacheado
    da `MetricDefinition` antes de montar o `TelemetryPoint`. Mismatch é rejeitado como
    `MetricTypeMismatch` **antes** do INSERT — nunca chega a tentar gravar linha incompatível.
  - Nenhum trigger de banco é adicionado no hot path para essa validação: o processor já teria a
    `MetricDefinition` em cache para resolver o `MetricDefinitionId`, então a checagem de tipo é
    uma comparação em memória sem custo adicional de I/O. Um trigger duplicaria trabalho já feito
    na aplicação e adicionaria custo por linha sem benefício — não há gate de benchmark que
    justifique essa dependência adicional.
  - Teste obrigatório: teste de integração que envia `temperature_c` (já `Numeric`) como string e
    confirma rejeição `MetricTypeMismatch` sem gravação parcial; teste separado que tenta INSERT
    direto (via SQL bruto, contornando o processor) com dois slots de valor preenchidos ou nenhum,
    confirmando que o `CHECK` de banco rejeita independente da aplicação.
- Chave única em `MetricDefinition`: `(WorkspaceId, MetricKey)`. O primeiro payload válido com
  chave desconhecida cria a definição com `Status=Discovered` e `ValueType` inferido. Depois de
  criada, o tipo é estável: `temperature_c` não pode virar texto depois de já existir como
  número (`MetricTypeMismatch`).
- `PayloadJson` continua existindo em `TelemetryIngestionRecord` como auditoria do payload bruto
  — não é substituído pela projeção tipada.

### Invariante de tenant/workspace (ARCH revisão 11 jul 2026 — TENANT-INVARIANT)

Verificado em código (`Fluxo.Domain/Entities/Workspace.cs`, `Device.cs`,
`TelemetryIngestionRecord.cs`, `WorkspacesController.cs`, `WorkspaceRepository.cs`): **não existe
entidade `Tenant` separada no domínio.** `TenantId` é uma `string` (`"default"` por padrão,
`Workspace.TenantId` validado por regex `^[a-z0-9\-]{3,120}$`) carregada diretamente no próprio
`Workspace` — não uma FK para uma tabela `Tenants`. `Workspace.Id` (`Guid`) é a chave primária e é
**globalmente única** (`Guid.NewGuid()`, sem escopo por tenant). A autorização real de acesso a um
workspace passa por `WorkspaceMembership` (userId → WorkspaceId via `ListUserWorkspacesUseCase`/
`CreateWorkspaceUseCase`), não por comparação de `TenantId` — nenhum controller ou middleware
inspecionado usa `TenantId` como fronteira de autorização.

**Decisão: Possibilidade A, com denormalização não-chave por consistência de schema.**

- `MetricDefinition` é resolvida e ownership-validada exclusivamente via `WorkspaceId`. A unique
  key `(WorkspaceId, MetricKey)` já é suficiente para isolamento entre clientes, porque
  `Workspace.Id` é globalmente único — dois workspaces de tenants diferentes nunca colidem nessa
  chave, com ou sem `TenantId` na constraint.
- Apesar disso, `Device` e `TelemetryIngestionRecord` já denormalizam `TenantId` mesmo carregando
  `WorkspaceId` como FK — é o padrão existente no repositório (evita join para filtros/observa-
  bilidade por tenant, ex. dashboards operacionais cross-workspace). Para manter `MetricDefinition`
  consistente com essa convenção, ela **também carrega uma coluna `TenantId` denormalizada**,
  copiada de `Workspace.TenantId` no momento da criação — mas essa coluna **não entra na chave
  única nem em nenhuma decisão de autorização ou resolução de escopo**. É metadado de
  observabilidade/consulta, não fronteira de segurança.
- Isso não é reavaliado como "B" porque `TenantId` hoje não é uma fronteira de tenant multi-
  cliente real e enforced — é um rótulo de string com valor único `"default"` na maior parte do
  sistema atual. Tratá-lo como fronteira de segurança seria simular um invariante que o resto do
  código não garante. Se o Fluxo evoluir para multi-tenancy real (RLS por `TenantId`), a decisão
  precisa ser revisada nesse momento — a coluna denormalizada já presente facilita essa migração
  futura sem exigir nova migration de schema em `MetricDefinition`.
- Índice: `(WorkspaceId, MetricKey)` único (chave real); índice não-único adicional em
  `(TenantId)` opcional, só se benchmark de dashboards cross-tenant demonstrar necessidade —
  não adicionar antecipadamente.

### Proteção contra explosão de cardinalidade

- Máximo padrão de 500 `MetricDefinition` por workspace no MVP (configurável por plano).
- Máximo de 20 novas `MetricKey` descobertas por device em janela de 1 hora — acima disso,
  rejeitar novas chaves e registrar `MetricCardinalityGuardTriggered`.
- Toda nova `MetricKey` gera audit log (device, workspace, ingestion record de origem).
- `MetricDefinition` em `Status=Ignored` não gera `TelemetryPoint` novo; o `PayloadJson` bruto
  continua preservando a chave enquanto dentro da retenção.
- **Cache obrigatório** de `MetricKey → MetricDefinitionId` por workspace no processor de
  ingestão — sem isso, toda mensagem paga um lookup (ou insert condicional) por métrica no hot
  path. Ponto não detalhado no documento de confronto original; fechar na Fase 1.

#### Algoritmo distribuído do guardrail (ARCH-004, correção 11 jul 2026)

A versão anterior deste ADR não definia onde vive o contador de "20 novas chaves/device/hora"
nem seu comportamento sob restart ou múltiplas instâncias do worker. Um contador em memória é
insuficiente (dois workers, ou um restart, o zeram/duplicam de forma incorreta — cenários
descritos na revisão arquitetural). Decisão fechada:

**Nova entidade — auditoria de descoberta:**

```
MetricDefinitionDiscoveryAudit
-------------------------------
Id
WorkspaceId
TenantId          (denormalizado, mesma regra de MetricDefinition)
DeviceId
MetricKey
DiscoveredAtUtc
IngestionRecordId
```

Sem índice único adicional — é puramente um log append-only usado para contar a janela; a
proteção contra duplicidade de `MetricDefinition` continua sendo a unique key
`(WorkspaceId, MetricKey)`.

**Fluxo dentro da mesma transação de ingestão** (a transação já descrita em "Fluxo transacional
de ingestão" — isto substitui o passo "resolver/criar MetricDefinitions"):

```
para cada MetricKey da mensagem:
  se MetricKey está no cache do processor (workspace) -> usar MetricDefinitionId do cache, não
    paga custo de lock nem de contagem. (fast path — cobre o caso comum de chaves já conhecidas)
  senão (cache miss):
    pg_advisory_xact_lock(hashtext(WorkspaceId::text || ':' || DeviceId::text))
    -- lock é liberado automaticamente no commit/rollback da transação (advisory *transaction* lock)
    SELECT Id FROM MetricDefinition WHERE WorkspaceId = ... AND MetricKey = ...
    se encontrou -> outra transação já criou (ou está commitada); usar o Id, popular cache, seguir
    se não encontrou:
      SELECT COUNT(*) FROM MetricDefinitionDiscoveryAudit
        WHERE DeviceId = ... AND DiscoveredAtUtc > now() - interval '1 hour'
      se count >= 20 -> guardrail disparado para esta MetricKey
      senão:
        INSERT INTO MetricDefinition (...) ON CONFLICT (WorkspaceId, MetricKey) DO NOTHING
          RETURNING Id
        INSERT INTO MetricDefinitionDiscoveryAudit (...)
        popular cache
```

**Escopo do advisory lock**: `(WorkspaceId, DeviceId)` — serializa apenas a descoberta de chaves
*novas* para o mesmo device entre instâncias concorrentes, o que é exatamente o escopo do limite
("20/device/hora"). Não usa lock por `MetricKey` nem por workspace inteiro — isso serializaria
descoberta de devices diferentes sem necessidade. Chaves conhecidas (cache hit) nunca entram no
lock, então não pagam o custo de serialização — só descoberta paga.

**Relógio da janela**: `now()` do servidor PostgreSQL (`CURRENT_TIMESTAMP`), não do worker — evita
divergência de relógio entre instâncias.

**Comportamento exatamente no limite**: contagem é feita **antes** do insert; se
`count < 20` (ou seja, até a 20ª nova chave), a descoberta é permitida. A partir da 20ª já
existente na janela (ou seja, a tentativa de criar a 21ª), o guardrail dispara.

**Duas mensagens descobrindo a mesma chave nova concorrentemente:**
- Mesmo device: serializado pelo advisory lock — a segunda transação, após adquirir o lock, verá
  a `MetricDefinition` já criada pela primeira (se já commitada) ou terá sua própria tentativa de
  `INSERT ... ON CONFLICT DO NOTHING` retornando zero linhas (se a primeira ainda não commitou mas
  já inseriu — não pode acontecer de fato porque o lock é exclusivo por transação, então as duas
  transações para o mesmo device nunca chegam a essa seção ao mesmo tempo).
- Devices diferentes do mesmo workspace descobrindo a mesma `MetricKey` nova ao mesmo tempo: os
  advisory locks são por device, então não se serializam entre si. A proteção final é a unique
  constraint `(WorkspaceId, MetricKey)`: a transação que perde a corrida do `INSERT` recebe zero
  linhas de `RETURNING Id`, faz um `SELECT` de fallback para obter o `Id` já commitado pela
  vencedora, e segue normalmente — nenhuma das duas falha, nenhuma cria duplicata.

**Comportamento na violação do guardrail**: rejeita a **mensagem inteira** (mesma regra de
atomicidade da seção de guardrails do contrato — "nunca persistir metade das métricas de um mesmo
ingestion record" também vale aqui: não persistir as métricas já conhecidas enquanto descarta
silenciosamente a nova). A rejeição é registrada como `TelemetryIngestionRejection` com
`ErrorType=MetricCardinalityGuardTriggered`, elegível para o fluxo de rejeições existente
(visível no dashboard operacional; **não** elegível para reprocessamento automático via
`RejectionReprocessingWorker`, pois é uma falha determinística que se repetiria — mesma lógica já
aplicada a `Validation`/`PayloadInvalid`/`Duplicate` nesse worker).

**Rollback**: se qualquer etapa falhar (inclusive o guardrail disparando), a transação inteira
reverte — nenhum `TelemetryPoint`, `MetricDefinition` ou `MetricDefinitionDiscoveryAudit` fica
parcialmente gravado.

**Auditabilidade/métricas operacionais**: contador `metric_cardinality_guard_triggered_total`
(por workspace) exposto nas métricas internas de ingestão já existentes
(`IIngestionMetrics`); `MetricDefinitionDiscoveryAudit` é consultável para dashboard de novas
chaves por device/dia.

**Redis não é necessário**: o volume de descobertas (por definição, um evento raro pós-
estabilização do catálogo de um device) não justifica infraestrutura adicional — PostgreSQL com
advisory lock resolve na escala do MVP (até 1000 devices, seção de benchmark abaixo). Reavaliar
apenas se o benchmark de cardinality abuse demonstrar contenção real no Postgres.

### Fluxo transacional de ingestão

```
MQTT message
  -> validar tamanho e envelope
  -> autenticar identidade (pipeline atual, inalterado)
  -> iniciar transação
  -> INSERT TelemetryIngestionRecord
       ON CONFLICT (DeviceId, Sequence) DO NOTHING
       RETURNING Id
  -> se não retornou Id: marcar duplicata e encerrar sem TelemetryPoint
  -> validar todas as métricas
  -> resolver/criar MetricDefinitions (via cache por workspace) controladamente
  -> montar TelemetryPoints
  -> inserir todos os pontos em uma única operação multi-row parametrizada
  -> commit
  -> publicar métricas internas de ingestão
```

`ITelemetryPointWriter` deve ser uma interface que permita trocar a implementação de escrita sem
alterar os use cases. **Decisão consolidada após benchmark da Fase 1**: Npgsql Binary COPY é o
writer padrão aprovado para o pipeline de `TelemetryPoint` V2. `EfTelemetryPointWriter` pode
permanecer para testes, comparação técnica ou fallback explícito. Não implementar seleção dinâmica
de writer baseada na quantidade de métricas por mensagem.

### Particionamento mensal

**Correção (ARCH-005, 11 jul 2026)**: a única tabela particionada na Fase 1 é `TelemetryPoint`.
`TelemetryIngestionRecord` **não** é particionada nesta fase — o roadmap de produção
(`roadmap-production-1000-devices.md`) mencionava particionamento de
`telemetry_ingestion_records`, o que divergia deste ADR; o roadmap foi corrigido para apontar
para esta decisão. Motivo: `TelemetryPoint` é a tabela de alto volume (N linhas por mensagem, ver
"Benchmark obrigatório" e a tabela de custo abaixo); `TelemetryIngestionRecord` cresce 1:1 com
mensagens MQTT, uma ordem de grandeza menor, e seu padrão de consulta (auditoria pontual por
`Id`/`DeviceId`+`Sequence`, não varredura por intervalo de tempo) não se beneficia tanto de
partição por `RANGE`. Particionar `TelemetryIngestionRecord` fica como decisão futura,
condicionada a medir (não assumir): tamanho real de `PayloadJson` acumulado, política de
retenção definida, crescimento observado, padrão de índice necessário e custo de vacuum — dados
que só existem depois do benchmark da Fase 1 rodar contra volume real.

```sql
PARTITION BY RANGE (OccurredAtUtc)
PRIMARY KEY (OccurredAtUtc, Id)  -- Postgres exige a chave de partição na PK/UNIQUE
```

Índice principal por partição: `(WorkspaceId, DeviceId, MetricDefinitionId, OccurredAtUtc DESC)`.

A migration inicial cria partição do mês anterior, mês atual e 6 meses futuros.
`TelemetryPartitionMaintenanceService` roda no startup e diariamente às 03:00 UTC:

- garante partições até `current month + 6 meses`;
- cria partição com **advisory lock** (evita corrida entre instâncias);
- registra `partitions_ahead` e `last_partition_maintenance_success`;
- health check `Degraded` se houver menos de 3 partições futuras, `Unhealthy` se a partição do
  próximo mês não existir.

Retenção de `PayloadJson`/`TelemetryPoint` não é hardcoded — `RetentionOptions` define por
ambiente/plano, com defaults explícitos em configuração.

BRIN em `OccurredAtUtc` só entra depois que as partições estiverem grandes e `EXPLAIN ANALYZE`
mostrar benefício real — não é decisão antecipada.

## Benchmark obrigatório antes e depois

| Perfil de teste | Mensagens | Métricas/msg | Critério mínimo |
|---|---|---|---|
| Baseline atual | 30.000 | contrato atual | registrar números reais (P50/P95/P99, throughput, CPU/mem do Worker, CPU do Postgres, WAL gerado, tamanho de tabela, backlog máximo) |
| V2 leve | 30.000 | 5 escalares | zero perda; zero partition error; P95 ≤ 1,5× baseline |
| V2 médio | 30.000 | 16 escalares | backlog volta a zero em até 60s após o fim da carga |
| V2 denso | 30.000 | 32 escalares | sem OOM; CPU médio normalizado do DB < 75%; nenhuma rejeição não planejada |
| Idempotência | 5.000 duplicadas | 5-16 escalares | zero `TelemetryPoint` duplicado |
| Cardinality abuse | 1.000 | chaves mutantes | guardrail dispara; crescimento de `MetricDefinition` fica limitado |

**Pendente nesta sessão**: Docker Desktop não estava em execução no momento em que este ADR foi
escrito, então o baseline "número real" ainda não foi recoletado — a linha "Baseline atual"
precisa ser preenchida antes de qualquer migration ser aplicada (gate de saída da Fase 0).

**Atualização de implementação (11 jul 2026)**: Docker/PostgreSQL 16 ficaram disponíveis e a
migration foi validada em banco descartável (up/down/up; 8 partições; chave `RANGE
(OccurredAtUtc)`). A matriz de benchmark definida acima ainda está `NOT MEASURED`; esta validação
de DDL não substitui baseline nem prova capacidade.

**Benchmark executado em 11 jul 2026**: resultados completos, ambiente, WAL, recursos e storage
estão em `docs/benchmarks/phase1-2026-07-11.md`. Binary COPY foi exigido pelos números e
implementado via `ITelemetryPointWriter`.

**Consolidação metodológica do gate em 11 jul 2026**: o benchmark foi reexecutado com orçamento
explícito de recursos e está registrado em `docs/benchmarks/phase1-normalized-2026-07-11.md`.
Para este ADR, a métrica oficial de CPU do PostgreSQL é:

```
PostgreSQL normalized CPU utilization = average consumed CPU cores / allocated CPU cores
```

O gate do perfil `v2-dense` exige utilização **média normalizada inferior a 75%** durante o
benchmark sustentado. Pico de CPU deve ser registrado separadamente como evidência operacional,
mas não reprova isoladamente o gate da Fase 1. Todo benchmark usado para este gate deve declarar
explicitamente o CPU budget do PostgreSQL.

Na execução normalizada de 11 jul 2026, o orçamento explícito foi:

- PostgreSQL: 2 CPU cores / 2 GiB RAM;
- Worker: 2 CPU cores / 1 GiB RAM.

Nesse orçamento, o perfil dense/COPY consumiu 1,0801 cores médios, ou 54,01% de CPU normalizada
média. O pico foi 1,6010 cores, ou 80,05% normalizado, e foi registrado como evidência
operacional. Portanto, o gate de CPU médio normalizado passou. Binary COPY fica aprovado como
estratégia de escrita padrão do pipeline V2 com base nos benchmarks da Fase 1.

### Projeção física de armazenamento (ARCH-005B, correção 11 jul 2026)

O roadmap de produção calculava escala tratando 1 mensagem MQTT = 1 evento persistido. Isso deixou
de valer com o schema chave-valor: uma mensagem com N métricas gera **N linhas de
`TelemetryPoint`** (mais 1 linha de `TelemetryIngestionRecord`, independente de N). As projeções
abaixo são **matemática, não capacidade comprovada** — só o benchmark real (seção acima) confirma
se o Fluxo sustenta esse volume; nenhuma delas autoriza declarar suporte a 1000 devices antes do
benchmark rodar.

Meta de referência: 1000 devices, 1 mensagem/10s ⇒ 8.640.000 mensagens/dia (igual ao roadmap de
produção) ⇒ 8.640.000 `TelemetryIngestionRecord`/dia, independentemente do perfil.

| Perfil | Métricas/mensagem | `TelemetryPoint`/dia |
|---|---|---|
| `light` | 5 | 43.200.000 |
| `medium` | 16 | 138.240.000 |
| `dense` | 32 | 276.480.000 |

A Fase 0/Fase 1 deve medir e registrar, por perfil, no momento do benchmark (não estimar):

- bytes médios por linha de `TelemetryIngestionRecord` (dominado por `PayloadJson`);
- bytes médios por linha de `TelemetryPoint`;
- bytes de índice por partição mensal (`(WorkspaceId, DeviceId, MetricDefinitionId,
  OccurredAtUtc DESC)` + PK `(OccurredAtUtc, Id)`);
- WAL gerado por perfil durante a janela de carga do benchmark;
- tamanho final de tabela e de índice ao fim da carga de 30.000 mensagens de cada perfil;
- crescimento estimado por dia (extrapolando os números medidos, não uma nova medição);
- projeção para 7, 30 e 90 dias de retenção, por perfil;
- custo operacional aproximado por perfil (disco + IOPS, na infraestrutura de referência usada no
  benchmark).

**Objetivo desta medição**: tornar possível responder no futuro "quanto custa aproximadamente
manter um device no Fluxo por mês", não fixar um preço comercial nesta revisão. Os números
concretos (bytes/linha, WAL/perfil, projeção 7/30/90 dias) entram como nova linha na tabela de
benchmark quando a Fase 1 rodar — este ADR só fixa os perfis conceituais e a obrigação de medir.

## Consequências

**Positivas**: qualquer vertical (industrial, ambiental, mobilidade) grava sem migration de
schema; filtros dinâmicos e o motor de alertas ficam triviais de consultar
(`WHERE MetricDefinitionId = ...`); catálogo evita colisão semântica (`temp` vs `temperature` vs
`Temperature`) e variação de unidade.

**Custos**: uma leitura com N métricas vira N linhas em `TelemetryPoint` em vez de 1;
particionamento deixa de ser opcional e passa a ser dia 1; uma camada de indireção
(`MetricDefinition`) precisa de cache para não pesar o hot path.

**Migração**: as 5 colunas antigas ficam como deprecated por um ciclo de compatibilidade; toda
leitura direta dessas colunas fora do processor de ingestão precisa migrar para consultar
`TelemetryPoint`. Nenhuma coluna/tabela legada é apagada na mesma fase em que a projeção nova é
introduzida.

## Referências

- PostgreSQL — Table Partitioning: https://www.postgresql.org/docs/current/ddl-partitioning.html
- PostgreSQL — Date/Time Functions (`date_bin`/`date_trunc`): https://www.postgresql.org/docs/current/functions-datetime.html
- PostgreSQL — BRIN Indexes: https://www.postgresql.org/docs/current/brin.html
- Npgsql — COPY / Binary COPY: https://www.npgsql.org/doc/copy.html
- `Fluxo_Documento_Confronto_Arquitetural_MVP.docx` (confronto técnico de 11 jul 2026, seções 03-04)
