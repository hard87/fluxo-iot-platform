# Relatório Técnico — Piloto controlado numa VM dedicada (Proxmox)

## 1. Resumo executivo

O perfil `docker-compose.controlled-prod.yml` foi implantado pela primeira vez fora do PC de
desenvolvimento, numa VM dedicada (`fluxo-controlled-prod`, `192.168.9.30`) no hipervisor Proxmox
`atlas404.lab`. O Gateway Pi físico (`edgewarden`) foi migrado do broker de dev para esta VM, com
ingestão ponta a ponta comprovada por telemetria real (não simulada). Um gate de NTP no boot do Pi
foi implementado e validado com um **reboot físico real**, não apenas por inspeção do systemd.

A implantação expôs e corrigiu **dois bugs reais de produto** — um deles presente em dois
arquivos — que nunca haviam sido exercitados porque nenhum ambiente anterior usava TLS na conexão
administrativa do dynamic-security nem no ingestion worker. As correções estão no working tree
local e replicadas na VM; **não foram commitadas** (aguardando decisão do autor).

A fase permanece **em piloto**: a VM ainda não tem HTTPS no Portal/API (só MQTT/TLS), não há
política de retenção, e o ensaio de continuidade específico deste ambiente (24h → 60h+) ainda não
foi repetido contra a controlled-prod (só existe validação prévia contra o perfil dev, registrada
em `docs/project-status.md` seção 2.11).

> Este relatório é o resumo curado. Para o passo a passo bruto — cada erro com mensagem exata,
> diagnóstico e comando de correção usado — ver as
> [notas detalhadas](local/notas-detalhadas-producao-controlada-2026-09-22.md) (arquivo local, fora
> do controle de versão).

## 2. Estado inicial

- Data: 22 set 2026; branch `main`, HEAD `bc2a048` no início da sessão.
- Proxmox `atlas404.lab` (192.168.9.120): Intel Core2 Quad Q6600 @ 2.40GHz (2007), 4 cores, 7,7 GiB
  RAM, `local-lvm` thin com 768 GB livres. VMs pré-existentes: `sql01-lab` (102) e `debian12-server`
  (103, **não reaproveitada** — investigação revelou usuário `analista` com atividade recente,
  não era uma casca vazia como presumido inicialmente).
- Gateway Pi `edgewarden` já apontava havia dias para o broker de **dev** neste PC (`192.168.9.7`),
  validado por >60h contínuas nessa configuração (seção 2.11 do `project-status.md`).

## 3. Decisões

- VM nova (VMID 104), não reaproveitar a 103, dado o achado acima.
- Instalação Debian 12 automatizada por preseed (boot direto por kernel/initrd extraído do ISO
  netinst), não interativa.
- Backup/restore validado separadamente contra um banco descartável a partir do dump de **dev**,
  em vez de restaurar esse dump diretamente na controlled-prod — decisão explícita para não
  misturar dado de teste acumulado com o ambiente novo (ver `docs/checklist-producao-controlada.md`,
  que já pede restore em banco descartável separado, não no alvo real).
- Portal/API expostos na LAN (`0.0.0.0`), não só loopback, para produzir um link utilizável —
  reabre o pendente de TLS HTTP, tratado à parte.
- CA MQTT reaproveitada (não uma nova) especificamente para que o `ca.crt` já confiado pelo Pi
  continuasse válido sem qualquer alteração nele.

## 4. Incidente durante a instalação (documentado, não escondido)

> Sequência completa de comandos e logs: [notas detalhadas, incidentes #2–#4](local/notas-detalhadas-producao-controlada-2026-09-22.md#2).

A primeira tentativa de preseed usou boot direto por kernel/initrd com `d-i finish-install/
reboot_in_progress`. Como o processo QEMU mantém o kernel do instalador residente em memória
(`-kernel`/`-initrd` do QEMU), um reboot do sistema convidado reinicia o **mesmo** kernel do
instalador, não o sistema recém-instalado — mudar a config do Proxmox não afeta o processo QEMU já
em execução. Isso causou um loop de reinstalação (4 ciclos completos, ~22 min cada, confirmado
pelos timestamps do log de acesso HTTP que serviu o `preseed.cfg`). Uma tentativa de cortar esse
loop matando a VM no meio de um ciclo deixou o disco sem bootloader válido.

Correção definitiva: `d-i debian-installer/exit/poweroff boolean true` no preseed — o instalador
**desliga** a VM ao terminar, em vez de reiniciar; o corte para o boot normal do disco então
acontece de forma determinística (esperar `status: stopped`), sem corrida de tempo. A VM foi
destruída e recriada do zero com essa correção; a segunda instalação completou em um único ciclo.

## 5. Ambiente e provisionamento

- VM 104: 2 vCPU, 4 GiB RAM, 40 GB disco (`local-lvm`, `cache=none`/`aio=io_uring`, crash-safe),
  `onboot: 1`, IP estático `192.168.9.30/24`.
- Acesso: SSH só por chave dedicada (`id_ed25519_fluxo_controlled_prod`), usuário `junior`, sudo
  sem senha, root desabilitado, `PasswordAuthentication no`, `fail2ban` ativo. Senha local (só
  console VNC, não serve por SSH) comunicada ao autor fora deste documento.
- Docker Engine 29.8.1 + Compose v5.5.1, serviço `enabled`/`active`.
- `.env` próprio da VM, segredos gerados na hora (Postgres, JWT signing key ≥32 chars, dynsec admin,
  ingestion worker) — nada copiado do `.env` de dev.
- Certificado MQTT: mesma CA de dev, novo `server.crt`/`server.key` com SAN `192.168.9.30`,
  `localhost`, `broker.fluxo.local`. **A chave privada da CA (`ca.key`) não foi copiada para a
  VM** — só usada localmente para assinar o novo certificado, depois removida da VM.
- Conta: `admin@fluxo-piloto.local`. Workspace: **Piloto Controlado - Officina404**
  (`tenantId=officina404-piloto`). Device: **Gateway Edgewarden (Raspberry Pi)**
  (`identifier=edgewarden`, categoria `Gateway`).
- 13 métricas do gateway (`gateway.*`) com `DisplayName` legível em português — não existe endpoint
  de API para isso hoje (nasce igual à chave técnica); ajustado direto no Postgres, seguro porque a
  descoberta usa `INSERT ... ON CONFLICT DO NOTHING` e não sobrescreve.

## 6. Bugs de produto encontrados e corrigidos

> Diagnóstico completo, logs e comandos exatos de cada correção desta seção:
> [notas detalhadas, incidentes #5–#12](local/notas-detalhadas-producao-controlada-2026-09-22.md#5).

### 6.1 — `X509Certificate2.CreateFromPemFile` com overload errado (dois arquivos)

`DynamicSecurityControlClient.cs:208` (conexão admin do dynamic-security) e
`MqttTelemetryIngestionWorker.cs:344` (ingestão do worker) carregavam a CA MQTT com:

```csharp
var certificate = X509Certificate2.CreateFromPemFile(certificatePath.Trim());
```

Esse overload de um único argumento espera **certificado e chave privada no mesmo arquivo PEM**.
Um `ca.crt` puro (só certificado, sem chave) sempre falha com
`CryptographicException: The key contents do not contain a PEM, the content is malformed, or the
key does not match the certificate`.

Nunca foi pego porque:
- dev usa `MqttDynamicSecurity__BrokerPort: 1883` / `UseTls: "false"` (sem TLS nessa conexão);
- `MqttIngestion__UseTls` em dev também é `false` por padrão.

A controlled-prod foi o **primeiro ambiente real** a fixar essas duas conexões em TLS/8883 com
`TlsCaCertificatePath` apontando para um arquivo de verdade — expondo um caminho de código nunca
exercitado antes.

**Correção**, idêntica nos dois arquivos:

```csharp
var certificate = X509CertificateLoader.LoadCertificateFromFile(certificatePath.Trim());
```

`X509CertificateLoader.LoadCertificateFromFile` é a API não-obsoleta do .NET 9+/10 para carregar
um certificado sem chave privada associada. Build local limpo (0 avisos, 0 erros) e validado ao
vivo na VM (conexão TLS bem-sucedida, ingestão ponta a ponta confirmada).

**Não commitado.** Fica no working tree local (`D:\Officina404\Fluxo`) e replicado na VM.

### 6.2 — Diretório do Mosquitto sem permissão de escrita para o UID do container

`/mosquitto/config` (montado de `docker/mosquitto` no host) pertencia a `junior` (UID 1000), modo
`755`. O processo mosquitto roda como UID 1883 dentro do container. Toda escrita do plugin
dynamic-security (que grava um `.json.new` temporário antes de renomear) falhava silenciosamente
com `Permission denied` — as mudanças (roles, clients) só existiam **em memória**, nunca
persistidas em disco. Um restart do broker perdia todo o estado.

Corrigido com `chown 1883:1883` no diretório. Mesma classe de bug já havia aparecido antes nesta
sessão no `server.key` (dono `junior`, container lendo como UID 1883) — ambos são consequência do
mesmo padrão (bind mount do host com UID que não bate com o processo do container) e vale revisar
os demais volumes bind-mounted do compose por precaução.

### 6.3 — Não é bug de produto: backlog do spool com tópico antigo

Ao trocar o `environment` do Pi, ~750–900 mensagens já enfileiradas no spool local mantinham o
tópico MQTT **antigo** (computado e congelado no momento do `enqueue()`, antes da troca). O broker
aceitava a publicação (PUBACK, RC:0) mas descartava silenciamente por a ACL da credencial nova só
permitir o tópico novo — MQTT não recusa isso de forma visível no protocolo padrão. A fila drenou
sozinha (falso positivo de sucesso do lado do gateway) sem nenhuma dessas mensagens chegar a algum
lugar. Comportamento esperado dado que essas mensagens não seriam entregáveis a nenhum dos dois
ambientes de qualquer forma; não requer correção de código, só ciência operacional ao trocar de
ambiente com backlog pendente.

## 7. Testes executados

| Teste | Resultado |
|---|---|
| Preseed automatizado (2ª tentativa, com poweroff-on-finish) | 1 ciclo, sem loop |
| `cache=none` do disco da VM | confirmado via `qm showcmd` |
| Docker Engine + Compose instalados | `enabled`/`active` |
| Build da stack controlled-prod | 3 imagens, sucesso |
| `docker compose up -d` (5 serviços) | todos `healthy` após correção do mosquitto |
| Migrations EF Core (10) | aplicadas via container SDK temporário na rede do compose |
| Registro de conta, workspace, device | HTTP 200/201, nomes legíveis |
| Publicação MQTT/TLS deste PC (payload real) | aceita, ingerida, consultável via API |
| `LoadTrustChain` — CA pura (bug 6.1) | reproduzido, corrigido, revalidado |
| Permissão do diretório dynsec (bug 6.2) | reproduzido, corrigido, revalidado |
| Rotação de credencial do device | funcional após 6.1/6.2 corrigidos |
| Corte do Pi para a VM nova (env + restart) | conectividade restaurada após ajustes |
| `deploy-flow.js` (credential store) | falhou 1x por secret divergente; recriado do zero |
| Ingestão real do Pi físico (não simulada) | confirmada — `uptime_sec`/timestamps batendo, intervalos de 30s |
| Gate de NTP — restart normal (já sincronizado) | 0,29s, sem espera |
| Gate de NTP — **reboot físico real** do Pi | capturado `activating` com `NTPSynchronized=no`; sincronizou ~21s após boot; nodered subiu em seguida, sem timeout de fallback |
| Gateway pós-reboot | fila em 0, sequence contínua, sem perda |

## 8. Recursos observados

- VM: 2 vCPU/4 GiB alocados, node Proxmox com 4 cores físicos totais (compartilhados com VMs 102 e
  103, hoje paradas).
- Pi pós-reboot: uptime 61s no momento da checagem, CPU 50,5 °C, fila 0, `sequence` 23046.
- Postgres controlled-prod: schema completo (10 migrations), telemetria real chegando a cada 30s.

## 9. Segurança

- Chaves SSH dedicadas por sistema (`id_ed25519_proxmox_atlas404`,
  `id_ed25519_fluxo_controlled_prod`), não reaproveitando a chave padrão de uso geral.
- `ca.key` nunca residiu na VM; só o `ca.crt` público e o `server.key`/`server.crt` novos.
- Segredos (senha de banco, JWT signing key, credenciais dynsec, senha da conta admin) gerados
  novos, nunca copiados de dev; comunicados ao autor fora deste documento, não versionados.
- `1883` não publicada pela controlled-prod (só `8883` TLS), conforme design.
- Portal/API expostos na LAN em HTTP puro (não TLS) — risco aceito temporariamente, mesma postura
  já usada em dev; pendência de TLS HTTP permanece registrada separadamente.

## 10. Falhas e divergências reais (cronologia honesta)

1. Presumi inicialmente que a VM 103 era uma casca vazia preparada para isto — estava errado (ver
   seção 2); parei antes de tocar nela e confirmei com o autor.
2. Loop de reinstalação por escolha de design equivocada (`-kernel`/`-initrd` + reboot em vez de
   poweroff) — ver seção 4.
3. Interrompi a VM no meio de um ciclo de instalação tentando cortar o loop, deixando o disco sem
   bootloader; a VM foi recriada do zero em vez de tentar recuperar o disco.
4. Instalação estática do preseed não aplicou o IP fixo (o instalador usa DHCP para buscar o
   próprio `preseed.cfg` via `url=`, e os valores estáticos só dentro do arquivo chegam tarde
   demais para substituir esse netcfg inicial) — corrigido editando `/etc/network/interfaces`
   diretamente no sistema já instalado, sem reinstalar.
5. Dois bugs reais de produto (seção 6.1, 6.2), nunca antes exercitados.
6. `deploy-flow.js` falhou decriptando `flows_cred.json` existente (chave de segredo divergente,
   causa exata não investigada a fundo) — contornado removendo o arquivo e deixando recriar do
   zero.
7. Diversos problemas de *quoting* PowerShell → SSH → bash → `docker exec` → heredoc durante a
   sessão (não são bugs do projeto, atrito de ferramenta; resolvidos migrando para scripts
   enviados por arquivo em vez de comandos inline).

## 10.1 — Reforço pós-reboot: descarte de medição com relógio não sincronizado

> Trecho de código completo e comandos de validação:
> [notas detalhadas, incidente #15](local/notas-detalhadas-producao-controlada-2026-09-22.md#15).

O reboot real (seção 7) confirmou que o gate de boot funciona, mas também confirmou que ele tem um
caminho de fallback (timeout de 120s) que sobe o `nodered.service` mesmo sem sync confirmado — o
que, isoladamente, ainda permitiria publicar telemetria com `occurredAtUtc` computado a partir de
um relógio sabidamente errado nesse cenário degradado. Corrigido com uma segunda camada de defesa,
diretamente no `gateway-spool.js` (`devices/pi-gateway-reference-node/scripts/gateway-spool.js`,
função `enqueue()`): a cada publicação — não só no boot — o script verifica
`timedatectl show -p NTPSynchronized --value` e, se o SO afirma explicitamente `false` (não
`null`/indisponível), descarta a medição antes de gravar no spool, incrementando o novo contador
`gateway.clock_unsynced_skips` em vez de enfileirar dado com timestamp não confiável.

Sintaxe validada localmente e no Pi (`node --check`), implantado ao vivo (subprocesso novo por
chamada, não requer restart do Node-RED), confirmado sem regressão (`depth:0`, sequence
continuando a subir normalmente, `clockUnsyncedSkips:0` no estado atual, já sincronizado). O
override de systemd (seção 6) foi salvo no repositório em
`devices/pi-gateway-reference-node/systemd/nodered.service.d/ntp-gate.conf`, que ainda não existia
versionado — só estava instalado ao vivo no Pi.

## 11. Limitações e próximos passos

- **Commitar (ou não) as correções da seção 6.1** — decisão do autor; ainda só no working tree
  local e na VM.
- TLS HTTP para Portal/API na controlled-prod — pendência separada, não iniciada.
- Repetir a validação de continuidade (24h → 60h+) especificamente contra a controlled-prod (só
  existe validação prévia contra o perfil dev).
- Executar e documentar a segunda validação de backup/restore, agora contra o dado real da
  controlled-prod (a primeira, contra dev → banco descartável, ainda está pendente de execução).
- Revisar os demais volumes bind-mounted do compose quanto a UID/permissão, dado o padrão
  encontrado na seção 6.2.
- Investigar a causa raiz exata da falha de decriptação do `flows_cred.json` (item 6 da seção 10),
  hoje só contornada.
- Testar o caminho de descarte por relógio não sincronizado (seção 10.1) de ponta a ponta — hoje só
  a sintaxe e o funcionamento normal (já sincronizado) foram validados ao vivo; o caminho
  `timeSynchronized === false` não foi forçado/observado diretamente.
- Retenção de telemetria/rejeições permanece indefinida, como já registrado em
  `docs/project-status.md`.

## 12. Gate da fase

O perfil controlled-prod está implantado e comprovado ponta a ponta com o Gateway Pi físico,
incluindo um teste real de reboot para o gate de NTP. Infra Fase 2 (piloto controlado) permanece
**EM PILOTO** — não é declarada concluída enquanto TLS HTTP, a segunda validação de backup/restore
e o ensaio de continuidade específico deste ambiente não fecharem.
