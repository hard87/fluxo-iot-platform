# Fluxo — Estado Atual do Projeto

## 1. Snapshot

- Consolidação documental: 31 jul 2026.
- Branch observada: `snapshot-auth-portal-mvp-20260526`.
- HEAD observado: `aeeaf78818faf52224a69b5eee396f7489e1543e`.
- Working tree observada: suja, com entregáveis do Gateway e itens preexistentes não rastreados.
- Esta consolidação não declara suporte de produção a 1000 devices.

## 2. Estado das trilhas

| Trilha | Fase | Status | Evidência |
|---|---|---|---|
| Produto | Fase 0 — decisões e baseline | CONCLUÍDA | ADRs aceitos e benchmark executado |
| Produto | Fase 1 — Schema V2 e ingestão | CONCLUÍDA | [Relatório Fase 1](handoff/relatorio-fase-1-schema-v2-2026-07-11.md) |
| Produto | Fase 2 — Telemetry Query API e Explorer | CONCLUÍDA | [Relatório Fase 2](handoff/relatorio-fase-2-telemetry-explorer-2026-07-12.md) |
| Produto | Fase 3 — alertas | PRÓXIMA | [ADR-0002](adr/0002-alert-evaluation-state-and-delivery.md) |
| Produto | Fase 4 — inteligência operacional | NÃO INICIADA | [Escopo do MVP](product/mvp-scope.md) |
| Produto | Fase 5 — pilotos físicos | EM PILOTO | [Relatório Gateway Pi](handoff/relatorio-fase-5-piloto-fisico-gateway-pi-2026-07-31.md) |
| Infraestrutura | Infra Fase 1 — hardening | CONCLUÍDA | Baseline de autenticação, ACL, TLS MQTT e ingestão |
| Infraestrutura | Infra Fase 2 — piloto controlado | EM PILOTO | Gateway Pi validado em MQTT/TLS; 24h, backup/restore e fechamento operacional pendentes |
| Infraestrutura | Infra Fase 3 — preparação de produção | NÃO INICIADA | [Roadmap](roadmap-production-1000-devices.md) |
| Infraestrutura | Infra Fase 4 — produção escalável | NÃO INICIADA | [Roadmap](roadmap-production-1000-devices.md) |

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

Produto Fase 3 — Alertas com estado e delivery. A fase ainda não foi iniciada. A arquitetura
normativa está no [ADR-0002](adr/0002-alert-evaluation-state-and-delivery.md); não deve ser
substituída por um desenho novo durante a implementação.

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
