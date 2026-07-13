# Relatório Técnico — Fase 2

## 1. Resumo executivo

A Fase 2 entregou a Telemetry Query API, catálogo de métricas e Telemetry Explorer genérico. Numeric, Boolean e Text foram consultados pelo mesmo backend, sem código por vertical, migration nova, índice novo, alertas ou IA.

## 2. Estado inicial do repositório

- Branch: `snapshot-auth-portal-mvp-20260526`
- HEAD inicial/final: `5c8ced1f925e4151e78f7dd66ae8e071a5c2dbe7`
- Working tree inicial: suja, contendo a Fase 1 não commitada; todas as alterações existentes foram preservadas.
- Nenhum commit ou push foi realizado nesta execução.

## 3. Arquivos alterados

### Backend

- DTOs tipados de query e catálogo.
- `TelemetryQueryValidationException`, `QueryTimeoutException` e extensão pontual do middleware.
- Autorização em lote de devices e resolução de métricas.
- `QueryTelemetryUseCase`, catálogo, repository EF/Npgsql e controller dedicado.
- Registros de DI estritamente necessários.

### Frontend

- Tipos, `telemetryService`, `TelemetryExplorerPage`, rota e navegação.
- Recharts 2.x, CSS responsivo e tratamento defensivo do status numérico legado.

### Scripts

- `seed-telemetry-query-benchmark.py` com Binary COPY e command contract do ADR.
- `run-phase2-query-explain.py` para Q1–Q7.
- `validate-phase2-responsive.cjs` para QA local autenticada.

### Testes

- Guardrails, 21 combinações ValueType×Aggregation e cinco pisos de bucket.
- 12 cenários PostgreSQL de query, incluindo os cinco testes negativos de isolamento e truncamento real.
- Cinco testes de componente do Explorer.

### Documentação

- Benchmark Q1–Q7, roadmap, checklist e este relatório.

## 4. Query API implementada

- `POST /api/workspaces/{workspaceId}/telemetry/query`
- `GET /api/workspaces/{workspaceId}/metric-definitions`
- Request sem `WorkspaceId`; rota é a única fonte.
- Raw, avg, min, max, sum, count e last.
- Buckets em allow-list: 1m, 5m, 15m, 1h, 6h e 1d.
- Guardrails: 10 devices, 10 métricas, 25 séries, 24h raw, 90d agregado, 20.000 pontos, timeout de 8s e cancellation token.
- ErrorCodes propagados em ProblemDetails.

## 5. Isolamento por workspace

Passaram:

- `Query_UserNotMemberOfWorkspace_Returns404NotFound`
- `Query_DeviceIdFromOtherWorkspace_Returns404NotFound`
- `Query_MetricKeyFromOtherWorkspace_Returns404NotFound`
- `Query_ValidRequest_NeverReturnsPointsFromOtherWorkspace`
- `Query_PartialDeviceOwnership_RejectsEntireRequestNotJustMissingDevice`

A query final sempre filtra `WorkspaceId`. O schema real persiste `TelemetryPoint.DeviceId` como identificador textual; a API resolve os GUIDs autorizados para identificadores e devolve os GUIDs do contrato, sem alterar schema.

## 6. Telemetry Explorer

- Devices e métricas com seleção múltipla e limites visíveis.
- Interseção dinâmica das aggregations por ValueType.
- Bucket recalculado conforme período.
- Numeric em `LineChart`; unidades diferentes em painéis separados.
- Boolean em degrau `stepAfter`, domínio 0/1.
- Text e count em tabela cronológica.
- Estados de loading, vazio, truncamento, 400 e 504.
- Demonstração protegida: `temperature_c` (60), `current_a` (60), `door_open` (12) e `machine_state` (12); total de 144 pontos em 13 ms.

## 7. Responsividade

Validação headless autenticada de cinco páginas (`/workspaces`, dashboard, devices, Explorer e status) em 360, 768, 1024 e 1440 px: 20 combinações sem overflow horizontal ou erro visível. Evidências locais em `artifacts/phase2-qa/report.json` e screenshots correspondentes. Em 1440 px, filtros e gráficos usam a largura disponível; em 360 px, filtros ficam empilhados.

## 8. Testes

- Unitários: 71/71 verdes.
- Integração PostgreSQL: 40/40 verdes.
- Frontend: 5/5 verdes.
- `npm run build`: verde, com `tsc --noEmit`.
- `npm audit --omit=dev`: 0 vulnerabilidades.
- `docker compose config`: verde.
- `docker compose -f docker-compose.controlled-prod.yml config`: verde.

## 9. Benchmark Q1-Q7

- Banco isolado: `fluxo_phase2_benchmark`.
- 5.115.083 TelemetryPoint.
- 5 partições mensais com dados.
- Maior série: 500.000 pontos.
- Q1 0,448 ms; Q2 2,392 ms; Q3 3,554 ms; Q4 59,641 ms; Q5 13,081 ms; Q6 1624,705 ms; Q7 293,031 ms.
- Planos e buffers completos: [benchmark Q1–Q7](../benchmarks/phase2-query-explain-2026-07-12.md).

## 10. Decisão de índice

Índice novo necessário: **NÃO**.

Q6 ficou abaixo de 2 segundos. Seus Parallel Seq Scans são adequados porque aproximadamente 4,28 milhões de linhas são relevantes para o agregado de 90 dias/100 séries. Leituras compartilhadas foram proporcionais ao conjunto relevante. Nenhuma migration nova foi criada.

## 11. Container do portal

- Serviço: `frontend`; imagem: `fluxo-frontend` (`7da469598e37`).
- Serviço API: `api`; imagem: `fluxo-api` (`f9d974f4299d`).
- Rebuilds executados e containers recriados.
- API e frontend ficaram healthy; `/health` e portal responderam HTTP 200.
- A tentativa `--no-cache` do frontend excedeu o limite do terminal na instalação; a imagem final foi reconstruída após invalidação do source layer e executou novamente `tsc`/Vite dentro do Docker.

## 12. Clarificação contratual e gap de evidência

- Estado atual: `count` usa `SampleCount` como valor semântico e os três slots de valor ficam
  nulos. A exceção ao exactly-one-value-slot do DTO de resposta foi clarificada, sem alterar
  código, persistência ou migration, no [ADR-0003](../adr/0003-telemetry-query-api.md).
- A unidade `°C` dos dados de QA inseridos por `psql` no console Windows foi exibida como `?C`; é limitação do seed manual/encoding, não do contrato ou agrupamento.

## 13. Riscos restantes

- Bundle principal: 575,37 kB minificado (166,51 kB gzip); Vite emite warning acima de 500 kB. Não bloqueia o MVP, mas recomenda lazy loading futuro do Explorer.
- Recharts 2.x é a versão exigida pelo ADR, porém está fora de manutenção ativa; migração para v3 deve ser decisão futura explícita.
- A fonte Google externa é bloqueada pela CSP do nginx e cai no fallback local. Não houve quebra visual; hospedar fontes localmente é melhoria futura.
- Sessão em memória é perdida em refresh, limitação já documentada do portal MVP.

## 14. Gate final

Todos os gates binários da Fase 2 foram exercitados com evidência. Nenhuma capacidade da Fase 3 foi implementada.

PHASE 2 COMPLETE
