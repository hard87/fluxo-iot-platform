# MQTT TLS e Credenciais por Device

Este documento consolida o fluxo de credenciais MQTT e configuracao TLS local.

## 1. Provisionamento via API

No portal/API, ao provisionar um dispositivo, a resposta inclui:

- `credentialUsername`
- `provisioningSecret` (exibido uma unica vez)
- `mqttPublishTopic`

Depois disso, endpoints de detalhe retornam somente metadados seguros da credencial ativa.

## 2. Rotacao de credencial

- Endpoint de rotacao gera nova credencial ativa.
- Credencial anterior e revogada.
- `provisioningSecret` novo tambem e exibido apenas no momento da rotacao.

## 3. Geracao de password_file e acl_file

Use o script:

```powershell
powershell -ExecutionPolicy Bypass -File docker/mosquitto/scripts/generate-auth-files.ps1 -Overwrite
```

Entrada esperada em `docker/mosquitto/credentials.local.json`:

```json
[
  {
    "username": "dev-acme-11111111-esp32-lab-01",
    "secret": "<provisioningSecret>",
    "topic": "fluxo/tenants/acme/workspaces/11111111-1111-1111-1111-111111111111/devices/esp32-lab-01/telemetry"
  }
]
```

Saida:
- `docker/mosquitto/passwords`
- `docker/mosquitto/acl`

## 4. ACL por dispositivo

Regra minima por usuario:

```text
user <credentialUsername>
topic write <mqttPublishTopic>
```

Isso impede que um device publique em topic de outro device.

## 5. TLS local (listener 8883)

Gerar certificados locais:

```powershell
powershell -ExecutionPolicy Bypass -File docker/mosquitto/scripts/generate-local-certs.ps1
```

Arquivos esperados:
- `docker/mosquitto/certs/ca.crt`
- `docker/mosquitto/certs/server.crt`
- `docker/mosquitto/certs/server.key`

## 6. Configuracao do firmware (ESP32 referencia)

No arquivo local nao versionado `devices/esp32-reference-node/main/app_config.local.h`:

- `APP_MQTT_BROKER_URI` (`mqtt://` para 1883 ou `mqtts://` para 8883)
- `APP_MQTT_USERNAME` (`credentialUsername`)
- `APP_MQTT_PASSWORD` (`provisioningSecret`)

## 7. Validacao da telemetria

- Publicacao autorizada deve entrar no worker e persistir.
- Mensagens invalidas, device inativo ou nao provisionado vao para `telemetry_ingestion_rejections`.

## 8. Riscos conhecidos antes de producao publica

- Certificados locais/self-signed servem apenas para laboratorio.
- `1883` sem TLS nao deve ser exposto em rede publica.
- Recomenda-se mutual TLS e gerencia de segredo por cofre para ambiente comercial.
