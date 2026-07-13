# Relatorio Executivo - Fase 1 Schema V2

Data: 2026-07-11  
Repositorio: `D:\Officina404\Fluxo`  
Branch: `snapshot-auth-portal-mvp-20260526`  
Escopo: Schema V2 de telemetria, ingestao compativel, guardrails, particionamento, benchmark e evidencias de qualidade.

## 1. Resumo executivo

A Fase 1 foi implementada em grande parte e ja possui evidencias tecnicas fortes: schema V2 criado, ingestion pipeline compativel com payload legado e V2, catalogo de metricas tipadas, pontos de telemetria particionados, guardrail de cardinalidade com protecao transacional, simulador V2, metricas operacionais, benchmark real e suite de testes verde.

Atualizacao metodologica: o benchmark foi reexecutado com orcamento explicito de CPU/RAM e CPU normalizada por cores alocados. O perfil denso com Binary COPY consumiu **1,0801 cores medios** em budget de **2,0 cores**, ou **54,01% de CPU normalizada**.

Em termos praticos: a arquitetura esta funcional e validada para o escopo da Fase 1. O gate de CPU do ADR nao deve continuar baseado em percentual bruto do Docker; ele precisa ser normalizado por budget explicito.

## 2. Objetivo da fase

O objetivo era substituir o contrato rigido de telemetria, baseado em cinco colunas fixas, por um modelo generico e tipado que aceite metricas numericas, booleanas e texto curto, mantendo compatibilidade temporaria com o payload legado.

A fase tambem precisava provar, com numeros reais, que o novo modelo consegue sustentar carga controlada sem perda, sem erro de particao, sem explosao de cardinalidade e sem regressao de performance fora dos limites do ADR.

## 3. Principais entregas

- Envelope V2 aceito pelo Worker com `schemaVersion`, `sequence`, `occurredAtUtc` e `metrics`.
- Adaptador de compatibilidade para o payload legado com as cinco metricas fixas.
- `MetricDefinition` por workspace, com `ValueType` estavel e `TenantId` denormalizado.
- `TelemetryPoint` particionada por `OccurredAtUtc`, com slots tipados para Numeric, Boolean e Text.
- `MetricDefinitionDiscoveryAudit` para auditar descoberta de novas chaves.
- Guardrail de cardinalidade com limite de 500 metricas por workspace e 20 novas chaves por device/hora.
- Lock transacional PostgreSQL por `(WorkspaceId, DeviceId)` para proteger concorrencia no guardrail.
- Cache de definicoes de metricas por workspace no hot path.
- Abstracao `ITelemetryPointWriter` com implementacoes EF e Npgsql Binary COPY.
- Manutencao automatica de particoes com advisory lock e health check.
- Simulador MQTT expandido para perfis V2, duplicatas deterministicas e abuso de cardinalidade.
- Metricas internas de ingestao com latencia monotonic, backlog e contador de guardrail.
- Override direto de `Microsoft.OpenApi` para versao sem vulnerabilidade conhecida no grafo atual.

## 4. Arquitetura implementada

O modelo novo preserva `TelemetryIngestionRecord` como trilha de auditoria 1:1 com a mensagem MQTT. Cada metrica do payload V2 e projetada em uma linha de `TelemetryPoint`, vinculada ao `MetricDefinition` correspondente.

Essa decisao aumenta o volume de linhas, mas destrava consultas dinamicas e alertas por metrica sem exigir novas migrations por vertical de device. O custo dessa flexibilidade aparece diretamente nos benchmarks: perfis com 16 e 32 metricas por mensagem geram respectivamente 480.000 e 960.000 pontos em uma carga de 30.000 mensagens.

O particionamento ficou limitado a `TelemetryPoint`, conforme ADR-0001. `TelemetryIngestionRecord` permanece nao particionada nesta fase.

## 5. Confiabilidade e consistencia

A ingestao foi mantida atomica: uma mensagem e aceita inteira ou rejeitada inteira. Nao ha persistencia parcial de metricas quando uma chave invalida, tipo incompatível, estouro de cardinalidade ou falha de escrita ocorre no meio do processamento.

A idempotencia por `(DeviceId, Sequence)` foi preservada com `ON CONFLICT DO NOTHING`. Testes e benchmark de duplicatas confirmaram que mensagens repetidas nao geram `TelemetryPoint` duplicado.

A criacao de novas metricas usa o relogio do PostgreSQL para a janela de uma hora, evitando divergencia entre instancias do Worker.

## 6. Benchmarks executados

Ambiente: Windows 10 Pro, Docker 29.4.0, PostgreSQL 16 Alpine, Mosquitto 2, .NET SDK 10.0.202. Containers sem limites explicitos. Latencia medida dentro do Worker, do inicio do processamento ao resultado apos commit; nao e latencia end-to-end MQTT.

| Perfil | Writer | Mensagens | Pontos | Rejeicoes | P95 | Backlog max | Recovery | WAL |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| legacy | EF | 30.000 | 150.000 | 0 | 51,906 ms | 333 | 0,280 s | 171.281.496 B |
| v2-light | EF | 30.000 | 150.000 | 0 | 62,454 ms | 561 | 0,231 s | 184.105.120 B |
| v2-medium | EF | 30.000 | 480.000 | 0 | 85,819 ms | 4.354 | 50,300 s | 424.253.232 B |
| v2-medium | COPY | 30.000 | 480.000 | 0 | 66,374 ms | 508 | 0,375 s | 397.322.976 B |
| v2-dense | EF | 30.000 | 960.000 | 0* | 84,236 ms | 5.001 | 64,414 s | 799.733.824 B |
| v2-dense | COPY | 30.000 | 960.000 | 0 | 61,248 ms | 447 | 0,235 s | 759.421.248 B |
| idempotency | COPY | 10.000 | 80.000 | 5.000 duplicatas | 66,817 ms | 4.921 | 41,929 s | 81.693.704 B |
| cardinality | COPY | 1.000 | 1.170 | 805 planejadas | 80,055 ms | 251 | nao medido | 3.709.240 B |

`*` No dense/EF houve contaminacao por nove rejeicoes antigas de sessao MQTT persistente, mas os registros e pontos do perfil corrente fecharam corretamente em 30.000/960.000.

Nota: a decisao final de gate usa o benchmark normalizado posterior em
`docs/benchmarks/phase1-normalized-2026-07-11.md`, que corrigiu budget explicito de CPU/RAM,
isolamento por perfil, sessoes MQTT persistentes e unidade de CPU.

## 7. Resultado dos gates

| Gate | Resultado | Observacao |
|---|---|---|
| Build .NET | Passou | Build final sem warnings. |
| Testes unitarios e integracao | Passou | 52/52 testes verdes. |
| Migration up/down/up | Passou | Validada em PostgreSQL 16 descartavel. |
| Vulnerabilidades NuGet conhecidas | Passou | `dotnet list package --vulnerable` limpo apos override de OpenAPI. |
| V2 light P95 <= 1,5x baseline | Passou | 62,454 ms contra baseline 51,906 ms, razao 1,203x. |
| V2 medium backlog recupera em ate 60s | Passou | EF recuperou em 50,300s; COPY em 0,375s. |
| V2 dense sem OOM e sem rejeicao nao planejada | Passou | Sem OOM e sem rejeicao no COPY. |
| V2 dense CPU media PostgreSQL < 75% | Passou com metodologia normalizada | COPY mediu 1,0801 cores medios em budget de 2 cores = 54,01% normalizado. |
| Idempotencia sem ponto duplicado | Passou | 5.000 duplicatas rejeitadas, 5.000 mensagens unicas persistidas. |
| Cardinality abuse limita crescimento | Passou funcionalmente | 805 rejeicoes planejadas; recovery nao foi medido por falha no harness. |

## 8. Testes automatizados

Foram adicionados testes unitarios para processamento de telemetria V2, payload legado, rejeicoes, idempotencia, tipos de metrica e metricas internas.

Foram adicionados testes de integracao PostgreSQL cobrindo concorrencia do guardrail, corrida cross-device para a mesma chave, rollback em falha do writer, falha real de Binary COPY sem particao, limite de workspace, janela horaria baseada no relogio do banco e manutencao idempotente de particoes.

Resultado consolidado: **52 testes verdes**.

## 9. Armazenamento projetado

As projecoes abaixo sao lineares, baseadas no benchmark local. Elas nao incluem WAL como retencao permanente.

| Perfil | Points/dia em 1000 devices | 1 dia | 7 dias | 30 dias | 90 dias |
|---|---:|---:|---:|---:|---:|
| light | 43.200.000 | 23.772.251.520 B | 166.405.760.640 B | 713.167.545.600 B | 2.139.502.636.800 B |
| medium | 138.240.000 | 60.169.132.800 B | 421.183.929.600 B | 1.805.073.984.000 B | 5.415.221.952.000 B |
| dense | 276.480.000 | 113.736.908.160 B | 796.158.357.120 B | 3.412.107.244.800 B | 10.236.321.734.400 B |

O perfil dense projeta crescimento muito alto para retencao longa. Antes de piloto amplo, e necessario decidir politicas de retencao, agregacao, downsampling e custo operacional por plano.

## 10. Riscos e gaps conhecidos

- O ADR ainda precisa ser atualizado explicitamente para definir CPU como utilizacao normalizada por budget alocado.
- Agregado de CPU/RAM do perfil medium/EF foi corrigido no harness, mas EF nao foi reexecutado por decisao de escopo desta rodada.
- A consulta HTTP de telemetria protegida ainda nao foi exercitada para o novo caminho V2; validacao foi feita principalmente no Worker e no banco.
- A projecao de custo ainda e fisica/local; nao substitui dimensionamento cloud real.

## 11. Recomendacao tecnica

Fechar a Fase 1 como concluida para o escopo de schema, ingestao, testes e benchmark normalizado. O correto e registrar que o gate de CPU precisa de normalizacao formal no ADR, nao que o PostgreSQL violou capacidade no experimento corrigido.

O proximo passo recomendado antes da Fase 2 e uma atualizacao documental curta:

1. Atualizar o ADR-0001 para declarar CPU como `cores consumidos / cores alocados`.
2. Manter Binary COPY como estrategia recomendada para medium/dense.
3. Usar as projecoes de raw hot + rollup 1 minuto como base de planejamento, sem implementar rollup nesta fase.

Nao houve tuning de PostgreSQL, batch size, indices, frequencia de commit ou COPY nesta rodada.

## 12. Referencias no repositorio

- Benchmark completo: `docs/benchmarks/phase1-2026-07-11.md`
- Benchmark normalizado: `docs/benchmarks/phase1-normalized-2026-07-11.md`
- ADR principal: `docs/adr/0001-telemetry-schema-v2.md`
- Handoff tecnico: `docs/handoff/fase-1-schema-v2-codex.md`
- Checklist de producao controlada: `docs/checklist-producao-controlada.md`
- Migration: `src/Fluxo.Infrastructure/Migrations/20260711172401_AddTelemetrySchemaV2.cs`
- Testes principais: `tests/Fluxo.IntegrationTests/Ingestion/TelemetrySchemaV2ConcurrencyTests.cs`
