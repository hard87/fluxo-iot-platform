# Mosquitto (seguranca basica)

Esta pasta contem configuracao inicial para broker MQTT local do Fluxo.

## Arquivos

- `mosquitto.conf`: configuracao principal (sem `allow_anonymous`).
- `passwords.example`: exemplo de arquivo de usuarios.
- `acl.example`: exemplo de ACL por topico.

## Como preparar ambiente local

1. Gere arquivo real de usuarios:
   - `mosquitto_passwd -c passwords <usuario>`
2. Crie arquivo real de ACL com regras minimas por tenant/workspace/device.
3. Mantenha `passwords` e `acl` fora de versionamento (ja ignorados no `.gitignore`).

## Observacao

Esta base e para hardening inicial. Antes de producao, habilite TLS e revise politicas de acesso por dispositivo.
