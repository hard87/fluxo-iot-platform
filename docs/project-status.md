# Fluxo — Estado Atual do Projeto

## 1. Snapshot

- Consolidação documental: 12 set 2026.
- Branch observada: `fix/portal-same-origin-auth`.
- HEAD observado: `0499dcfd7223bd9914809787bba06ef7b7287f9f`.
- Working tree observada: limpa, exceto um rascunho pessoal não rastreado (`inicio.txt`).
- Esta consolidação não declara suporte de produção a 1000 devices.

## 2. Estado das trilhas

| Trilha | Fase | Status | Evidência |
|---|---|---|---|
| Produto | Fase 0 — decisões e baseline | CONCLUÍDA | ADRs aceitos e benchmark executado |
| Produto | Fase 1 — Schema V2 e ingestão | CONCLUÍDA | [Relatório Fase 1](handoff/relatorio-fase-1-schema-v2-2026-07-11.md) |
| Produto | Fase 2 — Telemetry Query API e Explorer | CONCLUÍDA | [Relatório Fase 2](handoff/relatorio-fase-2-telemetry-explorer-2026-07-12.md) |
| Produto | Fase 3 — alertas | EM ANDAMENTO — backend concluído, portal pendente | [ADR-0002](adr/0002-alert-evaluation-state-and-delivery.md), [ADR-0005](adr/0005-alertas-canais-historico-isolamento-proposta.md) |
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
  ausência do servidor — **revalidação completa com Postgres descartável real ainda não foi
  executada após este lote de mudanças**.

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

Produto Fase 3 — Alertas com estado e delivery. O backend (engine, worker, endpoints) está
implementado; falta a interface do portal para criar/editar regras e acompanhar eventos, e a
validação e2e do fluxo completo (regra criada → telemetria dispara → evento aparece → canal
notifica). A arquitetura normativa está no
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
- construir a interface do portal para alertas (regras, eventos, histórico, reconhecimento);
- revalidar a suíte de integração com o Postgres descartável real
  (`scripts/tests/start-test-postgres.ps1`) após o lote de alertas/rejeições de 12/09/2026.

A fonte autoritativa dos checkboxes é o [checklist de produção controlada](checklist-producao-controlada.md).

## 7. Riscos ativos

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
- Sessão em memória perdida no refresh.

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
