# Relatório do ensaio 938e1554-a845-4e97-b9cc-3f689b7b2ad6

**Resultado: PASS WITH WARNINGS**

Avisos:
- 1 tipo(s) de rejeição registrados.

## Informações

- Test ID: `938e1554-a845-4e97-b9cc-3f689b7b2ad6`
- Gateway ID: `edgewarden`
- Início: 2026-08-09T02:34:49.250Z
- Fim: 2026-08-11T00:25:27.976Z
- Tempo efetivo acumulado: 86414.3s / alvo 86400s
- Estado final: COMPLETED
- capture_completed: sim / delivery_completed: sim

## Métricas

- Aceitas pelo Fluxo: 2860
- Faixa de sequence observada: 5289 .. 8148
- Sequences ausentes (gap não explicado): 0
- Duplicadas: 0
- Rejeitadas: 38
- Maior atraso de entrega (OccurredAtUtc -> ReceivedAtUtc): 1322.5 min

### Rejeições por tipo

- Duplicate (Mensagem duplicada por tenant/workspace/device/sequence.): 38

## Reinicializações

- Total de reinícios inesperados detectados: 0
- Recuperados automaticamente: 0 (ver eventos TEST_RECOVERED)

## Comunicação

- Quedas de comunicação detectadas (COMMUNICATION_LOST): 19
- Recuperações (COMMUNICATION_RECOVERED): 2

## Store-and-forward

- Backlogs iniciados: 9
- Backlogs drenados: 10
- Maior profundidade de fila observada: 2240

## Recursos do gateway

- CPU temp média/máxima: 42.5°C / 51.5°C
- RAM usada média/máxima: 34.0% / 50.7%
- Disco usado no início/fim: 17.5% / 17.5%
- Load(1m) média/máxima: 0.2 / 2.3

## Contagem de eventos por tipo

- COMMUNICATION_LOST: 19
- BACKLOG_DRAINED: 10
- BACKLOG_STARTED: 9
- COMMUNICATION_RECOVERED: 2
- TEST_STARTED: 1
- STALE_LOCK_RECOVERED: 1
- SHUTDOWN_REQUESTED: 1
- TEST_FINALIZING: 1
- TEST_CAPTURE_COMPLETED: 1
- TEST_COMPLETED: 1

## Conclusão

Ver avisos acima -- este ensaio encontrou pelo menos uma divergência que precisa ser explicada antes de considerar o EdgeWarden aprovado para produção controlada.
