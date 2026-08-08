# Relatório Técnico — Fase 5: piloto físico Gateway Raspberry Pi

## 1. Resumo executivo

O Raspberry Pi 3B+ `edgewarden` foi implementado e validado como o primeiro device físico de
categoria `Gateway` do Fluxo. O Node-RED publica diagnóstico real do Linux pelo Telemetry Schema
V2, MQTT/TLS e QoS 1, com sequence e spool persistentes. Provisionamento, ACL negativa,
ingestão, desconexão, restart do Node-RED, reboot do Pi e replay foram exercitados fisicamente.

A fase permanece **em piloto**, não concluída: não havia sensor físico conectado e o ensaio de 24
horas foi preparado, mas não executado até o fim nesta sessão.

## 2. Estado inicial

- Data: 31 jul 2026; branch `snapshot-auth-portal-mvp-20260526`.
- HEAD: `aeeaf78818faf52224a69b5eee396f7489e1543e` (`aeeaf78`).
- Working tree já continha DOCX, `artifacts/` e o handoff da Fase 5 não rastreados; preservados.
- `edgewarden`: Debian 13, kernel 6.18 aarch64, Node-RED 5.0.1, Node.js 22.23.2 e 905 MiB RAM.
- SSH por chave, `PasswordAuthentication no`, `PermitRootLogin no`, fail2ban ativo e UFW deny
  incoming com somente 22/tcp permitido.
- O EnvironmentFile existia com modo 644 e sem variáveis Fluxo; foi corrigido para 600.
- Nenhum USB serial ou I2C foi identificado. GPIO existe, sem evidência de sensor conectado.

## 3. Decisões

O [ADR-0004](../adr/0004-pi-gateway-store-and-forward.md) escolheu um único Device Gateway com
sensores representados por submétricas no piloto. A ACL atual vincula uma credencial ao tópico
exato de um device; devices filhos independentes exigiriam múltiplas credenciais ou delegação
explícita ainda inexistente.

O runtime usa Schema V2, tópico canônico, QoS 1, at least once, spool por arquivo, replay serial,
remoção após o callback/PUBACK e idempotência do backend para possível repetição.

## 4. Arquivos entregues

- `docs/adr/0004-pi-gateway-store-and-forward.md`;
- `devices/pi-gateway-reference-node/README.md`;
- `devices/pi-gateway-reference-node/environment.example`;
- `devices/pi-gateway-reference-node/flow.json`;
- scripts de deploy, validação, spool, diagnóstico, testes MQTT e monitoramento de 24h;
- `devices/pi-gateway-reference-node/docs/troubleshooting.md`;
- este relatório e atualização de `docs/project-status.md`.

Nenhum pacote Node-RED, migration, porta de entrada ou alteração de domínio foi necessário.

## 5. Ambiente e provisionamento

Foi usado o stack local do PC em `192.168.9.7`, via `docker-compose.yml`. MQTT plaintext 1883
permaneceu em loopback e foi comprovadamente inacessível pelo Pi; MQTT/TLS 8883 e a API 5000
foram expostos à LAN durante o piloto. O certificado de servidor foi reemitido, preservando a CA,
com SAN `broker.fluxo.local` e `192.168.9.7`.

Request sanitizado:

```json
{
  "name": "Edgewarden Gateway",
  "identifier": "edgewarden",
  "category": 3,
  "metadataJson": "{\"runtime\":\"node-red\",\"hardware\":\"raspberry-pi-3b-plus\"}"
}
```

Resultado principal:

- endpoint: `POST /api/workspaces/66451978-ec5f-4c21-91b6-48bdde621eca/devices/provision`;
- HTTP 201;
- device `9349f958-2805-41f8-a323-f08c7a161c1b` / identifier `edgewarden`;
- tenant `gateway-pilot-91f6a6e1be`;
- categoria persistida: `Gateway` (`3`);
- credencial individual e role ACL criadas;
- tópico: `fluxo/tenants/gateway-pilot-91f6a6e1be/workspaces/66451978-ec5f-4c21-91b6-48bdde621eca/devices/edgewarden/telemetry`.

Segredos foram transferidos diretamente ao EnvironmentFile e ao credential store criptografado;
não aparecem neste relatório, logs ou Git.

## 6. Telemetria e ingestão

O backend descobriu nove métricas:

- Numeric: CPU, disco, carga, memória, uptime, profundidade da fila, replays e descartes;
- Boolean: estado MQTT.

Snapshot após a validação: 15 mensagens aceitas, sequences 13–27, `LastTelemetrySequence=27` e
zero grupo duplicado. A fila estava vazia. Os arquivos de sequence e spool não contêm
credenciais.

## 7. Testes executados

| Teste | Resultado |
|---|---|
| Provisionamento com `category: 3` | HTTP 201; categoria persistida |
| MQTT com CA correta e credencial individual | conectado em TLS 1.3; publicação aceita |
| CA inválida | rejeitada |
| Credencial inválida | rejeitada |
| Publicação em tópico fora da ACL | MQTT v5 reason 135 (`Not authorized`) |
| Acesso plaintext 1883 a partir do Pi | bloqueado/inacessível |
| Ingestão Schema V2 | aceita; 9 MetricDefinition criadas |
| Restart do Node-RED offline | fila persistiu: 0/seq 18 → 4/seq 22 |
| Reboot do Pi offline | Node-RED voltou ativo; fila 6/seq 24 |
| Reconexão e replay | fila 6 → 0; backend aceitou em ordem até seq 25 |
| Duplicatas persistidas | zero grupos; índice idempotente confirmado |
| Testes .NET | 71 unitários + 40 integração, todos verdes |
| Build do portal | verde; warning conhecido de bundle >500 kB |
| `npm audit --omit=dev` | 2 vulnerabilidades moderadas em React Router; correção automática exige major/breaking change |
| Sintaxe/artefatos Gateway | shell, JavaScript e JSON válidos |
| Compose dev e controlled-prod | configurações válidas |
| Teste de 24 horas | não executado até o fim; monitor preparado |

## 8. Recursos observados

Após reboot e com Gateway ativo:

- RSS Node-RED: 131.492 KiB (14,1% da RAM);
- RAM do sistema: 257 MiB usados, 647 MiB disponíveis; swap 0;
- disco: 3,6 GiB de 29 GiB (14%);
- temperatura: 48,3 °C;
- carga: 0,48 / 0,51 / 0,22 no snapshot;
- fila: 0 após replay; nenhum descarte.

## 9. Falhas e divergências reais

1. A primeira transferência do EnvironmentFile removeu caracteres `r` literais ao tentar
   normalizar CRLF. A credencial não foi exposta, mas tornou-se irrecuperável. Esse primeiro
   device (`e67038d8-5879-42ef-83d1-e7c150196751`) ficou sem uso e sua credencial deve ser
   revogada/desativada por operação autorizada futura. O Gateway principal foi reprovisionado e o
   transporte passou a usar base64 + validação antes do restart.
2. As imagens API/Worker inicialmente em execução eram anteriores ao HEAD e rejeitaram 12
   mensagens V2 (`schemaVersion` numérico) como `PayloadInvalid`. Após rebuild no HEAD, a ingestão
   passou. Sequences 1–12 ficaram como lacunas intencionais e nunca foram reutilizados.
3. O Node-RED possui `adminAuth`; a API local retornou 401. O deploy foi ajustado para uma parada
   breve e atualização offline do flow/credential store criptografado, preservando o hardening.
4. A regra histórica UFW para 1880 não existia. Nenhuma porta foi aberta; o deploy não depende de
   acesso remoto à UI.
5. O perfil usado foi o compose local, não `docker-compose.controlled-prod.yml`. A exposição foi
   limitada a API e MQTT/TLS para a LAN; 1883 continuou loopback.
6. `npm audit --omit=dev` passou a reportar duas vulnerabilidades moderadas no React Router. A
   correção sugerida migra para `react-router-dom` 7.x e é breaking; não foi absorvida nesta fase
   de Gateway.

## 10. Segurança

- EnvironmentFile 600, flows e credential store 600, state/spool 700.
- CA pública em 664; nenhuma chave privada copiada para o Pi.
- SSH e UFW preservados; nenhuma regra de entrada adicionada.
- Sem fallback MQTT plaintext e sem credencial hardcoded no flow/repositório.
- `D:\Officina404\sbc.txt` não foi aberto nem usado; permanece risco externo a tratar.

## 11. Limitações e próximos passos

- Executar e revisar o ensaio completo de 24 horas com `scripts/monitor-24h.sh`.
- Revogar/desativar o primeiro device/credencial sem uso.
- Integrar um sensor físico real quando identificado e cadastrar/revisar suas métricas.
- Repetir o piloto no perfil controlled-prod e documentar encerramento/backup/restore.
- Avaliar desgaste do cartão SD e SQLite somente se volume real superar o spool por arquivos.
- Reabrir o ADR se sensores precisarem de identidade e ciclo de vida independentes.

## 12. Gate da fase

O Gateway mínimo está implementado e comprovado fisicamente. Produto Fase 5 e Infra Fase 2
avançam para **EM PILOTO**. A fase não recebe status concluído enquanto o teste de 24 horas e as
pendências operacionais acima não forem fechados.
