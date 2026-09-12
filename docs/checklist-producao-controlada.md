# Checklist de Produção Controlada

Use este checklist antes de expor um piloto com até 100 devices simultâneos. Um item permanece
aberto quando não há evidência executada; validação de Compose não prova operação ou TLS.

## Rede e TLS

- [ ] TLS HTTP ativo no proxy/terminador da API e do Portal.
- [ ] MQTT TLS ativo em `8883` no ambiente do piloto.
- [ ] Porta `1883` não exposta publicamente.
- [ ] Certificado MQTT válido para o host usado pelos devices.
- [ ] HSTS habilitado somente quando HTTPS estiver ativo.

## Identidade e acesso

- [ ] JWT configurado com issuer, audience e signing key forte no ambiente do piloto.
- [ ] `.env` real não versionado.
- [ ] Sem tokens, senhas, certificados reais ou chaves privadas no Git.
- [x] ACL por device validada no Mosquitto. (2026-07-10: device A não publica no tópico do device B; ver [evidência MQTT](mqtt-tls-e-credenciais.md).)
- [x] Credenciais únicas por device validadas no provisionamento via dynamic-security.
- [ ] Rotação de credencial MQTT testada.

## Operação

- [ ] Backup PostgreSQL executado.
- [ ] Restore testado em ambiente descartável.
- [ ] Retenção definida para telemetria e rejeições.
- [ ] Logs confirmados para API, Worker, Mosquitto e PostgreSQL no ambiente do piloto.
- [ ] Health checks confirmados para todos os componentes exigidos no Compose do piloto.
- [ ] Procedimento de encerramento validado.

## Qualidade

- [x] Schema V2 e regressão .NET validados. (2026-07-11; ver [relatório da Fase 1](handoff/relatorio-fase-1-schema-v2-2026-07-11.md).)
- [x] Migration `AddTelemetrySchemaV2` validada up/down/up em PostgreSQL 16 descartável.
- [x] Matriz completa de benchmark V2 executada. (2026-07-11; ver [benchmark](benchmarks/phase1-2026-07-11.md).)
- [x] Concorrência do guardrail e corrida cross-device/same-key validadas em PostgreSQL.
- [x] Gate dense CPU normalizado aprovado: 1,0801 cores em budget de 2 cores = 54,01%. (Ver [benchmark normalizado](benchmarks/phase1-normalized-2026-07-11.md).)
- [x] `npm run build` executado. (2026-07-12: inclui `tsc --noEmit`; Vite verde.)
- [x] `npm audit --omit=dev` executado sem vulnerabilidades de produção. (2026-07-12; revalidação
      de 2026-09-06 encontrou 2 vulnerabilidades moderadas no React Router — correção exige
      migração breaking para 7.x, ainda não tratada; ver riscos ativos em
      [project-status.md](project-status.md).)
- [x] `docker compose config` validado para desenvolvimento. (2026-07-12.)
- [x] `docker compose -f docker-compose.controlled-prod.yml config` validado. (2026-07-12; não comprova TLS ou health checks operacionais.)
- [x] Telemetry Query API e Telemetry Explorer: 71 testes unitários, 40 de integração e 5 de componente verdes; Q1–Q7 em 5.115.083 pontos. (2026-07-12.)
- [x] Teste de contrato JSON (`JsonContractTests`) validado contra o formato real enviado pelo
      navegador (JSON cru), não o objeto C# tipado que os demais testes de integração usam. Pegou
      e corrigiu um bug real: cadastro de device era impossível pelo portal. (2026-09-12.)
- [x] Suite E2E (Playwright) cobrindo os golden paths — registro, login, criação de workspace,
      provisionamento de device, persistência de sessão no reload, página de rejeições, rota
      inexistente — rodando automaticamente como check obrigatório no CI. (2026-09-12.)

## Devices

- [ ] Firmware ESP32 validado por 24h.
- [ ] Simulador validado com 10 devices.
- [ ] Simulador validado com 50 devices.
- [x] Simulador validado com 100 devices. (2026-07-11: 30.000/30.000 mensagens persistidas, sem rejeição; ver [documento técnico](sprint-2/sprint-2-technical-document.md).)
- [x] Publicação MQTT validada no tópico provisionado.
- [x] Consulta protegida validada via API. (2026-07-12: `temperature_c`, `current_a`, `door_open` e `machine_state`; 144 pontos, quatro séries, 13 ms.)

## Documentação

- [ ] Guia do piloto revisado.
- [ ] Backup/restore revisado.
- [ ] Simulador revisado.
- [x] Riscos restantes registrados. (Ver [relatório da Fase 1](handoff/relatorio-fase-1-schema-v2-2026-07-11.md), [relatório da Fase 2](handoff/relatorio-fase-2-telemetry-explorer-2026-07-12.md) e [roadmap consolidado](roadmap-production-1000-devices.md).)
