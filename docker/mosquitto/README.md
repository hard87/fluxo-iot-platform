# Mosquitto - autenticacao por dispositivo e TLS local

Esta pasta contem a base local para broker MQTT do Fluxo com:
- autenticacao por usuario/senha por device;
- ACL por topico;
- listener TLS em `8883` para laboratorio controlado.

## Arquivos principais

- `mosquitto.conf`: configuracao principal (1883 + 8883 TLS).
- `passwords.example`: placeholder para arquivo real `passwords`.
- `acl.example`: placeholder para arquivo real `acl`.
- `credentials.template.json`: template para gerar `passwords` e `acl`.
- `scripts/generate-auth-files.ps1`: automacao local de `password_file` e `acl_file`.
- `scripts/generate-local-certs.ps1`: gera CA e certificado local/self-signed.
- `data/` e `log/`: runtime do broker. Sao recriados pelo Mosquitto e nao devem ser versionados.

## Fluxo recomendado local

1. Copie o template de credenciais:

```powershell
Copy-Item docker/mosquitto/credentials.template.json docker/mosquitto/credentials.local.json
```

2. Preencha cada item com:
- `username`: `credentialUsername` retornado no provisionamento;
- `secret`: `provisioningSecret` retornado na criacao/rotacao;
- `topic`: `mqttPublishTopic` retornado pela API.

3. Gere arquivos reais do broker:

```powershell
powershell -ExecutionPolicy Bypass -File docker/mosquitto/scripts/generate-auth-files.ps1 -Overwrite
```

4. Gere certificados locais para `8883` (uma vez por ambiente local):

```powershell
powershell -ExecutionPolicy Bypass -File docker/mosquitto/scripts/generate-local-certs.ps1
```

5. Reinicie o Mosquitto para aplicar alteracoes.

## Seguranca

- `passwords`, `acl` e `credentials.local.json` nao devem ser versionados.
- chaves privadas (`*.key`) e certificados locais gerados tambem nao devem ser versionados.
- `data/mosquitto.db` e logs sao estado/runtime do broker; nao carregue esses arquivos para o Git.
- o listener `1883` existe apenas para desenvolvimento interno controlado.
- para piloto/externo, priorize `8883` com validacao de certificado no client.
