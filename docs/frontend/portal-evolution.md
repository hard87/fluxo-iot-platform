# Registro de evolução do Fluxo Portal

Este documento é a fonte central para acompanhar a evolução do front-end do Fluxo. O roadmap descreve a direção; este registro mostra o estado real de cada entrega, a evidência disponível e o que ainda falta para considerá-la concluída.

Última atualização: 2026-09-08.

## Como interpretar os estados

- **Planejado:** objetivo descrito, mas implementação ainda não iniciada.
- **Em implementação:** há trabalho em andamento e o aceite ainda não foi atendido.
- **Implementado:** o código existe, porém ainda não passou por toda a validação prevista.
- **Em validação:** implementação e verificações automatizadas existem; falta uma ou mais provas manuais ou operacionais.
- **Comprovado:** critérios de aceite atendidos e evidências registradas.
- **Bloqueado:** existe um impedimento concreto, identificado na própria entrega.
- **Concluído:** entrega comprovada, documentada e incorporada à linha de evolução do produto.

Uma entrega não deve avançar de estado somente porque o código foi escrito. A promoção exige evidência proporcional ao risco: teste automatizado, build, inspeção visual, navegação por teclado e, quando houver integração, validação contra a API real.

## Marco atual — console operacional investigável

Objetivo do marco: transformar o portal em uma interface que responda rapidamente a três perguntas: o que está acontecendo, em qual dispositivo e onde investigar a causa.

| ID | Entrega | Estado atual | Evidência principal | Falta para comprovar |
| --- | --- | --- | --- | --- |
| C1 | Persistência da sessão | Em validação | `portal-web/src/hooks/useAuth.tsx` e `useAuth.test.tsx` | Regressão manual de login, recarga e logout com backend real |
| C2 | Resumo e tabela acessível das séries | Em validação | `TelemetrySeriesPanel.tsx`; suíte do portal aprovada | Leitor de tela/teclado e conferência com séries reais |
| C3 | Visão operacional única do dispositivo | Em validação | `DeviceOperationalOverview.tsx`, `OperationalHistory.tsx` e `operationalMetrics.test.ts` | Revisão visual nos breakpoints e confirmação das unidades reais |
| C4 | Página 404 integrada ao shell | Em validação | `NotFoundPage.tsx` e `AppShell.test.tsx` | Navegação manual por rota inexistente, teclado e mobile |
| C5 | Sinal de atualização/freshness | Em validação | `TelemetryActivity.tsx` e `DashboardPage.test.tsx` | Confirmar linguagem e comportamento com dispositivo online, atrasado e offline |
| C6 | Busca e filtro de dispositivos | Em validação | `DevicesPage.tsx`; build e testes aprovados | Validar volume, teclado, estado vazio e viewport estreita |
| C7 | Histórico com 1h, 6h e 24h e seleção de métricas | Em validação | `OperationalHistory.tsx` e `operationalMetrics.test.ts` | Validar dados reais, legenda clicável e ausência de pontos sem inventar zero |
| C8 | Trilha de mensagens rejeitadas | Em validação | página, serviço, endpoint e caso de uso implementados; 8 testes de página no portal; 9 testes de integração exercitando o pipeline HTTP real (autenticação, isolamento entre workspaces, paginação, busca, vazio, prévia limitada); estados vazio/carregando/erro/populado e navegação por teclado conferidos no dev server isolado nos 4 breakpoints | Executar o endpoint contra PostgreSQL real / stack compartilhada — bloqueado enquanto a frente de alertas estiver na árvore de trabalho (ver "Restrições e riscos") |
| A1 | Temas e preferências para baixa visão | Planejado | Etapas 13 e 14 de `ui-roadmap.md` | Definir contrato de temas e validar contraste, zoom e preferência do usuário |
| A2 | Séries distinguíveis sem depender de cor | Planejado | Etapa 11 de `ui-roadmap.md` | Definir traços/símbolos e executar teste de daltonismo e alto contraste |

## Evidências registradas

Execuções realizadas em 2026-09-08 sobre o estado atual da árvore de trabalho:

| Verificação | Resultado | Alcance |
| --- | --- | --- |
| `npm run test` | 44 testes aprovados (11 arquivos) | Componentes, páginas, autenticação, shell e utilitários do portal; inclui 8 testes de `TelemetryRejectionsPage` |
| `npm run build` | Aprovado | TypeScript e bundle de produção do portal (bundle ~618 kB, aviso de tamanho conhecido) |
| `dotnet build Fluxo.slnx --no-restore` | Aprovado, sem warnings ou erros | Solução backend |
| `dotnet test Fluxo.slnx --no-build` | 97 testes unitários e 26 de integração aprovados; 41 ignorados | Regras e integrações disponíveis no ambiente atual; +9 testes de integração novos para `/api/workspaces/{id}/telemetry-rejections` |

Os 41 testes de integração ignorados dependem de um PostgreSQL real (Testcontainers/servidor descartável) e não estavam disponíveis neste ambiente; não contam como evidência positiva nem negativa.

Essas evidências comprovam integridade de compilação e comportamento automatizado coberto, mas não substituem validação visual, acessível ou operacional.

## Restrições e riscos conhecidos

- O bundle de produção está em aproximadamente 618 kB e ultrapassa o aviso padrão de 500 kB. Não bloqueia o marco atual, mas exige uma etapa posterior de divisão de código e análise de dependências.
- Há 41 testes de integração ignorados por dependências ou configuração do ambiente; eles não podem ser contabilizados como evidência positiva.
- A stack compartilhada (`docker-compose.yml`) está parada e **não foi reconstruída**. A árvore de trabalho contém a frente de alertas, que altera o caminho de escrita da ingestão (`TelemetryIngestionRepository` chama `AlertTransactions.LockIngestionAsync`/`EnqueueAsync`), adiciona o hosted service `AlertEvaluationWorker` ao worker de ingestão, injeta `AlertModelConfiguration` no `FluxoDbContext` e traz a migration `20260906225556_AddAlertBackend`. Subir a stack a partir desta árvore exigiria construir as imagens `api`+`worker` com esse código e aplicar `dotnet ef database update` (que roda **todas** as migrations, inclusive a de alertas) contra o volume `postgres_data` compartilhado — ou o worker de alertas passaria a consultar tabelas inexistentes. Qualquer um desses caminhos incorpora a frente paralela à stack, o que o marco proíbe. Por isso a prova contra PostgreSQL real de C8 fica pendente.
- C8 **não** introduz migration nem mudança de schema: a tabela `telemetry_ingestion_rejections` já existe desde a frente de reprocessamento; o endpoint apenas adiciona um método de leitura (`ListByTenantWorkspaceAsync`). Isso mantém a menor ação de desbloqueio simples (ver "Próximo portão").
- Algumas métricas recebidas ainda não informam unidade canônica. A interface deve expor a ausência, não inferir ou inventar unidade.
- A árvore de trabalho contém mudanças de outras frentes. Antes de gerar release, as alterações precisam ser isoladas em commits ou worktrees coerentes.

## Próximo portão de validação

O trabalho atual está no portão **C8 — prova operacional da trilha de rejeições**, sem abandonar a validação visual pendente de C1–C7.

Para atravessar esse portão:

1. Isolar o conjunto do portal e da leitura de rejeições das mudanças de alertas já presentes na árvore.
2. Reconstruir API e portal em ambiente de teste controlado.
3. Confirmar autenticação e isolamento entre workspaces no endpoint de rejeições.
4. Exercitar busca, paginação, estados vazio/erro e payloads longos.
5. Executar a matriz visual em 1440, 1024, 768 e 375 px e o fluxo principal por teclado.
6. Anexar capturas, resultado dos comandos e ocorrências encontradas a este registro.

Somente depois disso C8 pode passar a **Comprovado**. C1–C7 avançam individualmente quando suas pendências da tabela forem verificadas.

### Menor ação necessária para concluir a prova operacional de C8

Sem reconstruir a stack compartilhada nem misturar a frente de alertas:

1. Subir um PostgreSQL descartável isolado (porta/volume próprios — `scripts/tests/start-test-postgres.ps1` ou um `docker run` avulso).
2. Aplicar as migrations **até `20260711172401_AddTelemetrySchemaV2`** (a imediatamente anterior a `20260906225556_AddAlertBackend`) com `dotnet ef database update 20260711172401_AddTelemetrySchemaV2 --project src/Fluxo.Infrastructure --startup-project src/Fluxo.Api` — assim o schema de rejeições existe (veio em `20260711010719_AddTelemetryRejectionReprocessing`) e o de alertas não.
3. Rodar só a API (`dotnet run --project src/Fluxo.Api`) apontando a connection string para esse banco, com `Authentication__Enabled=true` e uma `SigningKey` de teste. Não subir o worker de ingestão.
4. `register` + `login` de dois usuários, criar um workspace para cada, semear algumas linhas em `telemetry_ingestion_rejections` (ou publicar telemetria malformada por MQTT, se o broker estiver disponível) e repetir contra o endpoint as verificações já cobertas em memória — desta vez com o tradutor de `Contains`/paginação do Npgsql e a serialização real.
5. Anexar as respostas HTTP e o resultado a este registro e promover C8 a **Comprovado**.

Alternativa equivalente: habilitar o PostgreSQL de Testcontainers no ambiente de CI e deixar os 41 testes de integração ignorados rodarem — os 9 casos novos de `TelemetryRejectionsEndpointsTests` passam a executar contra Postgres real sem nenhuma ação manual.

## Registro de validação — C8 (2026-09-08)

**Etapa do roadmap:** Etapa 8 (listas operacionais escaláveis) + Etapa 3 (feedback reutilizável). **Responsável pela validação:** sessão de continuação automatizada.

**Estado anterior:** Em validação — "página, serviço, endpoint e caso de uso implementados; testes do portal e build .NET aprovados". Sem cobertura automatizada de backend para a rota, sem inspeção visual/teclado, sem prova de isolamento.

**Estado final:** Em validação (mantido). Reforço substancial de evidência estática, automatizada e visual isolada; a única lacuna remanescente é a execução contra PostgreSQL real / stack compartilhada, com bloqueio concreto registrado.

**Arquivos alterados nesta sessão:**

- `tests/Fluxo.IntegrationTests/Api/TelemetryRejectionsEndpointsTests.cs` — novo; 9 casos (1 é `[Theory]` com 3 linhas) sobre o pipeline HTTP real com EF InMemory.
- `portal-web/src/pages/TelemetryRejectionsPage.test.tsx` — expandido de 2 para 8 casos: carregamento, erro + repetição, vazio, tabela acessível (legenda + `columnheader`), paginação, busca por teclado com volta à primeira página, payload/motivo longos.

Nenhum arquivo de produção (backend ou frontend) foi alterado — a implementação de C8 já existente foi apenas inspecionada e exercitada.

**Comandos executados:**

| Comando | Resultado |
| --- | --- |
| `git branch --show-current` | `fix/portal-same-origin-auth` (confirmado; nenhuma troca de branch) |
| `npm run test` (portal-web) | 44/44 aprovados |
| `npm run build` (portal-web) | Aprovado (`tsc --noEmit && vite build`) |
| `dotnet build Fluxo.slnx --no-restore` | Aprovado, 0 warning / 0 erro |
| `dotnet test Fluxo.slnx --no-build` | 97 unitários + 26 integração aprovados; 41 ignorados; 0 falha |

**Inspeção da trilha (estático):**

- **Endpoint:** `TelemetryRejectionsController` — `[ApiController, Authorize, Route("api/workspaces/{workspaceId:guid}/telemetry-rejections")]`; `page`/`pageSize`/`search` por query; delega a `ListWorkspaceTelemetryRejectionsUseCase` com `User.GetRequiredUserId()`.
- **Autorização/tenant:** o caso de uso chama `GetAuthorizedWorkspaceUseCase` antes de qualquer leitura. Não-membro → `NotFoundException` → **404** (sem vazar existência); token ausente → **401** (middleware). O repositório filtra por `TenantId` **e** `WorkspaceId` do workspace autorizado (`ListByTenantWorkspaceAsync`) — defesa em profundidade; nenhum id do corpo/rota é usado como prova de autorização.
- **Paginação/busca/ordenação:** `page < 1` ou `pageSize ∉ [1,100]` → `ArgumentOutOfRangeException` → **400**. Ordenação fixa `ReceivedAtUtc` desc. Busca: `Contains` parametrizado em `Topic`/`Reason`/`DeviceId`/`MessageType` (sem injeção; sensível a maiúsculas conforme o collation do Postgres — melhoria futura).
- **Estados no portal:** `LoadingState` (`role=status`), `ErrorState` (`role=alert`) com "Tentar novamente" que refaz `load()`, `EmptyState` com texto distinto para "sem resultado de busca" vs "workspace sem rejeições". Refresh usa `setLoading(true)` e troca o conteúdo pelo loading — pequeno desvio do design-system ("manter dados durante refresh"); registrado como polimento.
- **Timestamps/timezone:** `<Timestamp>` renderiza `formatHumanTimestamp` (pt-BR, `dd/mm/aaaa hh:mm:ss`) como texto visível e mantém o ISO em `title`/`dateTime`. Confirmado no navegador: `2026-09-08T14:00:00Z` → `08/09/2026 11:00:00`. **Lacuna transversal (não só C8):** o rótulo de fuso ("BRT"/"UTC−03:00") não aparece; o design-system exige fuso visível. Vale para todo uso de `Timestamp` no portal.
- **`DateTime` da API:** `ReceivedAtUtc` é `Kind=Utc` no domínio; System.Text.Json emite `...Z`; o front interpreta como UTC. OK.

**Exposição de dados sensíveis do payload (item 6):**

`ListWorkspaceTelemetryRejectionsUseCase.Preview` **apenas trunca** o `PayloadRaw` para 240 caracteres + "…" e colapsa espaços em branco; **não há redação**. Qualquer membro do workspace (inclusive Viewer) vê os primeiros 240 caracteres do payload bruto rejeitado. Telemetria normalmente não carrega segredos, mas um dispositivo mal configurado pode publicar tokens, credenciais ou dados pessoais, e uma mensagem rejeitada por `PayloadInvalid` é exatamente o caso em que o conteúdo é imprevisível. **Proposta (não implementada — decisão de produto/domínio):** (a) exigir papel mínimo Admin para ver a coluna de prévia, ou (b) redigir padrões conhecidos (`token`, `password`, `secret`, `authorization`, e-mails) antes de truncar, ou (c) tornar a prévia um clique explícito por linha com aviso. A cópia da página ("os payloads aparecem apenas como uma prévia limitada") sugere proteção que hoje não existe — ajustar cópia ou comportamento.

**Validação visual e de teclado (dev server isolado — `portal-web-dev` na porta 5173, sem stack, `window.fetch` interceptado para servir estados canônicos):**

| Item | Resultado |
| --- | --- |
| 1440 px | Tabela com 6 colunas, legenda e separadores; sem scroll horizontal de documento. |
| 1024 px | Sem overflow de documento; colunas comprimem; prévia de payload longa infla muito a altura da linha (ver polimento). |
| 768 px | Documento sem scroll horizontal; a tabela rola dentro de `.table-scroll` (924 > 705 px) conforme o design-system. |
| 375 px | Documento sem scroll horizontal; filtro de busca empilha (`flex-direction: column`); tabela rola no container. |
| Teclado | `Tab` alcança busca → "Buscar" → "Limpar"/paginação (quando habilitados); `:focus-visible` = 2 px sólido `#2563EB`, offset 2 px, em input e botões. Skip link visível ao foco. |
| `aria-current` | Item "Rejeições" com `aria-current="page"` + `is-active` na rota correta. |
| Estados | vazio, carregando (`role=status`), erro (`role=alert` + retry), populado e busca→filtra→"Limpar"→reseta — todos exercitados no React real. |
| Cor isolada | Reprocessamento é texto ("Resolvida"/"Tentativa N"/"Não tentada"); `errorType` é texto; nenhum sinal só por cor. |

**Problemas encontrados:**

1. **Fuso ausente nos timestamps** (transversal ao portal, não bloqueia C8 isoladamente).
2. **Prévia de payload sem redação** (ver item 6 acima) — maior risco funcional de C8.
3. **Altura de linha** quando a prévia de payload aproxima-se de 240 caracteres: a célula vira um bloco alto de ~13 linhas em larguras estreitas. Sugestão: `max-height` + scroll em `.payload-preview`, ou prévia curta com expandir.
4. **`errorType` exibido como enum PascalCase em inglês** ("Validation", "PayloadInvalid"). O design-system pede vocabulário pt-BR com o valor técnico como detalhe.
5. **`ErrorState` genérico**: 401 de sessão expirada mostra a mesma mensagem que 500. O design-system pede orientação específica para erro de sessão/autorização.
6. **Rejeições sem tenant/workspace resolvidos** (`TenantId`/`WorkspaceId` nulos — falha antes de rotear o tópico) nunca aparecem em nenhuma visão de workspace. Correto para isolamento, mas é um limite a documentar para o operador.

**Pendências para "Comprovado":**

- Executar o endpoint contra PostgreSQL real (ver "Menor ação necessária" acima) — cobre tradução real de `Contains`/paginação, `401`/`404`/`400` na pilha completa e isolamento com dois tenants reais.
- Decisão de produto sobre a proteção da prévia de payload (item 6).

**Bloqueios concretos:**

- Não é possível reconstruir/subir a stack compartilhada sem incorporar a frente de alertas (caminho de escrita da ingestão, `AlertEvaluationWorker`, `AlertModelConfiguration`, migration `20260906225556_AddAlertBackend`) ao ambiente compartilhado. Registrado em "Restrições e riscos". C8 permanece **Em validação**.

## Modelo de registro para novas entregas

Cada nova entrega deve acrescentar:

- identificador e objetivo observável;
- etapa correspondente em `ui-roadmap.md`;
- estado atual e responsável pela validação;
- critérios de aceite;
- arquivos, testes, comandos e capturas que servem como evidência;
- riscos, bloqueios e decisão tomada;
- data da última mudança de estado.

## Histórico

| Data | Mudança |
| --- | --- |
| 2026-09-08 | Registro central criado; marco atual, C1–C8, evidências automatizadas, riscos e próximo portão consolidados. |
| 2026-09-08 | C8: adicionados 9 testes de integração do endpoint de rejeições e ampliados os testes da página (2→8); inspeção estática completa da trilha, validação visual/teclado nos 4 breakpoints em dev server isolado, risco de exposição de payload documentado. Estado mantido em **Em validação**: falta execução contra PostgreSQL real, bloqueada pela frente de alertas na árvore. Menor ação de desbloqueio registrada. |
