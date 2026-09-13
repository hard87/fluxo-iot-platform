# Fluxo — Estado Atual do Projeto

## 1. Snapshot

- Consolidação documental: 12 set 2026.
- Branch observada: `main`.
- HEAD observado: `baf346470a436f39109a42de7fe61578c5c1f11f`.
- Working tree observada: limpa, exceto documentos novos não rastreados do plano de ação
  (`docs/plano-acao-e1-e2-e3-e5.md`, `docs/simulacao-piloto-industria-alimentos-100-devices.md`),
  uma alteração em `docs/README.md` que os referencia, e um rascunho pessoal não rastreado
  (`inicio.txt`).
- Esta consolidação não declara suporte de produção a 1000 devices.
- O portal de alertas (E1, ver seção 2.3) foi concluído nesta sessão em quatro PRs empilhados
  ainda não mergeados em `main`: `feat/portal-alert-contracts` (#7) →
  `feat/portal-alert-rules` (#8) → `feat/portal-alert-rule-editor` (#9) →
  `feat/portal-alert-events` (#10) → `feat/portal-alert-diagnostics` (#11). Mergear na ordem;
  cada PR reaponta sozinho para `main` no GitHub assim que o anterior é integrado.

## 2. Estado das trilhas

| Trilha | Fase | Status | Evidência |
|---|---|---|---|
| Produto | Fase 0 — decisões e baseline | CONCLUÍDA | ADRs aceitos e benchmark executado |
| Produto | Fase 1 — Schema V2 e ingestão | CONCLUÍDA | [Relatório Fase 1](handoff/relatorio-fase-1-schema-v2-2026-07-11.md) |
| Produto | Fase 2 — Telemetry Query API e Explorer | CONCLUÍDA | [Relatório Fase 2](handoff/relatorio-fase-2-telemetry-explorer-2026-07-12.md) |
| Produto | Fase 3 — alertas | EM ANDAMENTO — backend e portal (E1) concluídos; worker de avaliação desabilitado em todos os ambientes (ver 2.3/Riscos); E2 (validação ponta a ponta) e canal de entrega pendentes | [ADR-0002](adr/0002-alert-evaluation-state-and-delivery.md), [ADR-0005](adr/0005-alertas-canais-historico-isolamento-proposta.md) |
| Produto | Fase 4 — inteligência operacional | NÃO INICIADA | [Escopo do MVP](product/mvp-scope.md) |
| Produto | Fase 5 — pilotos físicos | EM PILOTO | [Relatório Gateway Pi](handoff/relatorio-fase-5-piloto-fisico-gateway-pi-2026-07-31.md) |
| Infraestrutura | Infra Fase 1 — hardening | CONCLUÍDA | Baseline de autenticação, ACL, TLS MQTT e ingestão |
| Infraestrutura | Infra Fase 2 — piloto controlado | EM PILOTO | Gateway Pi validado em MQTT/TLS; 24h, backup/restore e fechamento operacional pendentes |
| Infraestrutura | Infra Fase 3 — preparação de produção | NÃO INICIADA | [Roadmap](roadmap-production-1000-devices.md) |
| Infraestrutura | Infra Fase 4 — produção escalável | NÃO INICIADA | [Roadmap](roadmap-production-1000-devices.md) |

### 2.1 Atividade recente (12/09/2026)

- Backend de alertas ativado de ponta a ponta: `AlertEvaluationEngine` e `AlertTransactions`
  ligados à escrita de telemetria (lock de estado + enfileiramento na mesma transação),
  `AlertEvaluationWorker` no host de ingestão, `AlertsController` expondo regras, eventos,
  histórico, reconhecimento e diagnóstico. Migration `AddAlertBackend` aplicada. **Sem interface
  no portal ainda** — hoje a criação/consulta de regras só existe via API.
- Portal: view de investigação de mensagens rejeitadas (`/workspaces/{id}/rejections`), registro
  de dispositivos com busca/filtro, visão operacional do device (métricas nativas, sparkline,
  histórico) e alternativas acessíveis (texto/tabela) para os gráficos de telemetria.
- Correção: sessão de autenticação do portal agora sobrevive a reload (antes só existia em
  memória React; confirmado manualmente no navegador após o fix).
- Correção: cadastro de dispositivo pelo portal estava **completamente quebrado** — o frontend
  envia `category` como string e o DTO da API só aceitava o enum numérico padrão do
  System.Text.Json. Os testes de integração nunca pegaram isso porque serializam o enum a partir
  do objeto C# tipado (que vira número), nunca do JSON real que o navegador envia. Corrigido com
  `JsonStringEnumConverter<DeviceCategory>` escopado (não um converter global, para não alterar o
  formato de outros enums como `WorkspaceMembershipRole`).
- Cobertura unitária cresceu para 97 testes (0 falhas) com a adição de `AlertEvaluatorTests` e
  cobertura de rejeições. A suíte de integração local (sem o Postgres descartável de
  `scripts/tests/start-test-postgres.ps1` rodando) mantém 26 aprovados e 41 ignorados por
  ausência do servidor — revalidação completa com Postgres descartável real executada em
  12/09/2026 (ver 2.2).

### 2.2 Baseline M0.1 revalidada (12/09/2026)

Execução da baseline exigida pelo [plano de ação E1/E2/E3/E5](plano-acao-e1-e2-e3-e5.md) antes de
iniciar o E1, sobre HEAD `baf346470a436f39109a42de7fe61578c5c1f11f` em `main`:

- `dotnet build Fluxo.slnx`: sucesso, 0 avisos, 0 erros.
- `dotnet test Fluxo.slnx` com Postgres descartável real (perfil `transactional`) e
  `FLUXO_TESTS_REQUIRE_POSTGRES=1`: **97/97 testes unitários** e **71/71 testes de integração**
  aprovados, **0 ignorados**, 0 falhas. Nenhum teste relacional foi contado como aprovado por
  ausência de banco.
- `npm test` (portal-web): **44/44 testes** aprovados em 11 arquivos.
- `npm run build` (portal-web): `tsc --noEmit` sem erros e `vite build` concluído; permanece o
  aviso conhecido de bundle acima de 500 kB (ver Riscos, seção 7).
- `npm test` (e2e/Playwright, cenário `golden-path.spec.ts`) contra a stack de desenvolvimento já
  em execução (`docker compose up`, portal em `127.0.0.1:8080`): **1/1 teste** aprovado.
- `docker compose config`: perfil dev válido; perfil `docker-compose.controlled-prod.yml` válido
  quando avaliado com os placeholders de `.env.example` (o arquivo real exige variáveis
  obrigatórias sem default, por design).

Gate de saída da M0.1 cumprido: nenhuma falha encontrada, nenhuma correção separada necessária
antes do E1.

### 2.3 Portal de alertas concluído — E1 (12–13/09/2026)

Executado o [plano de ação E1-E5](plano-acao-e1-e2-e3-e5.md), etapas E1.1 a E1.5, em quatro PRs
empilhados (ver seção 1). Todas as operações do backend de alertas agora têm interface no portal:

- `/workspaces/{id}/alerts` — lista paginada de regras (nome, métrica, escopo, condição,
  severidade, estado) com ativar/desativar inline via `PUT` (única operação que o contrato
  suporta sem ambiguidade — reenvia a revisão atual com `expectedVersion`).
- `/workspaces/{id}/alerts/new` e `/workspaces/{id}/alerts/{ruleId}/edit` — formulário guiado
  pelo `ValueType` da métrica (operadores numéricos vs. booleanos), com validação client-side de
  apoio (servidor continua autoritativo) e confirmação explícita ao editar uma regra ativa.
- `/workspaces/{id}/alerts/events` — eventos ativos/resolvidos com filtro de estado/severidade
  aplicado apenas à página carregada (sem paginação client-side inventada).
- `/workspaces/{id}/alerts/events/{eventId}` — histórico de transições (valor, timestamps com
  fuso explícito, atraso de ingestão nunca mascarado como zero saudável) e reconhecimento
  idempotente com autoria.
- `/workspaces/{id}/alerts/diagnostics` — falhas de avaliação (`Failed`/`DeadLetter`, os únicos
  estados que este endpoint expõe; não existe ainda estado de entrega por canal de notificação).

Cobertura do portal cresceu de 44 para 132 testes (Vitest + Testing Library). Verificação
ponta a ponta feita ao vivo contra a API e o Postgres reais (não só mockada): dispositivo
provisionado, telemetria Schema V2 publicada via `scripts/mqtt-device-simulator.py`, regra criada
e ativada, evento disparado, listado, histórico consultado e reconhecido — idempotência do
reconhecimento confirmada (mesmo `id` retornado ao chamar duas vezes). `docker compose build/up`
e o E2E `golden-path.spec.ts` revalidados sem regressão em autenticação/ingestão. Auditoria de
1600/1024/768/375 px: nenhum componente novo do alertas overflowa horizontalmente; o overflow em
375 px encontrado nas telas de regras/eventos vem do `.app-navigation-list` pré-existente (mesmo
bug reproduzido em `/devices`, portanto não é regressão desta entrega — ver Riscos).

**Achado crítico durante a verificação:** o `AlertEvaluationWorker` nunca avalia nenhuma regra em
nenhum ambiente hoje. `AlertEvaluationOptions.Enabled` é um `bool` sem default explícito (logo
`false`) e a chave `AlertEvaluation:Enabled` não é definida em `docker-compose.yml`,
`docker-compose.controlled-prod.yml` nem em `appsettings.json` — o worker executa
`if (!options.Value.Enabled) return;` e encerra sem processar a fila. Os testes de integração
(`AlertBackendTests`) não pegam isso porque instanciam `AlertEvaluationEngine` diretamente,
pulando esse gate. Confirmado ao vivo via `docker exec fluxo-postgres psql`: um work item ficou
`Pending`/`AttemptCount=0` indefinidamente até a flag ser ligada manualmente (não commitado). Ou
seja, **o portal de alertas está completo e funcional, mas nenhuma regra dispara de fato em
nenhum ambiente configurado até essa flag ser corrigida** — ver Riscos ativos (seção 7).

## 3. Arquitetura atual

- Portal: React, Vite, TypeScript e Recharts 2.x.
- Backend: .NET, com API HTTP e Worker de ingestão MQTT.
- Persistência: PostgreSQL; broker: Mosquitto.
- Telemetry Schema V2 com envelope genérico e valores Numeric, Boolean e Text.
- `MetricDefinition` como catálogo tipado por workspace.
- `TelemetryPoint` particionada mensalmente por `OccurredAtUtc`.
- `TelemetryIngestionRecord` preservado como auditoria e não particionado nesta fase.
- Npgsql Binary COPY como writer aprovado de `TelemetryPoint`; EF permanece como apoio/fallback.
- Telemetry Query API e catálogo de métricas protegidos por workspace.
- Telemetry Explorer em `/workspaces/{workspaceId}/explorer`.

## 4. Capacidades comprovadas

- Ingestão controlada com 100 devices, 30.000 mensagens e zero perda no cenário registrado.
- Schema V2, compatibilidade com payload legado, tipo estável e guardrails de cardinalidade.
- Migration do Schema V2 validada up/down/up.
- Particionamento de `TelemetryPoint` e manutenção de partições testados.
- Binary COPY aprovado após benchmark; dense/COPY consumiu 54,01% do budget de 2 cores.
- Telemetry Query API com `raw`, `avg`, `min`, `max`, `sum`, `count` e `last`.
- Numeric, Boolean e Text consultados no mesmo Explorer, sem fork por vertical.
- Primeiro `DeviceCategory.Gateway` físico provisionado no Raspberry Pi `edgewarden`.
- MQTT/TLS QoS 1, sequence e spool persistentes validados após restart, reboot e reconexão.
- ACL negativa comprovada e telemetria diagnóstica Schema V2 aceita pelo backend.
- Q1–Q7 executados em 5.115.083 pontos e cinco partições.
- Q6 executou em 1624,705 ms, abaixo do gate de 2 s; nenhum índice novo foi necessário.
- Revalidação de 06/09/2026: 71 testes unitários, 48 de integração no PostgreSQL descartável transacional e 30 do portal aprovados. O antigo resultado de 40 integrações incluía 26 retornos sem execução quando faltava o banco; detalhes no [baseline de alertas](handoff/alertas-etapa-1-baseline.md).
- Validação responsiva em 20 combinações de página/largura sem overflow horizontal.

Esses resultados são evidência de carga e leitura controladas. Não provam que o Fluxo está
pronto para 1000 devices em produção.

## 5. Próxima fase de produto

Produto Fase 3 — Alertas com estado e delivery. Backend e portal (E1) estão implementados e
comprovados na interface (seção 2.3), mas o worker de avaliação está desabilitado em todos os
ambientes (ver Riscos) — corrigir isso é pré-requisito antes de qualquer demonstração ou piloto
que dependa de alertas realmente dispararem. Falta a validação e2e automatizada do fluxo completo
(E2 do plano de ação: regra criada → telemetria dispara → evento aparece → canal notifica) e a
definição do canal de entrega (E2.4, ainda em aberto). A arquitetura normativa está no
[ADR-0002](adr/0002-alert-evaluation-state-and-delivery.md), complementada pelo
[ADR-0005](adr/0005-alertas-canais-historico-isolamento-proposta.md); não deve ser substituída
por um desenho novo durante a implementação.

## 6. Pendências de produção controlada

- confirmar TLS HTTP e MQTT, exposição de portas, certificados e HSTS no ambiente-alvo;
- revisar JWT e ausência de secrets reais versionados;
- testar rotação de credencial MQTT;
- executar backup e restore descartável;
- definir retenção para telemetria e rejeições;
- confirmar logs e health checks de todos os componentes exigidos;
- validar procedimento de encerramento;
- executar firmware ESP32 por 24h;
- executar o Gateway Pi por 24h e revisar o log de monitoramento;
- revogar/desativar o primeiro provisionamento Gateway sem uso registrado no relatório da Fase 5;
- integrar sensor físico ao Gateway quando houver hardware identificado;
- revisar guia do piloto, backup/restore e simulador.
- habilitar `AlertEvaluation:Enabled` em todos os ambientes — hoje o worker de avaliação de
  alertas nunca dispara (ver seção 2.3 e Riscos ativos).

A fonte autoritativa dos checkboxes é o [checklist de produção controlada](checklist-producao-controlada.md).

## 7. Riscos ativos

- **Crítico:** `AlertEvaluationWorker` nunca avalia nenhuma regra em nenhum ambiente hoje —
  `AlertEvaluation:Enabled` não é definido em `docker-compose.yml`,
  `docker-compose.controlled-prod.yml` nem `appsettings.json`, e o default de `bool` em C# é
  `false`. O portal de alertas (seção 2.3) está completo e funcional, mas nenhum alerta dispara de
  fato até essa flag ser ligada. Detalhes e correção sugerida registrados como tarefa separada
  nesta sessão (não corrigido aqui para não misturar fix de infraestrutura com entrega de portal).
- `.app-navigation-list` não tem layout responsivo e força rolagem horizontal da página inteira em
  larguras de ~375 px, em qualquer rota (reproduzido em `/devices` e nas novas rotas de alertas) —
  pré-existente, não é regressão do E1; registrado como tarefa separada.
- HA completo do broker, serviços e PostgreSQL ainda não comprovado.
- Tracing fim a fim ainda ausente.
- Retenção, arquivamento, backup e restore carecem de política/exercício operacional.
- Perfil dense projeta volume elevado em retenção longa.
- Produção controlada, TLS operacional e firmware 24h permanecem pendentes.
- O Gateway Pi foi validado com TLS no compose local, mas o ensaio de 24h e a repetição no perfil
  controlled-prod permanecem pendentes.
- O spool por arquivos aumenta escrita no cartão SD e ainda não tem evidência de duração longa.
- Há um device/credencial Gateway inicial sem uso a revogar após falha de configuração local,
  conforme relatório da Fase 5.
- Bundle principal do portal acima do warning de 500 kB; Explorer sem lazy loading.
- `npm audit --omit=dev` reporta duas vulnerabilidades moderadas no React Router; correção
  disponível exige migração breaking para 7.x e deve ser tratada em trabalho próprio.
- Recharts 2.x requer decisão futura explícita antes de eventual migração.
- Google Font externa bloqueada pela CSP, com fallback local.
- ~~Sessão em memória perdida no refresh.~~ Corrigido em 12/09/2026 — ver atividade recente (2.1).

## 8. Documentos normativos

- [ADR-0001 — Telemetry Schema V2](adr/0001-telemetry-schema-v2.md)
- [ADR-0002 — Alertas com estado e delivery](adr/0002-alert-evaluation-state-and-delivery.md)
- [ADR-0003 — Telemetry Query API](adr/0003-telemetry-query-api.md)
- [ADR-0004 — Gateway Pi store-and-forward](adr/0004-pi-gateway-store-and-forward.md)
- [ADR-0005 — Complemento de alertas (canais, histórico, isolamento)](adr/0005-alertas-canais-historico-isolamento-proposta.md)
- [Escopo do MVP](product/mvp-scope.md)

## 9. Evidências

- [Relatório da Fase 1](handoff/relatorio-fase-1-schema-v2-2026-07-11.md)
- [Benchmark original da Fase 1](benchmarks/phase1-2026-07-11.md)
- [Benchmark normalizado da Fase 1](benchmarks/phase1-normalized-2026-07-11.md)
- [Relatório da Fase 2](handoff/relatorio-fase-2-telemetry-explorer-2026-07-12.md)
- [Benchmark Q1–Q7 da Fase 2](benchmarks/phase2-query-explain-2026-07-12.md)
- [Relatório da Fase 5 — Gateway Pi](handoff/relatorio-fase-5-piloto-fisico-gateway-pi-2026-07-31.md)

## 10. Regra para próximos agentes

Antes de implementar uma fase:

1. ler este `project-status.md`;
2. ler o ADR da fase;
3. ler o relatório da fase anterior e seus benchmarks;
4. confirmar branch, HEAD e working tree;
5. preservar mudanças preexistentes;
6. não reabrir fases concluídas sem evidência objetiva de regressão.
