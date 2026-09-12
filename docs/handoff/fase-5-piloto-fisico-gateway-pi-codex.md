# Handoff — Fase 5 (pilotos físicos): Fluxo Gateway Reference Device no Raspberry Pi (edgewarden)

> Status histórico: proposto, não iniciado.
> Este documento cobre **investigação + decisão arquitetural + implementação**. A nova sessão
> pode (e deve) começar propondo um ADR antes de codar — não existe ADR aprovado para este
> escopo ainda.

Cole este documento inteiro como prompt. Não resuma, não pule seções.

## A. Contexto verificado (confirme antes de editar, não assuma)

- Repo do Fluxo: `D:\Officina404\Fluxo`. Confirme branch/HEAD/working tree com `git status` antes
  de começar — o `project-status.md` mais recente registrava working tree suja com Fases 1 e 2
  ainda não commitadas; pode ter mudado.
- Leia nesta ordem antes de tocar em qualquer arquivo:
  1. `docs/project-status.md` — estado atual, trilhas, riscos ativos.
  2. `docs/piloto-real-controlado.md` — fluxo de provisionamento de device (login, workspace,
     `POST /api/workspaces/{workspaceId}/devices/provision`, credencial MQTT automática via
     plugin `dynamic-security` do Mosquitto).
  3. `docs/mqtt-tls-e-credenciais.md` — mecanismo de credencial por device e TLS.
  4. `src/Fluxo.Domain/Enums/DeviceCategory.cs` — **fato central deste handoff**:
     ```csharp
     public enum DeviceCategory
     {
         Sensor = 1,
         Actuator = 2,
         Gateway = 3,
         Camera = 4,
         Meter = 5,
         Other = 99
     }
     ```
     Só `Sensor` foi exercitado até hoje (pelo `devices/esp32-reference-node`). `Gateway` e
     `Camera` existem no domínio desde o início e **nunca foram implementados por nenhum device
     real**. Confirme isso com `git log -- src/Fluxo.Domain/Enums/DeviceCategory.cs` antes de
     assumir que continua assim.
  5. `devices/esp32-reference-node/README.md` e `devices/esp32-reference-node/main/*.c/.h` —
     padrão de referência a espelhar (não reinventar):
     - Tópico MQTT publicado, formato verificado:
       `fluxo/tenants/<tenantId>/workspaces/<workspaceId>/devices/<deviceIdentifier>/telemetry`
     - Payload verificado (de `piloto-real-controlado.md`, seção 10):
       ```json
       {
         "schemaVersion": "1.0",
         "tenantId": "acme-industria",
         "workspaceId": "<uuid>",
         "deviceId": "<deviceIdentifier>",
         "messageType": "telemetry",
         "timestampUtc": "<ISO8601>",
         "sequence": 1,
         "firmwareVersion": "esp32-reference",
         "metrics": { "temperature": 24.5, "humidity": 60.2, "battery": 3.92, "rssi": -55, "uptimeSec": 120 }
       }
       ```
     - `sequence` incremental com checkpoint persistido (o ESP32 usa NVS; no Pi, usar arquivo em
       disco) — a ingestão do Fluxo rejeita duplicidade por
       `(TenantId, WorkspaceId, DeviceId, Sequence)`, confirme em
       `Fluxo.Infrastructure`/tabela `telemetry_ingestion_records`.
     - TLS obrigatório (`mqtts://`), sem fallback para `mqtt://` sem criptografia, CA local gerada
       por `docker/mosquitto/scripts/generate-local-certs.ps1`.
     - Segredos nunca versionados: padrão `app_config.local.h` (ignorado no `.gitignore`) — no
       device Pi, usar equivalente (variáveis de ambiente fora do Git, ver seção E).
- Categorias `Sensor` e `Gateway` no request de provisionamento (`ProvisionDeviceRequest`) usam
  `category` como inteiro (`1` = Sensor, confirmado no guia). **Não assuma que `3` (Gateway) já
  foi testado ponta a ponta** — pode haver validação implícita em
  `Fluxo.Application/UseCases/Provisioning/ProvisionDeviceUseCase.cs` que nunca rodou para essa
  categoria. Teste isso cedo, não no fim.
- Schema de telemetria é V2 (ADR-0001, `docs/adr/0001-telemetry-schema-v2.md`) com
  `MetricDefinition` tipado por workspace (`Numeric`, `Boolean`, `Text`). **Leia o ADR-0001 antes
  de decidir como representar múltiplos sensores físicos atrás de um único device Gateway** — não
  está claro ainda (é decisão desta fase) se cada sensor filho vira um `MetricDefinition` com
  prefixo (`sensor01.temperature`, `sensor02.temperature`) sob o mesmo `deviceId` do Gateway, ou
  se cada sensor filho deveria ser seu próprio `Device` (categoria `Sensor`) com o Gateway apenas
  fazendo bridge de rede — **essa é a decisão arquitetural principal desta fase, não invente uma
  resposta sem registrar em ADR novo.**

## B. Objetivo desta fase

Implementar o **primeiro device físico real de categoria `Gateway`** do Fluxo, usando um
Raspberry Pi 3B+ já em produção controlada, hostname `edgewarden`, rodando Node-RED.

Por que Gateway e não Sensor: um Raspberry Pi só para ler um sensor e publicar MQTT duplica o que
o `esp32-reference-node` já faz com hardware mais barato e menor consumo — não avança nada de
novo no roadmap. As capacidades que só um Linux completo tem (agregação de múltiplos sensores por
serial/Wi-Fi local, buffer em disco para resiliência offline, TLS/credencial única para N
sensores atrás dele) mapeiam exatamente para a categoria `Gateway`, que está no domínio do Fluxo
desde o início e nunca foi implementada. Isso avança de verdade:
- `docs/project-status.md` seção 6, pendência "executar firmware ESP32 por 24h" → estende para
  "executar Gateway real por 24h".
- Trilha "Produto — Fase 5 — pilotos físicos" (hoje `NÃO INICIADA`).
- Trilha "Infraestrutura — Infra Fase 2 — piloto controlado" (hoje `PRÓXIMA`).

Escopo secundário opcional (não bloqueante, fazer só se sobrar tempo): categoria `Camera`,
aproveitando `rpicam-apps-lite` já instalado no Pi, publicando eventos discretos de
presença/movimento (não vídeo bruto) — outra categoria nunca implementada.

## C. Estado verificado do dispositivo físico (edgewarden)

Levantado via SSH em 2026-07-31, pode estar desatualizado — reverifique o que for crítico:

- Hardware: Raspberry Pi 3B+, Debian 13 (trixie), kernel 6.18 aarch64.
- **RAM total: 905 MiB** — apertado. Não assuma que dá para rodar o stack Docker completo do
  Fluxo (Postgres + API + Worker + Mosquitto + Portal) no próprio Pi. Plano recomendado: broker e
  backend do Fluxo continuam rodando no PC de desenvolvimento (`docker compose up -d`); o Pi só
  roda o cliente Node-RED do Gateway, conectando via rede local ao broker MQTT do PC (ou ao
  broker de produção controlada, se já existir um acessível). Valide a hipótese de RAM antes de
  desenhar diferente.
- Acesso: SSH por chave apenas (`ssh edgewarden`, ver `~/.ssh/config` do usuário se existir, senão
  resolve por hostname direto), `PasswordAuthentication no`, `PermitRootLogin no`, `fail2ban`
  ativo no jail `sshd`. Usuário de trabalho: `junior`, com `sudo`.
- Firewall (`ufw` + `iptables`): entrada limitada a `22/tcp` (SSH, todo mundo — considerar depois
  se restringe a LAN) e `1880/tcp` (Node-RED, só `192.168.9.0/24`). **Saída não é restrita**
  (`ufw` default allow outgoing) — uma conexão MQTT TLS de saída do Pi para o broker do Fluxo
  **não precisa de nenhuma regra nova de firewall no Pi**. Não abra portas de entrada extras sem
  necessidade real.
- Node-RED: rodando como serviço systemd (`nodered.service`), usuário `junior`, habilitado no
  boot. Diretório de trabalho: `/home/junior`. Projeto em `~/.node-red/`.
- Nodes já instalados em `~/.node-red/package.json` (reaproveitar, não reinstalar):
  `node-red-contrib-buffer-parser`, `node-red-node-pi-gpio`, `node-red-node-ping`,
  `node-red-node-play-audio`, `node-red-node-random`, `node-red-node-serialport`,
  `node-red-node-smooth`. **Falta o node MQTT com suporte a TLS mútuo/CA customizada** — o
  `node-red-node-serialport` e o `buffer-parser` sugerem que a intenção original era falar com
  sensores por serial (ESP32 do Kit Maker via USB, por exemplo); o node MQTT nativo do Node-RED
  (`node-red/mqtt-broker`) já suporta TLS com CA customizada nas opções de conexão — confirme se
  cobre o caso antes de instalar um node MQTT extra.
- Bibliotecas Python de sistema já instaladas (para o escopo Camera opcional):
  `python3-gpiozero`, `python3-rpi-lgpio`, `python3-spidev`, `python3-smbus2`, `rpicam-apps-lite`.
- `nodered.service` já referencia `EnvironmentFile=-/home/junior/.node-red/environment` (opcional,
  arquivo não precisa existir hoje) — **use esse arquivo para injetar credenciais MQTT sem
  hardcodar no flow nem versionar no Git**, seguindo a mesma disciplina de segredo do
  `esp32-reference-node` (`app_config.local.h` fora do Git).
- **Achado de segurança à parte, sem relação com este escopo**: existe um arquivo
  `D:\Officina404\sbc.txt` com duas senhas em texto puro (aparentam ser credenciais antigas do
  próprio Pi). Não é bloqueante para esta fase, mas sinalize ao usuário se ainda não foi limpo —
  não copie o conteúdo desse arquivo para nenhum lugar novo.

## D. Escopo da entrega

1. **ADR-0004** (`docs/adr/0004-<slug>.md`, seguir o formato dos ADRs 0001–0003 existentes)
   decidindo:
   - Como sensores físicos atrás do Gateway são representados no schema de telemetria (opção
     "sub-métricas com prefixo sob um único `Device` Gateway" vs. "cada sensor filho é seu próprio
     `Device` Sensor, Gateway é só transporte de rede" — investigar `ProvisionDeviceUseCase` e
     `MetricDefinition` antes de decidir, ver seção A).
   - Formato de tópico MQTT do Gateway (provavelmente reaproveitar
     `fluxo/tenants/<tenantId>/workspaces/<workspaceId>/devices/<deviceIdentifier>/telemetry` com
     `deviceIdentifier` = identificador do Gateway).
   - Estratégia de buffer/store-and-forward quando a conexão com o broker cair (fila em disco no
     Pi, replay com `sequence` contínuo ao reconectar).
2. **`devices/pi-gateway-reference-node/`** (pasta irmã de `devices/esp32-reference-node/`),
   contendo:
   - `README.md` no mesmo estilo do README do `esp32-reference-node` (o que faz, configuração,
     segurança, como rodar).
   - Export do flow Node-RED (`flow.json`) implementando: conexão MQTT TLS ao broker do Fluxo,
     leitura de sensor(es) físico(s) disponível(is) (serial e/ou GPIO conforme o que estiver
     fisicamente conectado ao Pi no momento — perguntar ao usuário o que há disponível fisicamente
     antes de assumir hardware específico), publicação periódica no tópico decidido no ADR-0004,
     buffer local em caso de desconexão.
   - Instruções de deploy no `edgewarden` via SSH (import do flow, configuração do
     `~/.node-red/environment` com as credenciais retornadas pelo provisionamento).
3. **Provisionamento real**: seguir `docs/piloto-real-controlado.md` seções 5–8, mas com
   `category = 3` (Gateway) em vez de `1` (Sensor). Documentar o resultado (funcionou de primeira?
   precisou de ajuste no backend para aceitar Gateway? registrar no relatório de entrega).
4. **Relatório de entrega** em `docs/handoff/relatorio-fase-5-piloto-fisico-gateway-pi-<data>.md`,
   seguindo o padrão dos relatórios de Fase 1/Fase 2 já existentes na mesma pasta (resultado real,
   divergências do plano, evidência).
5. Atualizar `docs/project-status.md` (trilha "Produto — Fase 5" e "Infraestrutura — Infra Fase
   2") refletindo o novo estado.

## E. Segurança e convenções a seguir (já estabelecidas no projeto, não reabrir a discussão)

- Nunca versionar segredo real (credencial MQTT do Gateway, senha, chave) — nem no repo Fluxo nem
  em nenhum arquivo novo. Usar `~/.node-red/environment` no Pi (fora do Git) e `.env`/
  `app_config.local.h`-equivalente no padrão já usado.
- TLS obrigatório na conexão MQTT do Gateway (`mqtts://`, porta 8883), sem fallback sem
  criptografia — mesmo padrão do `esp32-reference-node`.
- Não expor porta nova de entrada no `ufw`/`iptables` do Pi para este escopo — a conexão do
  Gateway ao broker é de saída.
- O Pi (`edgewarden`) já está hardenizado (SSH só por chave, fail2ban ativo) — não reduza isso
  para "facilitar" o deploy (ex.: não reative `PasswordAuthentication` para copiar arquivos; use
  `scp`/`ssh` com a chave já configurada).

## F. Perguntas em aberto que a nova sessão deve resolver antes de codar (não adivinhar)

1. ~~Que sensor físico está de fato disponível~~ — **RESOLVIDO em 2026-08-01**: há um sensor
   **Texas Instruments HDC1080** (temperatura + umidade) conectado ao `edgewarden` via I2C nativo
   do GPIO (`/dev/i2c-1`), endereço `0x40`. Confirmado por leitura de `Manufacturer ID` (registrador
   `0xFE` → `0x5449` = "TI") e `Device ID` (registrador `0xFF` → `0x1050`, valor exato do HDC1080).
   Leitura funcional validada: temperatura e umidade lidas com sucesso via protocolo de ponteiro de
   registrador (não confundir com o protocolo de comando do SHT2x/HTU21D/Si70xx — são famílias
   diferentes mesmo estando no mesmo endereço `0x40`).
   - Protocolo de leitura: escrever 1 byte com o endereço do registrador (`0x00` = temperatura,
     `0x01` = umidade, sem byte de dado depois — isso já dispara a conversão), aguardar ao menos
     ~6.5ms (usar 20ms de margem), depois ler 2 bytes (MSB primeiro).
   - Conversão: `temp_c = (raw / 65536.0) * 165.0 - 40.0`; `umidade_rh = (raw / 65536.0) * 100.0`.
   - Alimentação: módulo com os dois jumpers de pull-up em 3.3V (compatível com os GPIOs do Pi,
     não 5V-tolerantes) — confirmado fisicamente pelo usuário.
   - Isso define o primeiro sensor real do Gateway: o flow Node-RED deve usar o node `rpi-gpio`
     (ou um `exec`/`function` node chamando `smbus2` via Python, já que não há um node Node-RED
     nativo testado para HDC1080 neste projeto ainda) lendo `/dev/i2c-1` endereço `0x40`.
2. O broker MQTT de destino é o ambiente de dev local (`docker-compose.yml`, no PC) ou já existe
   um ambiente de produção controlada (`docker-compose.controlled-prod.yml`) acessível pela rede
   `192.168.9.0/24` onde o Pi está?
3. `category = 3` (Gateway) no endpoint de provisionamento aceita sem erro? Se o backend validar
   contra uma lista implícita de categorias suportadas, isso pode exigir uma mudança de backend
   antes do device conseguir se provisionar — não é assumido que "só passar o número" funciona.

Comece confirmando essas três perguntas com o usuário (ou testando diretamente) antes de gerar
código ou o ADR final.
