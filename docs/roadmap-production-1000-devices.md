# Fluxo — Roadmap técnico para produção e produto

Consolidação: 12 jul 2026. Este documento mantém duas trilhas distintas. A meta de 1000 devices
é uma referência de escala, não uma capacidade de produção comprovada.

## Meta de escala

- 1000 devices globais;
- envio médio de uma mensagem a cada 10 segundos;
- carga média projetada de aproximadamente 100 mensagens/s;
- aproximadamente 8.640.000 mensagens MQTT/dia.

No Schema V2, uma mensagem gera um `TelemetryIngestionRecord` e N `TelemetryPoint`. Assim, os
volumes abaixo são projeções, não evidência de capacidade:

| Perfil | Métricas/mensagem | `TelemetryIngestionRecord`/dia | `TelemetryPoint`/dia |
|---|---:|---:|---:|
| light | 5 | 8.640.000 | 43.200.000 |
| medium | 16 | 8.640.000 | 138.240.000 |
| dense | 32 | 8.640.000 | 276.480.000 |

A Fase 1 comprovou ingestão controlada com 100 devices e gates específicos. A Fase 2 comprovou
consulta em 5.115.083 pontos. Nenhuma dessas evidências, isolada ou combinada, comprova produção
pronta para 1000 devices.

## Trilha de Infraestrutura e Escala

### Infra Fase 1 — Hardening básico

Status: **CONCLUÍDA**.

- autenticação MQTT por device e ACL;
- TLS MQTT e rate limiting;
- idempotência e fluxo de rejeições;
- logs, métricas e health checks existentes no baseline.

### Infra Fase 2 — Piloto real controlado

Status: **PRÓXIMA** na trilha de infraestrutura.

- executar firmware ESP32 por 24 horas;
- validar TLS HTTP e MQTT no ambiente do piloto;
- executar e testar backup/restore;
- definir retenção;
- fechar os itens ainda abertos no
  [checklist de produção controlada](checklist-producao-controlada.md).

### Infra Fase 3 — Preparação de produção

Status: **NÃO INICIADA**.

- política de retenção, arquivamento e eventual downsampling;
- planejamento de HA para broker, API/Worker e PostgreSQL;
- tracing fim a fim e procedimentos operacionais;
- avaliar, por medição, se `TelemetryIngestionRecord` também precisa de particionamento.

`TelemetryPoint` já está particionada mensalmente; isso não é pendência desta fase.

### Infra Fase 4 — Produção inicial escalável

Status: **NÃO INICIADA**.

- escalabilidade horizontal com estado externo;
- HA do broker e do banco;
- testes sustentados progressivos até a meta de escala;
- custo e retenção validados para os perfis reais.

## Trilha de Produto — MVP Comercial

### Produto Fase 0 — Decisões e baseline

Status: **CONCLUÍDA**.

Evidência: ADR-0001, ADR-0002 e escopo do MVP aprovados; baseline e benchmark executados. O
resultado normalizado está no [benchmark da Fase 1](benchmarks/phase1-normalized-2026-07-11.md).

### Produto Fase 1 — Schema V2 e ingestão compatível

Status: **CONCLUÍDA**.

Evidência: Schema V2, `MetricDefinition`, `TelemetryPoint` particionada, guardrails, Binary COPY
como writer aprovado, migration up/down/up, testes e benchmark normalizado. Ver o
[relatório da Fase 1](handoff/relatorio-fase-1-schema-v2-2026-07-11.md).

### Produto Fase 2 — Telemetry Query API e Telemetry Explorer

Status: **CONCLUÍDA**.

Evidência: catálogo `metric-definitions`, Telemetry Query API, Telemetry Explorer, Numeric,
Boolean e Text, testes, responsividade e Q1–Q7. Q6 executou em 1624,705 ms, abaixo do gate de 2 s;
nenhum índice novo foi necessário. Ver o [relatório da Fase 2](handoff/relatorio-fase-2-telemetry-explorer-2026-07-12.md)
e o [benchmark Q1–Q7](benchmarks/phase2-query-explain-2026-07-12.md).

### Produto Fase 3 — Alertas com estado e delivery

Status: **PRÓXIMA**.

Ainda não iniciada. Implementar somente conforme o
[ADR-0002](adr/0002-alert-evaluation-state-and-delivery.md): estado persistido, fila transacional,
avaliação reprodutível e delivery at-least-once. `NoData` permanece na Fase 4.

### Produto Fase 4 — Inteligência operacional barata

Status: **NÃO INICIADA**.

Device health, stale/no-data, gaps de sequence, rejection rate, sensor travado, faixa esperada,
rate-of-change e estatística robusta determinística.

### Produto Fase 5 — Pilotos físicos

Status: **NÃO INICIADA**.

Validar os perfis industrial, ambiente e mobilidade no mesmo backend, sem migration por vertical.

## Riscos técnicos em aberto

| Risco | Estado atual | Responsável |
|---|---|---|
| HA completo do broker e dos serviços | Não comprovado | Infra Fases 3–4 |
| Tracing fim a fim | Ainda ausente | Infra Fase 3 |
| Retenção, arquivamento e restore | Políticas e exercícios pendentes | Infra Fases 2–3 |
| Volume dense em longo prazo | Projeção elevada; exige política operacional e custo real | Infra Fases 3–4 |
| TLS e operação do piloto | Compose validado não prova TLS operacional | Infra Fase 2 |
| Firmware físico sustentado | Teste ESP32 de 24h pendente | Infra Fase 2 |
| Bundle principal do portal | 575,37 kB minificado; warning do Vite | Produto, revisão futura |
| Explorer no bundle principal | Sem lazy loading; não bloqueia o MVP | Produto, revisão futura |
| Recharts 2.x | Entregue conforme ADR; evolução para v3 requer decisão explícita | Produto, revisão futura |
| Fonte externa do portal | Google Font bloqueada pela CSP; fallback local ativo | Produto, revisão futura |
| Sessão do portal | Token em memória perdido no refresh | Produto, revisão futura |

Particionamento de `TelemetryPoint`, benchmark da Fase 1, Schema V2, Binary COPY, Telemetry Query
API e Telemetry Explorer não são riscos abertos: foram implementados e possuem evidência.

## Critério para declarar produção pronta para 1000 devices

Essa declaração exige, no mínimo:

1. autenticação, ACL e TLS operacionais no ambiente-alvo;
2. retenção, backup e restore exercitados;
3. HA e observabilidade adequadas ao SLO definido;
4. firmware e infraestrutura validados de forma sustentada;
5. testes progressivos no perfil real comprovando estabilidade e custo até a meta de escala.

Até esses gates serem cumpridos, 1000 devices permanece uma meta de escala.
