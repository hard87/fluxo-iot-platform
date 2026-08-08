# Plano de testes — ensaio de resiliência EdgeWarden ↔ Fluxo

## Status desta implementação (2026-08-08)

**Já validado no hardware real** (Pi 3B+ `edgewarden`, systemd real, não simulado):

| Item | Evidência |
|---|---|
| Estado sobrevive a `kill -9` (crash abrupto) | `accumulated_runtime_seconds` preservado, `unexpected_shutdown_count` incrementado exatamente 1x, `PAUSED_UNEXPECTEDLY -> RECOVERING -> RUNNING` automático |
| `systemd` reinicia sozinho após crash | `Restart=on-failure`, recuperado em ~10s sem intervenção manual |
| Shutdown limpo (`systemctl stop`, SIGTERM real) não gera falso positivo | Evento `SHUTDOWN_REQUESTED`; restart seguinte sem `UNEXPECTED_SHUTDOWN_DETECTED` |
| **Cenário B** (`sudo reboot` real do SO, não só o processo) | `uptime` confirmou boot novo; `nodered`+harness voltaram sozinhos via `multi-user.target`; reboot ordenado gera `SHUTDOWN_REQUESTED` (SIGTERM durante o shutdown do systemd) e **não** conta como `unexpected_shutdown` — corretamente distinto de queda de energia |
| Store-and-forward (Cenário A, sem restart do broker) | `fluxo-mosquitto` parado ~40s; fila local cresceu, drenou a zero após reconexão; zero rejeições; zero sequences duplicadas no Postgres |
| Métricas novas chegam ao Fluxo | `gateway.time_synchronized`, `gateway.network_interface_up/rx_bytes/tx_bytes` confirmados em `telemetry_ingestion_records` |
| CLI (`start/status/events/stop`) consistente com o daemon systemd | Corrigido nesta sessão: CLI e daemon agora leem o mesmo `/etc/edgewarden-test/environment` |
| `scripts/report.js` (relatório completo, cruza Postgres + harness) | Implementado e rodado 2x nesta sessão; corretamente emitiu PASS no teste do reboot e **FAIL real** no drill de 5min (ver achado crítico abaixo) |
| Drill formal de 5 minutos (Cenário A+C+E combinados) | `reports/e7667665-db33-4d35-89fb-6a980dd688f4/report.md` + `evidence/` — máquina de estados do harness passou (`kill -9` recuperado, ciclo completo até `COMPLETED`), mas revelou uma perda de dados real na plataforma (ver abaixo) |

## ⚠️ Achado crítico do drill de 5 minutos (2026-08-08): janela de perda de dados no restart do mosquitto

**Não é um bug do EdgeWarden/harness — é uma lacuna real na plataforma Fluxo**, encontrada exatamente
pelo tipo de ensaio que esta especificação pediu para não mascarar (seção 34 da especificação
original). Resumo:

1. `fluxo-mosquitto` foi parado (`docker stop`) e reiniciado (`docker start`) simulando Cenário A.
2. O gateway `edgewarden` reconectou **imediatamente** ao voltar (mesmo segundo) e começou a
   republicar sua fila local (sequences 4055, 4056, 4057 pendentes).
3. O `fluxo-worker-ingestion` só reconectou e **resubscreveu** ao tópico **7 segundos depois**
   (log do worker: primeira mensagem processada após resubscrever foi a sequence 4057 — nada antes).
4. Nesses 7 segundos, o broker deu PUBACK (QoS 1) para 4055 e 4056 — sem nenhum assinante
   registrado para rotear a mensagem. O gateway, ao receber o PUBACK, corretamente removeu-as do
   spool local (fez exatamente o que o protocolo manda). As duas mensagens nunca chegaram ao
   Postgres — nem aceitas, nem rejeitadas: simplesmente não têm destino.
5. Observação adicional: o log do mosquitto mostra `Restored 0 clients` / `Restored 0 subscriptions`
   em praticamente todo restart observado nesta sessão (não só neste drill) — a persistência de
   sessão do broker não está preservando a assinatura do worker entre reinicializações, o que é a
   causa raiz da janela.

**Causa raiz real**: a "confirmação" que o `gateway-spool.js` usa hoje para descartar uma mensagem
da fila local é o **PUBACK do broker** (nível MQTT/transporte) — mas a especificação original do
ensaio (seção 7) pedia confirmação **do Fluxo** (nível aplicação/persistência). PUBACK garante só
que o broker recebeu a mensagem, não que ela foi roteada a um consumidor vivo. Em operação normal
(broker nunca reinicia) isso nunca aparece; em qualquer reinício do broker, existe uma janela real
onde mensagens podem ser confirmadas ao gateway e nunca chegarem ao worker.

**Decisão do usuário (2026-08-08): documentar como limitação conhecida da plataforma e prosseguir
com os drills de 15min/1h**, sem bloquear nesta etapa. Isso significa:
- Os próximos drills (15min/1h, que também reiniciam o `fluxo-mosquitto`) provavelmente vão
  reproduzir a mesma janela de perda sempre que o broker reiniciar — **isso é esperado, não um novo
  bug a cada vez**, e não deve ser reportado como surpresa se `report.js` marcar FAIL de novo pelo
  mesmo motivo.
- **Antes do ensaio oficial de 24h**, esta lacuna precisa de uma decisão de arquitetura real, uma
  das duas (fora do escopo do EdgeWarden isoladamente, é decisão de plataforma Fluxo):
  - (a) corrigir a persistência de sessão do mosquitto para que a assinatura do worker sobreviva a
    um restart do broker (elimina a janela na origem); ou
  - (b) mudar o `gateway-spool.js` para só descartar uma mensagem da fila após uma confirmação
    fim-a-fim do Fluxo (ex.: um ACK aplicativo via tópico de resposta, não o PUBACK do MQTT) — mais
    fiel à especificação original, mas exige um mecanismo novo dos dois lados.
- Enquanto isso não for decidido, **todo relatório de ensaio deve ser lido puxando o comparativo
  contra o Postgres (não só os eventos locais do gateway)** — é exatamente o que `report.js` já faz.

**Reprodutibilidade confirmada**: o mesmo padrão se repetiu no drill de 15 minutos (Cenário B+E,
reboot real do Pi durante backlog + restart do mosquitto), desta vez com 3 sequences perdidas
(4091–4093) em vez de 2 — mesma assinatura (mensagens que estavam pendentes exatamente no momento
do restart do broker, primeiras a serem descartadas no PUBACK antes do worker resubscrever). Não é
um evento raro/aleatório; acontece de forma consistente sempre que o `fluxo-mosquitto` reinicia com
mensagens pendentes no gateway.

## Pendências restantes antes do ensaio oficial de 24h

1. **Decisão de arquitetura sobre a janela de perda de dados no restart do mosquitto** (ver achado
   crítico acima) — bloqueante para produção controlada, não bloqueante para continuar os drills
   curtos (decisão do usuário).
2. **Drills formais de 15min e 1h** — em andamento nesta sessão.
3. **Categorização de erro de rede** (DNS/timeout/recusado/auth, seção 9 da especificação original)
   — o harness hoje só distingue `COMMUNICATION_LOST`/`COMMUNICATION_RECOVERED` via padrão de texto
   no log do `nodered`; não diferencia a causa.
4. **`docs/architecture.md`**, **`docs/failure-model.md`**, **`docs/recovery-strategy.md`** — o
   conteúdo já existe espalhado (plano aprovado desta sessão + `current-state-assessment.md`), mas
   não foi consolidado nesses arquivos separados como a especificação original sugeria.

## Ordem recomendada a partir daqui

1. ~~Escrever `scripts/report.js`~~ — feito.
2. ~~Rodar o ensaio curto de 5 min~~ — feito, achado crítico documentado acima.
3. Rodar o drill de 15 min (Cenário B+E combinado: reboot real durante backlog ativo) e o de 1h
   (estabilidade pura, sem falha injetada).
4. Consolidar as descobertas de todos os drills num resumo único antes de decidir o ensaio oficial
   de 24h — inclui decidir o item 1 da lista de pendências acima.
5. Só então o usuário decide explicitamente iniciar o ensaio oficial de 24h
   (`EDGEWARDEN_TEST_TARGET_DURATION_SECONDS=86400`, o default de produção da unit).

## Critérios de aprovação (herdados da especificação original, seção 26)

- Nenhuma métrica válida perdida, ou toda perda com causa tecnicamente identificada. **Parcialmente
  atendido**: a perda encontrada no drill de 5min tem causa tecnicamente identificada (ver achado
  crítico acima), mas ainda não corrigida — critério textual da spec ("perdida... ou... causa
  identificada") tecnicamente satisfeito, decisão do usuário foi prosseguir sabendo disso.
- 100% das reinicializações testadas recuperadas automaticamente (**validado**, para o processo do
  harness — crash e reboot ordenado do SO).
- 100% dos dados pendentes permanecem persistidos durante indisponibilidade (**validado** para o
  período de indisponibilidade em si; a perda observada acontece na *reconexão*, não durante a
  indisponibilidade — distinção real, não desculpa).
- O contador do ensaio sobrevive a reboot/crash (**validado**, para crash de processo e reboot real
  do SO).
- O ensaio não reinicia do zero após falha (**validado**).
- Dados retransmitidos não geram duplicação lógica no Fluxo (**validado**, via constraint única
  existente).
