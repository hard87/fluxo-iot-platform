# Runbook — edgewarden-test-harness

Passo a passo para instalar, operar e encerrar o ensaio de resiliência sem depender de quem
implementou. Todos os comandos abaixo já foram executados e validados fisicamente no
`edgewarden` (Raspberry Pi 3B+, `junior@192.168.9.18`) em 2026-08-08 — ver evidência na tarefa
"Implantar no Pi e reverificar store-and-forward/idempotência" desta sessão.

## 1. Pré-requisitos

- Node.js 18+ com `node:sqlite` disponível (confirmado no Node 22.23.2 do Pi, sem flag extra).
- `nodered.service` já instalado e funcionando (`Fluxo/devices/pi-gateway-reference-node/`).
- SSH com chave para `junior@<ip-do-pi>` (ver
  [`validacao-comunicacao-edgewarden.md`](../../pi-gateway-reference-node/docs/validacao-comunicacao-edgewarden.md)
  seção 2.1 se o IP mudou).

## 2. Instalar

```bash
# Do host de desenvolvimento:
scp -r src junior@<ip-do-pi>:/opt/edgewarden-test-harness/    # 1ª vez: sudo mkdir -p /opt/edgewarden-test-harness && sudo chown junior:junior /opt/edgewarden-test-harness
scp systemd/edgewarden-test-harness.service junior@<ip-do-pi>:/tmp/

# No Pi:
sudo install -m 0644 /tmp/edgewarden-test-harness.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable edgewarden-test-harness   # NÃO usar --now: o ensaio só começa com `start`
```

Os defaults de produção já estão na unit (`gateway=edgewarden`, alvo=86400s, checkpoint a cada 30s).
Para sobrescrever sem editar a unit, criar `/etc/edgewarden-test/environment` (mesmo formato de
`KEY=VALUE` do `environment.example` do gateway) — o CLI e o daemon leem o mesmo arquivo, então
nunca ficam de configurações diferentes (ver `harness.js`, `loadEnvironmentFileIfPresent`).

## 3. Configurar (opcional)

```bash
sudo mkdir -p /etc/edgewarden-test
sudo tee /etc/edgewarden-test/environment <<'EOF'
EDGEWARDEN_TEST_TARGET_DURATION_SECONDS=86400
EDGEWARDEN_TEST_CHECKPOINT_INTERVAL_SECONDS=30
EOF
```

## 4. Iniciar

```bash
node /opt/edgewarden-test-harness/src/cli.js start
```

Isso: (1) chama `sudo systemctl start edgewarden-test-harness`; (2) cria uma sessão nova em
`CREATED` (a menos que já exista uma ativa, caso em que só garante que o serviço está rodando). O
daemon assume a sessão `CREATED` no ciclo seguinte (até `EDGEWARDEN_TEST_CHECKPOINT_INTERVAL_SECONDS`
depois).

## 5. Consultar status

```bash
node /opt/edgewarden-test-harness/src/cli.js status
node /opt/edgewarden-test-harness/src/cli.js events --limit=20
journalctl -u edgewarden-test-harness -f          # acompanhar em tempo real
journalctl -u nodered -f                          # log da captura/publicação em si
```

## 6. Simular perda do Fluxo (Cenário A/D da especificação)

Do host que roda o Fluxo:

```bash
docker stop fluxo-mosquitto
# aguardar o tempo desejado, observando `gateway-spool.js status` crescer via SSH no Pi:
ssh junior@<ip-do-pi> "node /home/junior/.node-red/fluxo-gateway/scripts/gateway-spool.js status"
docker start fluxo-mosquitto
# confirmar drenagem:
ssh junior@<ip-do-pi> "node /home/junior/.node-red/fluxo-gateway/scripts/gateway-spool.js status"
```

Validar depois, no Postgres do host Fluxo: zero linhas novas em `telemetry_ingestion_rejections`
para o tópico do device, e nenhuma `Sequence` duplicada no intervalo (`GROUP BY Sequence HAVING
count(*) > 1`).

## 7. Simular reinicialização normal (Cenário B)

```bash
ssh junior@<ip-do-pi> "sudo systemctl stop edgewarden-test-harness"   # SIGTERM limpo
ssh junior@<ip-do-pi> "sudo systemctl start edgewarden-test-harness"
node /opt/edgewarden-test-harness/src/cli.js events --limit=5
```

Esperado: evento `SHUTDOWN_REQUESTED` no stop, **nenhum** `UNEXPECTED_SHUTDOWN_DETECTED` no
restart seguinte, `accumulated_runtime_seconds` continuando de onde parou.

## 8. Simular reinicialização inesperada (Cenário C)

```bash
ssh junior@<ip-do-pi> "PID=\$(systemctl show edgewarden-test-harness -p MainPID --value); sudo kill -9 \$PID"
```

`systemd` reinicia sozinho em até `RestartSec` (10s, ver `.service`). Esperado nos eventos:
`UNEXPECTED_SHUTDOWN_DETECTED` (severidade WARN) seguido de `TEST_RECOVERED`, e
`unexpected_shutdown_count` incrementado em exatamente 1.

## 9. Reinicialização durante backlog (Cenário E)

Combinar as seções 6 e 8: parar o Fluxo, esperar a fila crescer, matar o daemon (ou reiniciar o
Pi inteiro com `sudo reboot`) enquanto a fila ainda está cheia, restaurar o Fluxo, e confirmar que
a sincronização retoma sem reenviar como "nova" nenhuma métrica já confirmada (checar sequence no
Postgres antes/depois).

## 10. Finalizar / parar manualmente

```bash
node /opt/edgewarden-test-harness/src/cli.js stop
```

Isso **não** marca o ensaio como concluído — só para o processo de forma limpa. Rodar `start` de
novo retoma a mesma sessão de onde parou. O ensaio só vira `COMPLETED` sozinho quando
`accumulated_runtime_seconds >= alvo` **e** a fila local (`gateway-spool.js status`) estiver zerada.

## 11. Gerar relatório

```bash
node /opt/edgewarden-test-harness/src/cli.js report        # resumo local (só o que o gateway sabe)
node scripts/report.js --test-id=<id>                      # relatório completo, roda no HOST Fluxo
                                                             # (Postgres só escuta em 127.0.0.1, não
                                                             # é alcançável pela rede a partir do Pi)
```

`scripts/report.js` ainda não implementado nesta fase — ver `docs/test-plan.md` para o que ele
precisa cobrir antes do ensaio oficial de 24h.
