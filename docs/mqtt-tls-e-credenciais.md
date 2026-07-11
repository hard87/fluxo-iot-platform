# MQTT TLS e Credenciais por Device

Este documento consolida o fluxo de credenciais MQTT e configuracao TLS local.

## 1. Provisionamento via API

No portal/API, ao provisionar um dispositivo, a resposta inclui:

- `credentialUsername`
- `provisioningSecret` (exibido uma unica vez)
- `mqttPublishTopic`

Depois disso, endpoints de detalhe retornam somente metadados seguros da credencial ativa.

No mesmo request, a API cria automaticamente o acesso correspondente no broker (usuario +
role com ACL restrita ao `mqttPublishTopic` do device) via o plugin `dynamic-security` do
Mosquitto. Nao ha passo manual: se o broker estiver indisponivel no momento do provisionamento,
a credencial fica registrada no banco mas o erro e logado (`ProvisionDeviceUseCase`) para
sincronizacao posterior.

## 2. Rotacao de credencial

- Endpoint de rotacao gera nova credencial ativa.
- Credencial anterior e revogada no banco.
- `provisioningSecret` novo tambem e exibido apenas no momento da rotacao.
- A API cria o novo usuario no broker (reaproveitando a mesma role/ACL do device) e remove o
  usuario anterior automaticamente.

## 3. Como a autenticacao e ACL funcionam no broker

Autenticacao e autorizacao MQTT sao geridas pelo plugin `dynamic-security` do Mosquitto
(`docker/mosquitto/mosquitto.conf`), nao por `password_file`/`acl_file` estaticos. Cada device
tem:

- uma **role** estavel por device (`device-<deviceId>`), com uma unica ACL
  `publishClientSend` restrita ao `mqttPublishTopic` do device;
- um **client** (usuario/senha) vinculado a essa role, recriado a cada rotacao.

Isso e feito pela API via `IDeviceMqttAccessProvisioner`
(`src/Fluxo.Infrastructure/Mqtt/MqttDynamicSecurityDeviceProvisioner.cs`), que fala o protocolo
de controle `$CONTROL/dynamic-security/v1` do broker usando as credenciais administrativas
configuradas em `MqttDynamicSecurity:AdminUsername`/`AdminPassword`.

O worker de ingestao tambem recebe acesso automatico (role `ingestion-worker` com
`subscribePattern`/`publishClientReceive` no filtro de topico), garantido no startup da API por
`MqttIngestionWorkerAccessBootstrapper` (`src/Fluxo.Api/Services/`).

Esse fluxo so roda quando `MqttDynamicSecurity:Enabled=true` (padrao no `docker-compose.yml` e
`docker-compose.controlled-prod.yml`). Em execucao local sem broker (`dotnet run` fora do
Docker), fica desabilitado por padrao e a API funciona normalmente, so sem sincronizar o broker.

## 4. Bootstrap do broker (uma vez por ambiente)

O admin do `dynamic-security` e criado automaticamente na primeira subida do container
`mosquitto` (`mosquitto_ctrl dynsec init`, ver `command:` do servico no compose), usando
`FLUXO_MQTT_DYNSEC_ADMIN_USERNAME`/`FLUXO_MQTT_DYNSEC_ADMIN_PASSWORD`. O arquivo gerado
(`docker/mosquitto/dynamic-security.json`) persiste no volume/bind mount local e nao deve ser
versionado.

## 5. TLS local (listener 8883)

Gerar certificados locais:

```powershell
powershell -ExecutionPolicy Bypass -File docker/mosquitto/scripts/generate-local-certs.ps1 -CommonName broker.fluxo.local
```

Arquivos esperados:
- `docker/mosquitto/certs/ca.crt`
- `docker/mosquitto/certs/server.crt`
- `docker/mosquitto/certs/server.key`

## 6. Configuracao do firmware (ESP32 referencia)

No arquivo local nao versionado `devices/esp32-reference-node/main/app_config.local.h`:

- `APP_MQTT_BROKER_URI` (obrigatoriamente `mqtts://<host>:8883`)
- `APP_MQTT_USERNAME` (`credentialUsername`)
- `APP_MQTT_PASSWORD` (`provisioningSecret`)
- `APP_MQTT_CA_CERT_PEM` (CA do broker em string C com `\n`)

Obs.: o host de `APP_MQTT_BROKER_URI` precisa bater com o `CommonName` usado no certificado do broker.

Conversao util da CA para string C:

```powershell
$ca = (Get-Content docker/mosquitto/certs/ca.crt -Raw).Replace("`r","").Replace("`n","\n")
$ca
```

## 7. Validacao da telemetria

- Publicacao autorizada deve entrar no worker e persistir.
- Mensagens invalidas, device inativo ou nao provisionado vao para `telemetry_ingestion_rejections`.

## 8. Riscos conhecidos antes de producao publica

- Certificados locais/self-signed servem apenas para laboratorio.
- `1883` sem TLS nao deve ser exposto em rede publica.
- O perfil `docker-compose.controlled-prod.yml` publica somente `8883` para MQTT.
- O worker aceita `MqttIngestion__UseTls=true`, `MqttIngestion__TlsTargetHost` e `MqttIngestion__TlsCaCertificatePath` para assinar a conexao com o broker.
- A API aceita as mesmas variaveis equivalentes em `MqttDynamicSecurity__UseTls`,
  `MqttDynamicSecurity__TlsTargetHost` e `MqttDynamicSecurity__TlsCaCertificatePath` para a
  conexao administrativa com o broker (ja habilitadas por padrao em
  `docker-compose.controlled-prod.yml`).
- Recomenda-se mutual TLS e gerencia de segredo por cofre para ambiente comercial.
