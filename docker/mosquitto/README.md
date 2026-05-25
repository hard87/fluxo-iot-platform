# Mosquitto (seguranca basica)

Esta pasta contem configuracao inicial para broker MQTT local do Fluxo.

## Arquivos

- `mosquitto.conf`: configuracao principal (sem `allow_anonymous`).
- `passwords.example`: exemplo de arquivo de usuarios.
- `acl.example`: exemplo de ACL por topico.

## Como preparar ambiente local

1. Provisione um device na API (`POST /api/provisioning/devices`) para obter:
   - `credentialUsername`
   - `provisioningSecret` (exibido uma unica vez)
   - `mqttPublishTopic`
2. Gere/atualize arquivo real de usuarios:
   - primeira vez: `mosquitto_passwd -c passwords <credentialUsername>`
   - proximas: `mosquitto_passwd passwords <credentialUsername>`
3. No prompt da senha, use `provisioningSecret` retornado pela API.
4. Crie/atualize arquivo real de ACL com regra minima por device:
   - `user <credentialUsername>`
   - `topic write <mqttPublishTopic>`
5. Mantenha `passwords` e `acl` fora de versionamento (ja ignorados no `.gitignore`).
6. Reinicie o broker apos alteracoes de credenciais/ACL.

## Observacao

Esta base e para hardening inicial. Antes de producao, habilite TLS e revise politicas de acesso por dispositivo.
