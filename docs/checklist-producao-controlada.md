# Checklist de Producao Controlada

Use este checklist antes de expor um piloto com ate 100 dispositivos simultaneos.

## Rede e TLS

- [ ] TLS HTTP ativo no proxy/terminador da API e do Portal.
- [ ] MQTT TLS ativo em `8883`.
- [ ] Porta `1883` nao exposta publicamente.
- [ ] Certificado MQTT valido para o host usado pelos devices.
- [ ] HSTS habilitado somente quando HTTPS estiver ativo.

## Identidade e acesso

- [ ] JWT configurado com issuer, audience e signing key forte.
- [ ] `.env` real nao versionado.
- [ ] Sem tokens, senhas, certificados reais ou chaves privadas no Git.
- [x] ACL por dispositivo validada no Mosquitto. (2026-07-10: device A nao consegue publicar no topico do device B; ver docs/mqtt-tls-e-credenciais.md)
- [x] Credenciais unicas por dispositivo. (username+senha exclusivos por device, criados automaticamente no provisionamento via dynamic-security)
- [ ] Rotacao de credencial MQTT testada.

## Operacao

- [ ] Backup PostgreSQL executado.
- [ ] Restore testado em ambiente descartavel.
- [ ] Retencao definida para telemetria e rejeicoes.
- [ ] Logs disponiveis para API, worker, Mosquitto e PostgreSQL.
- [ ] Health checks ativos no Compose.
- [ ] Procedimento de encerramento validado.

## Qualidade

- [x] `dotnet test Fluxo.slnx` executado.
- [ ] `npm run build` executado.
- [ ] `npm audit --omit=dev` sem vulnerabilidades de producao.
- [ ] `docker compose config` validado para dev.
- [ ] `docker compose -f docker-compose.controlled-prod.yml config` validado.

## Dispositivos

- [ ] Firmware ESP32 validado por 24h.
- [ ] Simulador validado com 10 dispositivos.
- [ ] Simulador validado com 50 dispositivos.
- [x] Simulador validado com 100 dispositivos. (2026-07-11: 100 devices x 300 msgs, ~100 msg/s por 5min, 30000/30000 persistidas, 0 rejeicoes, 0 erros/reconnects — ver docs/sprint-2/sprint-2-technical-document.md)
- [x] Publicacao MQTT validada no topico provisionado.
- [ ] Consulta de telemetria validada via API protegida. (validado direto no banco nesta sessao; endpoint HTTP de consulta ainda nao exercitado)

## Documentacao

- [ ] Guia do piloto revisado.
- [ ] Backup/restore revisado.
- [ ] Simulador revisado.
- [ ] Riscos restantes registrados.
