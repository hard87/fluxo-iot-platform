# Handoff — Fase 1: Schema V2 + ingestão compatível (Codex GPT)

> Status histórico: execução concluída.  
> Resultado real: ver [Relatório da Fase 1](relatorio-fase-1-schema-v2-2026-07-11.md).

Cole este documento inteiro como prompt. Não resuma, não pule seções — o formato completo existe
porque a primeira versão deste handoff (`Dictionary<string,double>` genérico, sem guardrails) foi
rejeitada em revisão técnica por deixar decisões críticas para o agente decidir sozinho.

Pré-requisito: ADR-0001 e ADR-0002 aprovados (`Fluxo/docs/adr/`). Fase 0 (baseline de carga com
números reais) deve estar registrada antes de aplicar qualquer migration — se
`docs/adr/0001-telemetry-schema-v2.md` ainda diz "pendente" na seção de benchmark, pare e peça
para rodar o baseline primeiro.

## A. Contexto verificado (confirme antes de editar, não assuma)

- Repo: `D:\Officina404\Fluxo` (.NET Clean Architecture, Postgres, MQTT via Mosquitto
  dynamic-security). Branch: confirme com `git status`/`git branch` antes de começar.
- Rode `grep -rn "IncomingTelemetryMetrics\|TelemetryIngestionRecord" src/` e confirme se os
  caminhos abaixo ainda são os reais — se mudaram, use os caminhos reais e relate a divergência
  no relatório de entrega, não edite silenciosamente um arquivo diferente do esperado:
  - `Fluxo.Domain/Entities/TelemetryIngestionRecord.cs` — 5 colunas tipadas fixas hoje
    (`Temperature`, `Humidity`, `Battery`, `Rssi`, `UptimeSec`).
  - `Fluxo.Worker.Ingestion/Models/IncomingTelemetryMessage.cs` — `IncomingTelemetryMetrics` é
    POCO fixa com os mesmos 5 campos.
  - `Fluxo.Infrastructure/Configurations/TelemetryIngestionRecordConfiguration.cs`.
- Comportamento atual que **não pode regredir**: idempotência por `(DeviceId, Sequence)` via
  `ON CONFLICT DO NOTHING`; simulador (`scripts/mqtt-device-simulator.py` +
  `scripts/provision-simulated-devices.py`) precisa continuar funcionando sem mudança de
  credenciais/dynsec.

## B. Decisão arquitetural (ler ADR-0001 na íntegra: `docs/adr/0001-telemetry-schema-v2.md`)

Resumo do que implementar — **a especificação completa está no ADR, este é só o índice**:

1. Envelope V2 (`schemaVersion: 2`, `sequence`, `occurredAtUtc`, `metrics: {}` com valores
   `Number|Boolean|Text`), com adaptador de compatibilidade para o payload legado por um ciclo.
2. Entidade `MetricDefinition` (catálogo por workspace, `Status: Discovered|Active|Ignored`,
   `ValueType` estável após criada, coluna `TenantId` denormalizada não-chave — ver invariante
   tenant/workspace abaixo).
3. Entidade `TelemetryPoint` (tabela alta, **particionada** por `RANGE (OccurredAtUtc)`,
   `PRIMARY KEY (OccurredAtUtc, Id)`, `CHECK` garantindo **apenas** exactly-one-value-slot —
   compatibilidade com `MetricDefinition.ValueType` é responsabilidade do processor, não do
   `CHECK`; ver ADR-0001 seção do `CHECK` para o motivo). `TelemetryIngestionRecord` **não** é
   particionada nesta fase (decisão fechada em ADR-0001/ARCH-005 — não reinterprete
   "particionamento" como as duas tabelas).
4. Todos os guardrails da tabela do ADR (payload ≤32KB, 1-64 métricas/msg, regex de `MetricKey`,
   limites de cardinalidade — 500 `MetricDefinition`/workspace, 20 novas chaves/device/hora). O
   algoritmo de cardinalidade **está fechado** em ADR-0001 seção "Algoritmo distribuído do
   guardrail (ARCH-004)": nova entidade `MetricDefinitionDiscoveryAudit`, advisory transaction
   lock por `(WorkspaceId, DeviceId)` via `pg_advisory_xact_lock(hashtext(...))`, contagem na
   janela antes do insert, `INSERT ... ON CONFLICT (WorkspaceId, MetricKey) DO NOTHING` como
   proteção final entre devices diferentes. **Não reinvente esse algoritmo** — implemente o que
   está especificado lá.
5. `TelemetryPartitionMaintenanceService` (startup + diário 03:00 UTC, advisory lock, health
   check `Degraded`/`Unhealthy`).
6. **Cache de `MetricKey → MetricDefinitionId` por workspace** no processor de ingestão — sem
   isso o hot path faz lookup/insert condicional por métrica em toda mensagem. Não estava no
   documento de origem; é requisito deste handoff.

### Invariante tenant/workspace (fechada em ADR-0001, revisão 11 jul 2026)

Não existe entidade `Tenant` no domínio — `TenantId` é uma string denormalizada em `Workspace`,
`Device` e `TelemetryIngestionRecord`; `Workspace.Id` (`Guid`) é globalmente único e é a fronteira
real de ownership (via `WorkspaceMembership`, não via `TenantId`). `MetricDefinition` segue a
mesma convenção: chave única `(WorkspaceId, MetricKey)` (sem `TenantId` na chave — desnecessário,
`WorkspaceId` já é globalmente único), mais uma coluna `TenantId` denormalizada (copiada de
`Workspace.TenantId` na criação) só para consistência de schema com `Device`/
`TelemetryIngestionRecord` — não usada em nenhuma decisão de autorização. Não adicione `TenantId`
à chave única nem à lógica de resolução/cache — isso já foi decidido, não é uma escolha do
implementador.

## C. Escopo de alteração

**Permitido**: `Fluxo.Domain/Entities/` (novas entidades), `Fluxo.Infrastructure/Configurations/`
e `Migrations/` (nova migration com SQL bruto para `PARTITION BY RANGE`), `Fluxo.Infrastructure/
Repositories/` (novos métodos), `Fluxo.Worker.Ingestion/Models/` e `Workers/
MqttTelemetryIngestionWorker.cs` (processor), `Fluxo.Application/` (se houver use cases lendo as
5 colunas antigas).

**Permitido (verificado nesta revisão — ARCH-006)**: `scripts/mqtt-device-simulator.py` e
`scripts/provision-simulated-devices.py` **podem e devem** ser evoluídos para suportar os perfis
de benchmark V2 descritos na seção E. Hoje (inspecionado em 11 jul 2026) o simulador só suporta o
envelope legado (`schemaVersion: "1.0"`, 5 campos fixos) — não suporta `schemaVersion: 2`,
contagem configurável de métricas, tipos `Boolean`/`Text`, mutação de cardinalidade, duplicatas
determinísticas nem seed reproduzível. As alterações **devem preservar**: o modo legado atual
(argumentos e comportamento inalterados quando `--schema-version` não é passado ou é `1`),
provisionamento via `provision-simulated-devices.py` sem mudança de fluxo, credenciais/dynamic-
security (nenhuma mudança em como device se autentica), e o arquivo `--credentials-file`
consumido do jeito que já é hoje.

**Proibido nesta fase**: apagar as 5 colunas antigas ou o `IncomingTelemetryMetrics` legado
(ficam deprecated); tocar em `Fluxo.Worker.Ingestion/Services/RejectionReprocessingWorker.cs`
(fora de escopo); adicionar qualquer dependência de banco/mensageria nova (TimescaleDB, Kafka,
etc. — ver princípio "sem dependência mágica", seção 02 do documento de confronto).

**Migration**: obrigatória, com SQL bruto para particionamento nativo (EF Core não gera
`PARTITION BY` sozinho). Testar `up`/`down` em banco descartável.

## D. Segurança e custo

- `MetricKey` só aceita `^[a-z][a-z0-9._-]{0,63}$` — validar no processor antes de qualquer
  insert, rejeitar payload inteiro se inválida.
- Guardrail de cardinalidade (20 novas chaves/device/hora) precisa ser testável e observável
  (log/evento `MetricCardinalityGuardTriggered`), não só um comentário no código.
- Nenhuma dependência nova sem registrar: problema resolvido, custo operacional, lock-in, plano
  de saída, mantenedor (regra do documento de confronto, seção 02).

## E. Testes e benchmark

- Unitários: processor grava N `TelemetryPoint` corretos por mensagem; `MetricTypeMismatch`
  rejeita troca de tipo; guardrail de cardinalidade dispara no limite certo (20ª chave nova
  permitida, 21ª rejeitada — ver algoritmo em ADR-0001); idempotência por `Sequence` continua
  rejeitando duplicata.
- Integração: migration `up`/`down` roda limpo em banco descartável; `TelemetryPartitionMainte-
  nanceService` cria partição futura corretamente com o advisory lock.
- Benchmark: executar a matriz completa da seção "Benchmark obrigatório" do ADR-0001 (baseline,
  V2 leve/médio/denso, idempotência, cardinality abuse). Critério de aceite de cada perfil está
  na tabela do ADR-0001 — reportar os números reais, não "parece que passou".

### Comandos de benchmark (ARCH-006, correção 11 jul 2026)

**Verificado nesta revisão**: a afirmação anterior deste handoff ("comando exato de cada perfil
... estão na tabela do ADR") era falsa — a tabela do ADR-0001 só tem critérios de aceite, não
invocações de CLI, e `scripts/mqtt-device-simulator.py` hoje só suporta o schema legado. Os
comandos abaixo estão em dois grupos: os que já funcionam sem alteração (`legacy`), e os que
dependem de flags novas no simulador (`v2-*`), marcados explicitamente como contrato a
implementar — os nomes de argumento abaixo **são a especificação**, não sugestão.

**Perfil `legacy` (já funciona hoje, sem alteração no simulador):**

```bash
python scripts/provision-simulated-devices.py \
  --api http://localhost:5000 --email bench@fluxo.dev --password 'Benchmark123!' \
  --tenant-id bench --workspace-name bench-legacy --device-prefix bench-device \
  --devices 100 --output legacy-devices.json

python scripts/mqtt-device-simulator.py \
  --tenant-id bench --workspace-id <workspaceId-impresso-pelo-comando-acima> \
  --device-prefix bench-device --devices 100 --credentials-file legacy-devices.json \
  --host localhost --port 1883 --interval-seconds 1 --messages-per-device 300
```

100 devices × 300 mensagens = 30.000 mensagens — bate com a linha "Baseline atual" da tabela do
ADR-0001.

**`COMMAND CONTRACT — TO BE IMPLEMENTED IN PHASE 1`** (perfis V2 — exigem as flags novas abaixo em
`scripts/mqtt-device-simulator.py`; provisionamento continua usando
`provision-simulated-devices.py` sem alteração):

Flags novas a implementar no simulador, todas opcionais e sem efeito no modo legado quando
omitidas:

| Flag | Tipo | Comportamento |
|---|---|---|
| `--schema-version` | `1`\|`2` | Default `1` (comportamento atual inalterado). `2` monta o envelope V2 (`schemaVersion: 2`, `occurredAtUtc`, `metrics: {}`). |
| `--metrics-per-message` | int | Total de chaves em `metrics` por mensagem (só válido com `--schema-version 2`). |
| `--boolean-metrics` | int | Quantas das `--metrics-per-message` chaves são `Boolean` (default 0). |
| `--text-metrics` | int | Quantas são `Text` curto (default 0). O restante (`metrics-per-message - boolean - text`) é `Numeric`. |
| `--cardinality-mutation-rate` | float 0.0-1.0 | Fração das mensagens que inclui uma `MetricKey` adicional nunca antes usada (`metric_dyn_<sequence>`), para exercitar o guardrail de cardinalidade. Default 0.0. |
| `--duplicate-every` | int | A cada N mensagens publicadas, republica a última mensagem com o mesmo `sequence` (duplicata bit-exata para o campo de idempotência). Default 0 (desligado). |
| `--seed` | int | `random.seed(seed)` no início do processo — reprodutibilidade determinística de todos os valores gerados. Obrigatório em todo perfil V2 para reprodutibilidade. |

```bash
# v2-light — 100 devices x 300 msgs = 30.000 mensagens, 5 métricas numéricas
python scripts/mqtt-device-simulator.py --schema-version 2 \
  --tenant-id bench --workspace-id <workspaceId> --device-prefix bench-device --devices 100 \
  --credentials-file v2-light-devices.json --host localhost --port 1883 \
  --interval-seconds 1 --messages-per-device 300 --metrics-per-message 5 --seed 42

# v2-medium — 30.000 mensagens, 16 métricas (2 boolean, 1 text, 13 numeric)
python scripts/mqtt-device-simulator.py --schema-version 2 \
  --tenant-id bench --workspace-id <workspaceId> --device-prefix bench-device --devices 100 \
  --credentials-file v2-medium-devices.json --host localhost --port 1883 \
  --interval-seconds 1 --messages-per-device 300 --metrics-per-message 16 \
  --boolean-metrics 2 --text-metrics 1 --seed 42

# v2-dense — 30.000 mensagens, 32 métricas (4 boolean, 2 text, 26 numeric)
python scripts/mqtt-device-simulator.py --schema-version 2 \
  --tenant-id bench --workspace-id <workspaceId> --device-prefix bench-device --devices 100 \
  --credentials-file v2-dense-devices.json --host localhost --port 1883 \
  --interval-seconds 1 --messages-per-device 300 --metrics-per-message 32 \
  --boolean-metrics 4 --text-metrics 2 --seed 42

# v2-idempotency — 50 devices x 100 msgs = 5.000 mensagens únicas + 5.000 duplicatas
# (--duplicate-every 1 republica cada mensagem uma vez com o mesmo sequence)
python scripts/mqtt-device-simulator.py --schema-version 2 \
  --tenant-id bench --workspace-id <workspaceId> --device-prefix bench-device --devices 50 \
  --credentials-file v2-idempotency-devices.json --host localhost --port 1883 \
  --interval-seconds 0.5 --messages-per-device 100 --metrics-per-message 16 \
  --duplicate-every 1 --seed 7

# v2-cardinality-abuse — 10 devices x 100 msgs = 1.000 mensagens, toda mensagem introduz
# uma MetricKey nova (dispara o guardrail de 20 chaves novas/device/hora)
python scripts/mqtt-device-simulator.py --schema-version 2 \
  --tenant-id bench --workspace-id <workspaceId> --device-prefix bench-device --devices 10 \
  --credentials-file v2-cardinality-devices.json --host localhost --port 1883 \
  --interval-seconds 0.1 --messages-per-device 100 --metrics-per-message 5 \
  --cardinality-mutation-rate 1.0 --seed 99
```

Todos os perfis V2 usam `provision-simulated-devices.py` (sem alteração) para gerar o
`credentials-file` correspondente antes de rodar o simulador, exatamente como o perfil `legacy`.

## F. Critério de aceite (checklist binário)

- [ ] `dotnet test Fluxo.slnx` verde.
- [ ] Migration `up`/`down` testada em banco descartável.
- [ ] `TelemetryPoint` particionada por `RANGE (OccurredAtUtc)`; `TelemetryIngestionRecord`
      permanece não-particionada (confirma ARCH-005 — não parta as duas tabelas).
- [ ] `CHECK` de `TelemetryPoint` valida apenas exactly-one-value-slot; teste de integração
      demonstra que `MetricTypeMismatch` é responsabilidade do processor, não do `CHECK` (ARCH-003).
- [ ] Guardrail de cardinalidade implementado com `MetricDefinitionDiscoveryAudit` + advisory
      transaction lock por `(WorkspaceId, DeviceId)`, testado com dois workers/instâncias
      concorrentes (ARCH-004).
- [ ] `MetricDefinition` carrega `TenantId` denormalizado (não-chave); chave única continua
      `(WorkspaceId, MetricKey)` (TENANT-INVARIANT).
- [ ] `scripts/mqtt-device-simulator.py` suporta os perfis V2 (flags da seção E), preservando o
      modo legado sem alteração de comportamento quando `--schema-version` é omitido (ARCH-006).
- [ ] Matriz de benchmark completa executada com os comandos da seção E, números registrados no
      ADR-0001 (substituindo "pendente"): baseline, v2-light, v2-medium, v2-dense,
      v2-idempotency, v2-cardinality-abuse.
- [ ] 100 devices simulados repetidos (perfil `legacy` e `v2-light`): zero perda, zero partition
      error.
- [ ] Nenhuma leitura direta das 5 colunas antigas fora do processor de ingestão (grep limpo).
- [ ] `docs/roadmap-production-1000-devices.md` e `docs/checklist-producao-controlada.md`
      atualizados.

## G. Relatório de entrega (obrigatório ao final)

Arquivos alterados; decisões tomadas (especialmente onde os caminhos reais divergiram deste
documento); testes executados e resultado; gaps conhecidos; riscos não resolvidos. O default de
`ExpectedIntervalSec` para métricas `Discovered` já está fechado em ADR-0002
(`AlertEvaluation:DefaultExpectedIntervalSec`, 5 minutos) — nada a decidir sobre isso na Fase 1;
a Fase 3 apenas implementa o valor já especificado.

## H. Evidência de implementação — 11 jul 2026

- `AddTelemetrySchemaV2` cria catálogo, auditoria e `telemetry_points` particionada; up/down/up
  executado em PostgreSQL 16 descartável, com 8 partições e `RANGE (OccurredAtUtc)` confirmados.
- `dotnet test Fluxo.slnx`: 29/29 testes verdes, incluindo ingestão PostgreSQL legado + envelope
  V2 com valores Numeric/Boolean/Text e idempotência.
- Simulador suporta as flags V2 deste handoff; `--help` e dry-run misto determinístico executados.
- Gate permanece aberto: matriz integral de benchmark, testes concorrentes do guardrail,
  manutenção/health sob falhas e medições físicas ainda não foram executados.

Atualização: matriz, concorrência, rollback, maintenance/health, WAL e storage foram executados;
ver `docs/benchmarks/phase1-2026-07-11.md`. Binary COPY foi implementado por gate medido. O gate
continua aberto exclusivamente pelo critério dense de CPU PostgreSQL (<75%; medido 105,84%).
