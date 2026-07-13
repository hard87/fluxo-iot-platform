# Relatório — Higienização Documental Pós-Fase 2

## 1. Objetivo

Consolidar o estado documental após a conclusão da Produto Fase 2, preservando evidência
histórica e sem alterar comportamento, arquitetura implementada ou código funcional.

## 2. Estado inicial do repositório

- Branch: `snapshot-auth-portal-mvp-20260526` (2 commits à frente do remoto).
- HEAD: `5c8ced1f925e4151e78f7dd66ae8e071a5c2dbe7` (`5c8ced1`).
- Working tree: suja antes desta execução.
- Documentos rastreados já modificados: `docs/checklist-producao-controlada.md` e
  `docs/roadmap-production-1000-devices.md`.
- Diretórios documentais não rastreados já existentes: `docs/adr`, `docs/benchmarks`,
  `docs/handoff` e `docs/product`.
- Também já existiam mudanças de frontend, backend, testes, scripts, migration, packages e
  artefatos. Nenhuma foi descartada ou atribuída a esta higienização.

## 3. Documentos revisados

| Documento | Alteração | Motivo |
|---|---|---|
| `roadmap-production-1000-devices.md` | Consolidado e separado em trilhas | Remover colisão nominal e pendências obsoletas |
| `checklist-producao-controlada.md` | Evidências e pendências ajustadas | Não inferir TLS/health; registrar riscos documentados |
| `adr/0003-telemetry-query-api.md` | Clarificação localizada de `count` | Alinhar DTO ao comportamento implementado |
| `benchmarks/phase2-query-explain-2026-07-12.md` | Resumo e tabela Q1–Q7 | Facilitar leitura sem alterar medições |
| `portal-web-mvp.md` | Estado pós-Fase 2 | Registrar Explorer e limitações confirmadas |
| handoffs das Fases 1 e 2 | Cabeçalho histórico | Evitar reutilização como próxima instrução |
| relatório da Fase 2 | Referências e classificação do gap | Melhorar rastreabilidade sem alterar resultados |
| `project-status.md` | Criado | Oferecer ponto de entrada factual e curto |

## 4. Inconsistências encontradas

- Roadmap: `TelemetryPoint` aparecia como ainda não implementada. Evidência: migration, testes,
  ADR-0001 e relatório da Fase 1. Resolução: estado consolidado como implementado e particionado.
- Roadmap: baseline e benchmark da Produto Fase 0/1 apareciam pendentes. Evidência: benchmarks
  original e normalizado. Resolução: Fases 0 e 1 marcadas CONCLUÍDAS.
- Roadmap: infraestrutura e produto reutilizavam os mesmos números de fase. Resolução: títulos
  `Infra Fase` e `Produto Fase`, sem renumerar a sequência.
- ADR-0003: exactly-one-value-slot conflitava com `count` em `SampleCount`. Evidência:
  `TelemetryQueryRepository`, DTO, matriz de testes e relatório da Fase 2. Resolução:
  exceção explícita somente no DTO de resposta.
- Portal: documento não continha o Telemetry Explorer. Evidência: relatório e testes da Fase 2.
  Resolução: telas, fluxo, tipos e limites atualizados.
- Checklist: riscos documentados continuavam desmarcados. Evidência: relatórios das Fases 1 e
  2 e roadmap. Resolução: somente esse item documental foi marcado; TLS, health, backup/restore,
  simulador e guia do piloto permanecem abertos quando não comprovados.

## 5. Roadmap consolidado

- Produto: Fases 0, 1 e 2 CONCLUÍDAS; Fase 3 PRÓXIMA; Fases 4 e 5 NÃO INICIADAS.
- Infraestrutura: Infra Fase 1 CONCLUÍDA; Infra Fase 2 PRÓXIMA; Infra Fases 3 e 4 NÃO INICIADAS.
- 1000 devices permanece meta de escala, não capacidade de produção comprovada.

## 6. ADR-0003

A ambiguidade `count`/`SampleCount` foi clarificada em seção datada. `SampleCount` é o resultado
obrigatório de `count`; os três slots tipados podem ser nulos. A exceção não altera o CHECK de
`TelemetryPoint`, persistência, contrato de banco ou migrations.

## 7. Checklist

`Riscos restantes registrados` foi marcado com referências aos relatórios e ao roadmap. Itens
sem evidência objetiva permaneceram abertos, inclusive TLS operacional, health checks completos,
backup/restore, firmware 24h e revisões documentais do piloto.

## 8. Project status

`docs/project-status.md` passa a ser o ponto de entrada curto, com snapshot, fases, arquitetura,
capacidades comprovadas, próxima fase, pendências, riscos e fontes normativas/evidenciais.

## 9. Arquivos não alterados por decisão de escopo

Nenhum arquivo de código, migration, Dockerfile, Compose, package ou dependência foi alterado por
esta execução. As modificações preexistentes nesses grupos foram preservadas.

## 10. Achados fora de escopo

- O relatório histórico da Fase 1 ainda registra gaps que foram posteriormente fechados pela
  Fase 2. O texto foi preservado como fotografia daquela fase; o estado atual está neste relatório,
  no roadmap e em `project-status.md`.
- A unidade `°C` do seed manual de QA apareceu como `?C` no console Windows. O relatório da
  Fase 2 classifica isso como limitação de encoding do seed, sem evidência de bug do contrato.
- Riscos de bundle, Recharts, CSP/fonte e sessão permanecem registrados, sem correção por serem
  explicitamente fora do escopo documental.

## 11. Validação final

- `git diff --check` do recorte documental rastreado: aprovado. A revisão do conjunto completo
  preparado para publicação também sinaliza hard breaks Markdown históricos e linhas finais
  extras em arquivos preexistentes da implementação; nenhum foi introduzido como mudança
  funcional nesta higienização.
- Links Markdown relativos dos documentos alterados: validados; nenhum destino inexistente.
- Diff desta execução: exclusivamente documental; mudanças preexistentes de código permanecem
  distinguíveis pelo estado inicial registrado.

## 12. Gate

DOCUMENTATION CONSOLIDATION COMPLETE
