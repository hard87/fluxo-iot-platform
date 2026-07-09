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
- [ ] ACL por dispositivo validada no Mosquitto.
- [ ] Credenciais unicas por dispositivo.
- [ ] Rotacao de credencial MQTT testada.

## Operacao

- [ ] Backup PostgreSQL executado.
- [ ] Restore testado em ambiente descartavel.
- [ ] Retencao definida para telemetria e rejeicoes.
- [ ] Logs disponiveis para API, worker, Mosquitto e PostgreSQL.
- [ ] Health checks ativos no Compose.
- [ ] Procedimento de encerramento validado.

## Qualidade

- [ ] `dotnet test Fluxo.slnx` executado.
- [ ] `npm run build` executado.
- [ ] `npm audit --omit=dev` sem vulnerabilidades de producao.
- [ ] `docker compose config` validado para dev.
- [ ] `docker compose -f docker-compose.controlled-prod.yml config` validado.

## Dispositivos

- [ ] Firmware ESP32 validado por 24h.
- [ ] Simulador validado com 10 dispositivos.
- [ ] Simulador validado com 50 dispositivos.
- [ ] Simulador validado com 100 dispositivos.
- [ ] Publicacao MQTT validada no topico provisionado.
- [ ] Consulta de telemetria validada via API protegida.

## Documentacao

- [ ] Guia do piloto revisado.
- [ ] Backup/restore revisado.
- [ ] Simulador revisado.
- [ ] Riscos restantes registrados.
