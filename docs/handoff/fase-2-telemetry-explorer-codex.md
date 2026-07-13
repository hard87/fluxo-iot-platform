# Handoff — Fase 2: Telemetry Query API + Telemetry Explorer (Codex GPT)

> Status histórico: execução concluída.  
> Resultado real: ver [Relatório da Fase 2](relatorio-fase-2-telemetry-explorer-2026-07-12.md).

Cole este documento inteiro como prompt. Não resuma, não pule seções. Este handoff cobre
**backend e frontend na mesma fase** — o Codex implementa o endpoint de consulta, o endpoint de
catálogo de métricas, e a tela `TelemetryExplorerPage` no `portal-web`.

Pré-requisito: Fase 1 concluída e validada (`docs/handoff/relatorio-fase-1-schema-v2-2026-07-11.md`,
gate `PHASE 1 COMPLETE` em `docs/benchmarks/phase1-normalized-2026-07-11.md`). ADR-0003
(`docs/adr/0003-telemetry-query-api.md`) aprovado — **leia o ADR na íntegra antes desta seção**;
este handoff é o índice executável, a especificação completa está lá.

## A. Contexto verificado (confirme antes de editar, não assuma)

- Repo: `D:\Officina404\Fluxo`. Branch: confirme com `git status`/`git branch` antes de começar.
- Verificado nesta revisão (11 jul 2026) — se algum caminho abaixo mudou, use o caminho real e
  relate a divergência no relatório de entrega, não edite silenciosamente um arquivo diferente:
  - `Fluxo.Domain/Entities/TelemetryPoint.cs`, `MetricDefinition.cs` — schema V2 já implementado.
  - `Fluxo.Infrastructure/Migrations/20260711172401_AddTelemetrySchemaV2.cs` — índices reais:
    `(WorkspaceId, DeviceId, MetricDefinitionId, OccurredAtUtc)` composto, PK
    `(OccurredAtUtc, Id)`, mais índices simples em `MetricDefinitionId` e `IngestionRecordId`.
    **Não existe `DESC` nem colunas `INCLUDE` nesse índice** — não assuma otimizações que não
    existem.
  - `Fluxo.Api/Controllers/TelemetryController.cs` e `PortalDevicesController.cs` — endpoints de
    leitura **existentes** devolvem `TelemetryIngestionRecord` paginado (payload bruto), não
    `TelemetryPoint`. Esta fase adiciona um endpoint novo, não modifica os existentes.
  - `Fluxo.Application/UseCases/Portal/GetAuthorizedWorkspaceUseCase.cs` e
    `GetAuthorizedDeviceUseCase.cs` — padrão de autorização a reaproveitar (não reinventar).
  - `Fluxo.Api/Middleware/ExceptionHandlingMiddleware.cs` — middleware de erro genérico existente;
    esta fase adiciona 1 case novo (`QueryTimeoutException` → 504) e 1 linha para propagar
    `ErrorCode` — não reescreve o resto.
  - `portal-web/package.json` — hoje só `react`, `react-dom`, `react-router-dom`. Esta fase
    adiciona `recharts` (ver ADR-0003, seção "Biblioteca de gráficos").
  - `portal-web/src/services/api/httpClient.ts`, `deviceService.ts`, `types/index.ts` — padrão de
    client HTTP (fetch + Bearer token em memória via `useAuth`, `ApiError`/`ApiProblem`) a
    reaproveitar para o novo `telemetryService.ts`.
  - `portal-web/src/App.tsx` — rotas existentes sob `/workspaces/:workspaceId/...` dentro de
    `ProtectedRoute`/`AppLayout`. Nova rota entra no mesmo padrão.
- Comportamento atual que **não pode regredir**: endpoints de telemetria existentes
  (`GET /api/devices/{id}/telemetry`, `GET /api/workspaces/{id}/devices/{deviceId}/telemetry`,
  `GET /api/workspaces/{id}/dashboard`) continuam funcionando sem alteração de contrato.

## B. Decisão arquitetural (ler ADR-0003 na íntegra: `docs/adr/0003-telemetry-query-api.md`)

Resumo do que implementar — **a especificação completa está no ADR, este é só o índice**:

1. `POST /api/workspaces/{workspaceId}/telemetry/query` — `TelemetryQueryRequest`/
   `TelemetryQueryResponse` exatamente como definidos no ADR (DTOs, matriz ValueType×aggregation,
   guardrails, piso de bucket por período, erros de domínio e códigos HTTP).
2. Isolamento: `GetAuthorizedWorkspaceUseCase` (papel mínimo `Viewer`) + novos
   `GetAuthorizedDevicesUseCase` (lote) e `ResolveMetricDefinitionsUseCase` (lote) — ver ADR seção
   "Isolamento". Filtro `WorkspaceId = @workspaceId` **sempre** presente na query final contra
   `telemetry_points`, mesmo após validação de ownership.
3. `TelemetryQueryValidationException`/`QueryTimeoutException` + extensão pontual do
   `ExceptionHandlingMiddleware` — ver ADR seção "Erros de domínio".
4. Query de agregação via `date_bin`/`GROUP BY` com SQL bruto (Npgsql), não LINQ-to-EF — EF Core
   não traduz `date_bin` nativamente. Raw usa LINQ-to-EF normalmente (sem agregação).
5. **Novo endpoint de catálogo** (não estava explícito no pedido original, mas é pré-requisito do
   fluxo do Explorer): `GET /api/workspaces/{workspaceId}/metric-definitions` — devolve o
   catálogo de métricas do workspace (`IsQueryable=true` apenas), **não filtrado por device
   selecionado** (decisão fechada: filtrar por "métricas que este device específico já emitiu"
   exigiria uma consulta `DISTINCT` cara sobre `telemetry_points` com janela de tempo ambígua —
   fora de escopo do MVP; o Explorer mostra o catálogo do workspace inteiro, e uma métrica nunca
   emitida por um device simplesmente resulta em série vazia, que já é um estado tratado).

   ```csharp
   public sealed record MetricDefinitionResponse(
       Guid Id, string MetricKey, string DisplayName, string ValueType,
       string? SemanticType, string? CanonicalUnit, string Status, bool IsQueryable);
   ```

   Autorização: `GetAuthorizedWorkspaceUseCase(userId, workspaceId, Viewer)`. Filtro:
   `WHERE WorkspaceId = @workspaceId AND IsQueryable = true`. Sem paginação (teto de 500
   `MetricDefinition`/workspace já garantido pelo guardrail de cardinalidade do ADR-0001 — resposta
   sempre pequena).
6. `TelemetryExplorerPage` no `portal-web` — ver seção E.

## C. Escopo de alteração

**Backend — permitido**:
- `Fluxo.Application/DTOs/Telemetry/` (novos DTOs de request/response).
- `Fluxo.Application/UseCases/Telemetry/` (novo: `QueryTelemetryUseCase`,
  `ListWorkspaceMetricDefinitionsUseCase`).
- `Fluxo.Application/UseCases/Portal/` (novos: `GetAuthorizedDevicesUseCase`,
  `ResolveMetricDefinitionsUseCase` — seguindo o padrão dos use cases existentes na mesma pasta).
- `Fluxo.Application/Common/Exceptions/` (novos: `TelemetryQueryValidationException`,
  `QueryTimeoutException`).
- `Fluxo.Application/Interfaces/Repositories/` (novo: `ITelemetryQueryRepository`).
- `Fluxo.Infrastructure/Repositories/` (novo: `TelemetryQueryRepository`, implementação com SQL
  bruto via Npgsql para os caminhos agregados; LINQ-to-EF aceitável para o caminho `raw`).
- `Fluxo.Api/Controllers/` (novo: `TelemetryQueryController.cs` — **não** adicionar estas rotas em
  `TelemetryController.cs` nem `PortalDevicesController.cs` existentes; controller novo e
  dedicado, para não misturar o contrato de leitura paginada de `TelemetryIngestionRecord` já
  existente com o contrato novo de consulta agregada de `TelemetryPoint`).
- `Fluxo.Api/Middleware/ExceptionHandlingMiddleware.cs` (só a extensão pontual descrita no ADR —
  1 case novo + 1 linha de `Extensions`, não reescrever o arquivo).

**Backend — proibido nesta fase**: alterar `TelemetryController.cs`/`PortalDevicesController.cs`
existentes; alterar schema/migrations (Fase 2 é leitura pura, nenhuma migration nova); implementar
rollup/materialização; introduzir TimescaleDB ou qualquer dependência de banco/mensageria nova;
tocar no motor de alertas (ADR-0002, Fase 3, fora de escopo).

**Frontend — permitido**:
- `portal-web/package.json` (adicionar `recharts`).
- `portal-web/src/services/api/telemetryService.ts` (novo, mesmo padrão de `deviceService.ts`).
- `portal-web/src/types/index.ts` (novos tipos: `TelemetryQueryRequest`, `TelemetryQueryResponse`,
  `TelemetrySeriesResponse`, `TelemetryPointResponse`, `MetricDefinitionResponse`; extensão
  `ApiProblem.errorCode?: string`).
- `portal-web/src/pages/TelemetryExplorerPage.tsx` (novo).
- `portal-web/src/components/` (novos componentes de suporte — ver seção E; nomes ficam a critério
  da implementação, mas devem seguir o padrão de componente único e pequeno já usado, ex.
  `DeviceStatusBadge.tsx`).
- `portal-web/src/App.tsx` (nova rota `/workspaces/:workspaceId/explorer`).
- `portal-web/src/components/AppLayout.tsx` (novo link de navegação para o Explorer).

**Frontend — proibido nesta fase**: dashboard builder, drag-and-drop, editor de widgets, qualquer
tela de regras/alertas, qualquer sugestão gerada por IA. Não adicionar biblioteca de state
management (Redux/Zustand/React Query) — o padrão existente é `useState`/`useEffect` direto nas
páginas (ver `DashboardPage.tsx`/`DevicesPage.tsx`); manter consistência, não introduzir uma
segunda forma de buscar dados.

## D. Segurança e isolamento (ler ADR-0003 seção "Isolamento" na íntegra)

- `workspaceId` só existe na rota — o `TelemetryQueryRequest` **não tem campo `WorkspaceId`**.
  Confirme isso no DTO antes de implementar; se sentir necessidade de adicionar, pare — é sinal de
  desvio da decisão fechada.
- Toda query final contra `telemetry_points` inclui `WorkspaceId = @workspaceId` no `WHERE`,
  vindo do resultado de `GetAuthorizedWorkspaceUseCase`, nunca do corpo.
- `GetAuthorizedDevicesUseCase`/`ResolveMetricDefinitionsUseCase` rejeitam a requisição **inteira**
  (404) se qualquer id/chave não pertencer ao workspace — não filtram silenciosamente os inválidos
  e seguem com o resto.

## E. Telemetry Explorer — fluxo, estados e visualização

Fluxo da tela (`TelemetryExplorerPage`, rota `/workspaces/:workspaceId/explorer`):

1. Workspace já vem do contexto de rota (`useParams`), igual às demais páginas — sem seletor
   adicional de workspace dentro da própria tela.
2. Seletor de devices: busca `GET /api/workspaces/{workspaceId}/devices` (endpoint **já
   existente**, `deviceService.listDevices`) — multi-seleção, até 10 (limite do ADR-0003; UI
   desabilita seleção adicional ao atingir o teto, com mensagem, não deixa o usuário descobrir o
   limite só depois de errar a chamada).
3. Ao ter ao menos 1 device selecionado, busca
   `GET /api/workspaces/{workspaceId}/metric-definitions` (novo endpoint, seção B.5) e lista
   `MetricKey`/`DisplayName`/`ValueType`/`CanonicalUnit` como opções — multi-seleção, até 10.
4. Período: dois campos de data/hora (`fromUtc`/`toUtc`). Não implementar seletor de timezone —
   ADR-0003 fixa tudo em UTC; a UI pode exibir os timestamps convertidos para o timezone do
   navegador **apenas para leitura** (formatação local de exibição), mas envia e recebe UTC.
5. Aggregation: `select` com as opções válidas **filtradas dinamicamente pelo `ValueType` das
   métricas selecionadas** — se há métricas de mais de um `ValueType` selecionadas, mostrar apenas
   as opções compatíveis com **todas** (interseção da matriz do ADR-0003); se a interseção for
   vazia (ex. selecionou uma `Numeric` e uma `Text` querendo `avg`), a única opção viável é `raw`
   ou `count`/`last` — a UI deve deixar isso visível, não deixar o usuário montar uma combinação
   que o backend vai rejeitar.
6. Bucket: só habilitado/obrigatório quando `Aggregation != raw`; opções limitadas às permitidas
   pelo piso da tabela do ADR-0003 para o período já selecionado (recalcular ao mudar o período).
7. Botão "Executar consulta" — desabilitado se: nenhum device selecionado, nenhuma métrica
   selecionada, período inválido (`from >= to`), ou combinação aggregation/bucket inválida (já
   filtrada nos passos 5-6, mas revalidar no clique como defesa final client-side antes de
   chamar a API).
8. Resultado: um painel de gráfico **por combinação `(ValueType, CanonicalUnit)`** entre as séries
   retornadas — nunca um gráfico único com métricas de unidades diferentes no mesmo eixo. Regra
   fechada do MVP: agrupar séries por `(ValueType, CanonicalUnit ?? "sem unidade")`; cada grupo
   vira um painel de gráfico separado, empilhados verticalmente na página. Sem eixo duplo (dual
   y-axis) em nenhuma circunstância no MVP — é a causa clássica de gráfico enganoso, e não há
   necessidade de resolver isso agora.

### Representação por `ValueType`

- **Numeric**: gráfico de linha (`recharts` `LineChart`/`Line`), eixo X = tempo, eixo Y = valor.
  Múltiplas séries do mesmo grupo (mesma unidade) no mesmo painel, uma linha por
  `(deviceId, metricKey)`, com legenda identificando `deviceName + metricKey` (resolver
  `deviceName` a partir da lista de devices já carregada, não expor só o Guid).
- **Boolean**: **não é linha contínua** — representação de estado (step chart / degrau):
  `recharts` `LineChart` com `type="stepAfter"` na `Line`, domínio do eixo Y fixo em
  `[0, 1]`/`false`-`true` com apenas 2 ticks rotulados, ou uma representação de barras de estado
  (timeline de intervalos "true"/"false"). Para o MVP, `type="stepAfter"` é suficiente e não exige
  componente novo — decisão fechada para não deixar o Codex escolher uma representação ad hoc.
- **Text**: **nunca forçar em gráfico de linha**. Representação: tabela/lista cronológica simples
  (timestamp + valor), a mesma para `raw`/`last`/`count` — para `count`, a "lista" mostra
  `bucketStart` + contagem (é um número, mas continua sendo lista, não linha, para manter Text
  visualmente consistente independente da agregação escolhida).

### Estados obrigatórios da UI

| Estado | Gatilho | Tratamento |
|---|---|---|
| Loading (devices) | carregando lista de devices | mensagem "Carregando dispositivos..." (padrão já usado em `DevicesPage`) |
| Loading (métricas) | carregando catálogo após seleção de device | mensagem equivalente |
| Loading (consulta) | requisição de query em voo | desabilita botão "Executar consulta", mostra indicador |
| Empty (sem devices) | workspace sem devices cadastrados | mensagem + link para cadastro (`NewDevicePage`, já existente) |
| Empty (sem métricas) | workspace sem `MetricDefinition` `IsQueryable` | mensagem "nenhuma métrica disponível ainda — envie telemetria primeiro" |
| Nenhuma métrica selecionada | usuário não marcou nenhuma métrica | botão desabilitado, sem chamada à API |
| Range excessivo | usuário escolhe período > limite do ADR (24h raw / 90d agregado) | validação client-side bloqueia envio, mensagem citando o limite exato; se ainda assim chegar ao backend (ex. relógio local divergente), `ApiErrorMessage` exibe o `detail` do `ProblemDetails` |
| Erro de validação (400) | qualquer guardrail do ADR-0003 violado | `ApiErrorMessage` já existente exibe `problem.detail`; usar `problem.errorCode` (novo campo) para destacar visualmente qual filtro corrigir (ex. sublinhar o campo de bucket se `errorCode === "BUCKET_BELOW_MINIMUM"`) |
| Métrica sem dados no período | série retornada com `points: []` | painel daquela série mostra "sem dados neste período" em vez de gráfico vazio silencioso |
| Múltiplas séries | mais de uma combinação device×métrica retornada | agrupadas por `(ValueType, CanonicalUnit)` conforme regra acima |
| Unidades diferentes | métricas de `CanonicalUnit` distintos selecionadas juntas | painéis separados automaticamente (regra acima) — nunca um aviso de erro, é o comportamento esperado |
| Resultado truncado | qualquer série com `Truncated=true` | banner explícito "resultado truncado — refine o período ou reduza a seleção", não silencioso |
| Erro genérico/servidor (500/504) | falha inesperada ou timeout | `ApiErrorMessage`; para 504 especificamente, mensagem sugerindo reduzir período/seleção (é a ação que resolve um timeout nesta API) |

## F. Matriz de testes

**Backend — unitários**:
- `QueryTelemetryUseCase`: cada guardrail do ADR-0003 dispara o `ErrorCode` correto (11 casos —
  um por linha da tabela de erros de domínio); matriz ValueType×Aggregation aceita/rejeita
  exatamente conforme a tabela do ADR (21 combinações: 3 ValueTypes × 7 aggregations); piso de
  bucket rejeita bucket abaixo do mínimo para cada uma das 5 faixas de período.

**Backend — integração (PostgreSQL descartável)**:
- Os 5 testes negativos de isolamento nomeados no ADR-0003 seção "Isolamento" (itens 1-5).
- `Query_Raw_ReturnsPointsInAscendingOrder`.
- `Query_Aggregated_BucketBoundariesUseUtc`.
- `Query_EmptySeriesForDeviceWithNoDataInPeriod_ReturnsEmptyPointsNotError`.
- `Query_RawExceedsPerSeriesLimit_SetsTruncatedTrue`.
- `Query_AggregatedEstimateExceedsMaxPoints_Returns400BeforeExecutingQuery` (prova que a
  validação acontece antes do acesso ao banco — ex. via mock/spy de repositório não chamado).
- `Query_CancelledToken_ThrowsWithoutExecuting` (prova que o `CancellationToken` é propagado).
- `MetricDefinitions_List_OnlyReturnsIsQueryableTrue`.

**Backend — benchmark de leitura** (ver ADR-0003 seção "Estratégia de query e índices"):
- Rodar `scripts/benchmark/seed-telemetry-query-benchmark.py` (novo — contrato abaixo) para atingir
  o volume mínimo especificado no ADR (≥ 5.000.000 linhas, ≥ 4 partições, ≥ 1 par
  device/métrica com ≥ 500.000 linhas).
- Executar `EXPLAIN (ANALYZE, BUFFERS)` para Q1-Q7 (tabela do ADR) e registrar os planos em
  `docs/benchmarks/phase2-query-explain-<data>.md`.
- Aplicar o critério "quando um novo índice é justificável" do ADR — só criar índice novo se
  algum dos 3 gatilhos disparar, e documentar a decisão (criado ou não, com o plano que motivou).

**`COMMAND CONTRACT — TO BE IMPLEMENTED IN PHASE 2`** — `scripts/benchmark/seed-telemetry-query-benchmark.py`:

| Argumento | Tipo | Comportamento |
|---|---|---|
| `--connection-string` | string | conexão Npgsql direta ao banco de benchmark. |
| `--workspace-id` | guid | workspace já existente (criado via `provision-simulated-devices.py`) onde semear os dados. |
| `--devices` | int | quantos `DeviceId` distintos usar (devices já devem existir). |
| `--metrics` | int | quantas `MetricDefinition` distintas usar (já devem existir/ser criadas antes, para não disparar o guardrail de cardinalidade). |
| `--months-back` | int | quantos meses retroceder a partir do mês atual (define quantas partições são exercitadas). |
| `--points-per-day-per-series` | int | quantos `TelemetryPoint` gerar por dia por par device×métrica (controla o volume total e permite atingir o mínimo de 500.000 em uma série específica ajustando este valor × dias). |
| `--seed` | int | reprodutibilidade determinística dos valores gerados. |
| `--batch-size` | int | tamanho do lote de `COPY` binário por transação. |

Insere direto via `NpgsqlBinaryCopyTelemetryPointWriter` (reaproveitado, não uma nova
implementação de escrita) em `telemetry_ingestion_records` + `telemetry_points`, com
`OccurredAtUtc` distribuído retroativamente conforme `--months-back`/`--points-per-day-per-series`
— não passa por MQTT, não faz idempotência por `Sequence` (não é teste de ingestão, Fase 1 já
validou isso).

**Frontend**:
- Teste de componente: `TelemetryExplorerPage` desabilita "Executar consulta" sem device/métrica
  selecionados.
- Teste de componente: seleção de métricas de `ValueType` diferentes reduz as opções de
  `Aggregation` disponíveis para a interseção compatível.
- Teste de componente: série com `points: []` renderiza "sem dados", não um gráfico vazio.
- Teste de componente: séries com `CanonicalUnit` diferentes renderizam em painéis separados, não
  no mesmo `LineChart`.
- Teste de componente: `Truncated=true` renderiza o banner de truncamento.
- Teste manual (critério de produto, seção G): demonstrar `temperature_c`, `current_a`,
  `door_open` e `machine_state` no mesmo Explorer, sem alterar backend por vertical.

## G. Critério de aceite (checklist binário)

- [ ] `dotnet test Fluxo.slnx` verde (novos testes incluídos).
- [ ] `npm run build` verde no `portal-web` (inclui `tsc --noEmit`).
- [ ] Os 5 testes negativos de isolamento (ADR-0003) passam.
- [ ] Matriz ValueType×Aggregation validada por teste (21 combinações).
- [ ] `EXPLAIN (ANALYZE, BUFFERS)` de Q1-Q7 executado no volume mínimo especificado; resultado
      documentado em `docs/benchmarks/phase2-query-explain-<data>.md`; decisão de criar ou não
      índice novo registrada com justificativa.
- [ ] Demonstração manual: `temperature_c`, `current_a`, `door_open`, `machine_state` visualizados
      no mesmo Explorer sem alteração de backend por vertical (critério de produto).
- [ ] Nenhuma migration nova criada nesta fase.
- [ ] `docs/roadmap-production-1000-devices.md` atualizado (Fase 2 marcada com evidência).

## H. Relatório de entrega (obrigatório ao final)

Arquivos alterados; decisões tomadas (especialmente onde os caminhos reais divergiram deste
documento); resultado dos testes; planos de `EXPLAIN` obtidos e se algum índice novo foi criado
(com justificativa); gaps conhecidos; riscos não resolvidos — em particular, se `recharts` se
mostrar insuficiente para algum caso real (ex. volume de pontos maior que o previsto), registrar
isso explicitamente para reavaliação futura, não trocar de biblioteca silenciosamente dentro desta
fase.
