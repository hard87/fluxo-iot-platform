# EdgeWarden 24h Resilience Test Harness

Camada de orquestração/observação para o ensaio de resiliência de 24h entre o gateway `edgewarden`
e a plataforma Fluxo. **Não captura nem publica telemetria** — isso continua sendo responsabilidade
do `nodered.service`/`gateway-spool.js` em
[`pi-gateway-reference-node/`](../pi-gateway-reference-node/), já testado e em produção desde a
Fase 5. Este harness só observa esse pipeline (via `gateway-spool.js status` e o journal do
`nodered`) e mantém, de forma persistente e resistente a crash/reboot, uma máquina de estados do
ensaio em si: quanto tempo efetivo já rodou, quantas interrupções houve, se foram recuperadas
automaticamente, e um log estruturado de eventos.

Ver [`docs/current-state-assessment.md`](docs/current-state-assessment.md) para o levantamento que
motivou as escolhas técnicas abaixo, e [`docs/test-plan.md`](docs/test-plan.md) para o que já foi
validado no hardware real e o que ainda falta antes do ensaio oficial de 24h.

## Arquitetura

```
gateway-spool.js status  ─┐
journal do nodered        ├─►  harness.js (systemd, checkpoint a cada 30s)  ──►  SQLite (node:sqlite)
/sys (CPU/disco/rede)     ┘         │
                                     ▼
                          máquina de estados do ensaio
              CREATED → RUNNING ⇄ RUNNING_DEGRADED → FINALIZING → COMPLETED
                       ↳ (crash) → PAUSED_UNEXPECTEDLY → RECOVERING ↗
```

- `src/db.js` — schema SQLite (`TestSession`, `Checkpoint`, `Event`), `PRAGMA synchronous = FULL`
  (fsync a cada commit — o ensaio existe para provar que não se perde dado num corte de energia,
  então o próprio checkpoint não pode ser o elo fraco).
- `src/harness.js` — daemon: adota/retoma sessão no boot, distingue shutdown limpo de sujo, loop de
  checkpoint, detecção de backlog/queda de MQTT/disco cheio.
- `src/cli.js` — `edgewarden-test start|stop|status|events|report`.
- `systemd/edgewarden-test-harness.service` — `User=junior`, `StateDirectory=edgewarden-test`
  (systemd cria `/var/lib/edgewarden-test` já com o dono certo, sem `sudo mkdir` manual).

## Uso rápido

Ver [`docs/runbook.md`](docs/runbook.md) para o passo a passo completo (instalar, configurar,
simular cada cenário de falha, gerar relatório). Resumo:

```bash
node src/cli.js start    # cria/retoma sessão + garante o serviço rodando
node src/cli.js status
node src/cli.js events --limit=20
node src/cli.js stop     # parada manual controlada -- NÃO marca o ensaio como concluído
```

## Por que não SQLite para o spool de telemetria também?

Só o estado *do ensaio* usa SQLite. O spool de mensagens (`gateway-spool.js`) continua em arquivo
por mensagem, atômico, já testado com reboot/crash/replay na Fase 5 — reescrever isso para SQLite
trocaria um mecanismo comprovado por um novo, sem necessidade. Ver a tabela de decisões em
`docs/current-state-assessment.md`.
