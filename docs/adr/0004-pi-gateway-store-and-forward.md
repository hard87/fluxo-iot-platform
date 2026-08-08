# ADR-0004 — Gateway Raspberry Pi com store-and-forward persistente

## Status

Aceito em 31 jul 2026 para o primeiro piloto físico de `DeviceCategory.Gateway`.

## Contexto

O Raspberry Pi 3B+ `edgewarden` executa Debian 13 e Node-RED 5.0.1, com aproximadamente 905 MiB
de RAM. Ele deve atuar como ponte Linux e não hospedar o stack Fluxo. A plataforma já fornece
credencial MQTT individual por device, ACL restrita ao tópico exato, Telemetry Schema V2 e
idempotência por `(TenantId, WorkspaceId, DeviceId, Sequence)`.

O provisionamento real de 31 jul 2026 confirmou HTTP 201 para `category: 3`, persistência da
categoria Gateway, criação da credencial e ACL no tópico canônico. Não foi encontrado sensor USB,
serial ou I2C conectado; a primeira carga útil usa diagnóstico real do Pi.

## Decisão

### Representação dos sensores

O piloto representa o Pi como um único Device Gateway e sensores locais como submétricas, por
exemplo `sensor01.temperature_c`. Métricas internas usam o prefixo `gateway.`. Essa escolha não é
uma afirmação de que todo sensor futuro deva perder identidade: ela decorre da autorização atual,
em que uma credencial tem `publishClientSend` somente para o tópico exato do próprio device.

Sensors como Devices independentes exigiriam múltiplas credenciais no Pi ou uma nova delegação
“publish on behalf of”, ampliando provisionamento e superfície de segurança sem hardware real que
justifique a mudança. Quando um sensor precisar de ciclo de vida, auditoria ou histórico
independentes, essa decisão deve ser reaberta e a delegação modelada explicitamente.

### Contrato e tópico

O Gateway publica Schema V2:

```json
{
  "schemaVersion": 2,
  "sequence": 1,
  "occurredAtUtc": "2026-07-31T12:00:00.000Z",
  "metrics": { "gateway.uptime_sec": 120 }
}
```

O tópico não muda:

```text
fluxo/tenants/<tenantId>/workspaces/<workspaceId>/devices/<deviceIdentifier>/telemetry
```

Chaves seguem o guardrail do ADR-0001 e são descobertas como `MetricDefinition` no workspace.

### Entrega e confirmação

MQTT usa TLS obrigatório na porta 8883, QoS 1 e sem retain. A semântica é at least once. Toda
mensagem é persistida antes da tentativa de envio. O node MQTT nativo chama `done` somente no
callback de `client.publish`; para QoS 1 isso ocorre após PUBACK. Um node `complete` remove o
arquivo somente depois desse callback.

Se o broker confirmar e o Pi falhar antes da remoção local, o arquivo será reenviado com o mesmo
sequence. O índice idempotente do backend descarta a repetição. Replay é serial, pelo menor
sequence, com intervalo de 250 ms; falhas usam backoff de 5 s.

### Sequence persistente

O high-water mark fica em `state/sequence.json` e `state/sequence.backup.json`, modo privado. Um
novo valor é reservado e gravado nos dois checkpoints antes de criar o arquivo da mensagem; uma
falha pode criar lacuna, mas nunca deve reutilizar sequence. Escritas usam arquivo temporário no
mesmo diretório, `fsync`, `rename` e `fsync` do diretório.

Na inicialização, usa-se o maior valor válido entre os checkpoints e nomes dos arquivos do spool.
Se ambos os checkpoints existentes estiverem corrompidos e não houver high-water mark no spool, o
runtime falha fechado e exige recuperação a partir do backend. Retroceder silenciosamente é
proibido.

### Store-and-forward

Cada mensagem ocupa um arquivo JSON em `state/spool`, nomeado pelo sequence com 20 dígitos. O
registro contém tópico, payload, instante e metadados de fila, nunca credenciais. Um lock exclusivo
curto serializa operações e locks com mais de 30 s são recuperados como stale após log.

Limites iniciais: 10.000 mensagens, 50 MiB e 7 dias. Ao atingir quantidade ou bytes, descarta-se a
mensagem nova, preservando a ordem já aceita no spool, e incrementa-se `droppedMessages` com log.
Mensagens expiradas são descartadas da mais antiga para a mais nova. Esses valores acomodam cerca
de 3,5 dias a 30 s no limite de quantidade; 50 MiB protege o cartão SD contra payloads maiores.

## Consequências

Benefícios: uma credencial e ACL mínimas; operação offline após restart/reboot; ordem explícita;
recuperação auditável; sem banco ou pacote Node-RED adicional; duplicatas compatíveis com a
idempotência existente.

Custos e riscos: arquivos e `fsync` aumentam escrita no cartão SD; o replay é single-thread e não
é desenhado para grande throughput; descarte por idade/limite causa perda explícita; submétricas
acoplam sensores ao Gateway; uma intervenção manual incorreta nos dois checkpoints ainda exige
consulta ao backend. SQLite ou delegação para devices filhos devem ser reconsiderados se volume,
quantidade de sensores ou requisitos de identidade crescerem.

O flow não abre portas, não enfraquece SSH/UFW e não contém segredos. O deploy preserva o
`adminAuth` e injeta a credencial no credential store criptografado a partir do EnvironmentFile
protegido, durante uma parada breve do serviço.
