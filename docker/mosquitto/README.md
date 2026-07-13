# Mosquitto - autenticacao por dispositivo e TLS local

Esta pasta contem a base local para broker MQTT do Fluxo com:
- autenticacao e ACL por device via plugin `dynamic-security` (sem `password_file`/`acl_file` estaticos);
- listener TLS em `8883` para laboratorio controlado.

## Arquivos principais

- `mosquitto.conf`: configuracao principal (1883 + 8883 TLS + plugin `dynamic-security`).
- `dynamic-security.json`: base de usuarios/roles/ACLs gerida pelo broker. Criada automaticamente
  no primeiro start do container (`mosquitto_ctrl dynsec init`) e depois atualizada pela API a
  cada provisionamento/rotacao de device. **Nao versionar.**
- `scripts/generate-local-certs.ps1`: gera CA e certificado local/self-signed.
- `data/` e `log/`: runtime do broker. Sao recriados pelo Mosquitto e nao devem ser versionados.

## Fluxo local

1. Defina `FLUXO_MQTT_DYNSEC_ADMIN_USERNAME`/`FLUXO_MQTT_DYNSEC_ADMIN_PASSWORD` no `.env` (ou use
   os defaults de desenvolvimento do `docker-compose.yml`).
2. Gere certificados locais para `8883` (uma vez por ambiente local):

```powershell
powershell -ExecutionPolicy Bypass -File docker/mosquitto/scripts/generate-local-certs.ps1
```

3. Suba a stack com `docker compose up -d`. O container `mosquitto` cria o admin do
   `dynamic-security` automaticamente no primeiro start.
4. Ao provisionar (ou rotacionar) um device pela API, o usuario/ACL correspondente e criado no
   broker automaticamente — nao ha passo manual de `password_file`/`acl_file`. Veja
   [docs/mqtt-tls-e-credenciais.md](../../docs/mqtt-tls-e-credenciais.md).

## Seguranca

- `dynamic-security.json` nao deve ser versionado (contem hashes de senha de todos os devices e
  do admin).
- chaves privadas (`*.key`) e certificados locais gerados tambem nao devem ser versionados.
- `data/mosquitto.db` e logs sao estado/runtime do broker; nao carregue esses arquivos para o Git.
- o listener `1883` existe apenas para desenvolvimento interno controlado.
- para piloto/externo, priorize `8883` com validacao de certificado no client.
