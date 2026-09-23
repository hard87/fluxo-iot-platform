# Troubleshooting do Pi Gateway

## O flow não conecta

Execute `scripts/validate-environment.sh`, teste TCP de saída para `host:8883` e confira se o host
ou IP está no SAN do certificado. Não habilite `1883`, `rejectUnauthorized=false` ou fallback sem
TLS. Uma CA ou credencial inválida deve produzir falha de conexão.

## A fila cresce

Use `scripts/diagnostics.sh` e `journalctl -u nodered`. Confirme status do broker, DNS/rota, relógio
do Pi e ACL do tópico exato. Não remova arquivos enquanto o flow está ativo. Depois da reconexão,
o replay é serial e preserva a ordem.

## Sequence ou spool corrompido

Pare o Node-RED antes de intervenção manual. Preserve uma cópia de `state/`. O runtime recupera o
maior high-water mark válido. Se os dois checkpoints estiverem inválidos e o spool vazio, consulte
o último sequence no backend e restaure os checkpoints com valor igual ou maior. Nunca reinicie em
zero um device já ingerido.

## Recuperação de espaço

Verifique `state/spool`, os limites e a idade das mensagens. A política automática descarta
mensagens expiradas e rejeita a mais nova quando quantidade ou bytes atingem o limite. Registre
qualquer remoção manual e mantenha os checkpoints de sequence.

## Node-RED não inicia

Valide JSON com `node -e "JSON.parse(require('fs').readFileSync('flow.json'))"`, permissões e logs.
O deploy preserva outros flows, mas substitui a aba e os config nodes com IDs reservados da
referência. Restaure o backup do diretório `.node-red` se uma configuração preexistente colidir.

## Relógio não sincronizado / timestamps suspeitos

`nodered.service` tem um gate de boot (`ExecStartPre`, drop-in em
`/etc/systemd/system/nodered.service.d/ntp-gate.conf`) que espera até 120s por
`timedatectl show -p NTPSynchronized --value` = `yes` antes de subir; se o tempo esgotar (sem rede
no boot — o Pi não tem RTC), sobe mesmo assim e registra um aviso via `systemd-cat -t
fluxo-ntp-gate`. Isso reduz, mas não elimina, o risco de publicar com relógio errado logo após um
boot frio.

Como segunda camada de defesa, `gateway-spool.js`'s `enqueue()` verifica `NTPSynchronized` a cada
publicação (não só no boot): se o SO afirma explicitamente que o relógio **não** está sincronizado
(`false`, não `null`/indisponível), a medição é descartada antes de gravar no spool — nenhum
`occurredAtUtc` computado com relógio sabidamente errado chega a ser enfileirado ou publicado. Cada
descarte incrementa `gateway.clock_unsynced_skips` (contador cumulativo, visível na telemetria e no
`diagnostics.sh`). Um valor crescente indica boots frequentes sem rede disponível a tempo — investigue
a rede do Pi nesse cenário, não o hardware do relógio (ele não tem RTC por design).

## Monitor contínuo não inicia

Execute `systemctl status fluxo-gateway-monitor --no-pager` e
`journalctl -u fluxo-gateway-monitor -b --no-pager`. O monitor depende dos scripts instalados em
`/home/junior/.node-red/fluxo-gateway`, mas não controla nem reinicia o Node-RED. O intervalo deve
ser um número inteiro de pelo menos 60 segundos.
