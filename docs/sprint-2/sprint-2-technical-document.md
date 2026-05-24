# Fluxo - Sprint 2 (Documento Tecnico)

## 1) Status atual do Fluxo

O Fluxo evoluiu de **MVP tecnico interno basico** para **MVP tecnico interno endurecido** (hardening inicial).

Estado atual:

1. Pipeline MQTT ponta a ponta funcional.
2. Validacao de contrato mais robusta.
3. Idempotencia inicial na persistencia.
4. Trilho de rejeicoes persistidas para analise e reprocessamento futuro.
5. Ajustes de seguranca basica para reduzir risco imediato.

## 2) Objetivo desta evolucao incremental

Esta evolucao nao reescreve a arquitetura. O foco foi reduzir risco tecnico imediato em:

1. Seguranca de configuracoes e erros.
2. Consistencia de ingestao de telemetria.
3. Confiabilidade operacional do worker MQTT.
4. Base de observabilidade minima.
5. Preparacao para escalar gradualmente rumo a 1000 devices.

## 3) Fluxo de ingestao atual

```text
Device -> MQTT Broker -> Fluxo.Worker.Ingestion -> PostgreSQL
```

Passos:

1. Worker assina topico `fluxo/tenants/+/workspaces/+/devices/+/telemetry`.
2. Mensagem recebida entra em buffer interno (`Channel<T>` bounded).
3. Processador valida topico, payload e coerencia topico x payload.
4. Mensagens validas sao persistidas em `telemetry_ingestion_records`.
5. Mensagens invalidas/duplicadas/falhas de persistencia sao registradas em `telemetry_ingestion_rejections`.

## 4) Decisoes arquiteturais aplicadas

### 4.1 Canonical path de telemetria

- **Canonical path novo:** `telemetry_ingestion_records` (ingestao MQTT).
- **Path legado mantido temporariamente:** `telemetry_records` (HTTP legado), para compatibilidade.

Regra de leitura na API:

1. Tenta primeiro `telemetry_ingestion_records` (por `workspaceId + deviceIdentifier`).
2. Se nao houver dados, faz fallback para `telemetry_records`.

Essa decisao evita ruptura imediata e prepara migracao gradual.

### 4.2 Idempotencia

Foi adicionada chave logica por:

- `tenantId`
- `workspaceId`
- `deviceId`
- `sequence`

Implementacao:

1. Indice unico filtrado em `telemetry_ingestion_records`:
   `UX_telemetry_ingestion_records_tenant_workspace_device_sequence`.
2. Tratamento explicito de duplicidade no repositorio e no processor.
3. Duplicatas sao descartadas com log e registro em tabela de rejeicao.

### 4.3 Rejeicao estruturada (DLQ inicial)

Nova tabela:

- `telemetry_ingestion_rejections`

Finalidade:

1. Guardar payload/topic/motivo/tipo de erro/timestamp.
2. Permitir trilha de auditoria de mensagens rejeitadas.
3. Preparar reprocessamento futuro sem perder contexto.

Tipos atuais de falha registrados:

1. `PayloadInvalid`
2. `Validation`
3. `Duplicate`
4. `DatabaseError`
5. `TransientError`
6. `ProcessingError`

### 4.4 Buffer interno no worker

O worker foi separado em duas responsabilidades:

1. Consumo MQTT (entrada).
2. Processamento/persistencia (saida).

Foi adotado `Channel<T>` bounded para:

1. evitar acoplamento direto callback MQTT -> banco;
2. permitir backpressure;
3. preparar batching e evolucao futura.

## 5) Seguranca aplicada

Ajustes implementados:

1. Remocao de credenciais hardcoded de `appsettings` da API e do worker.
2. Resolucao de conexao por `ConnectionStrings__DefaultConnection` ou variaveis `FLUXO_DB_*`.
3. Firmware de referencia sem SSID/senha reais versionados.
4. `mosquitto.conf` sem `allow_anonymous true`.
5. Estrutura inicial para `password_file` e `acl_file` no broker.
6. Middleware global sem exposicao direta de `exception.Message` em producao.
7. Estrutura inicial de autenticacao/autorizacao na API (`Authentication` + `UseAuthentication`).

## 6) Observabilidade minima aplicada

Implementado:

1. Correlation ID por header `X-Correlation-ID` na API.
2. Health check HTTP (`/health`) com verificador de `DbContext`.
3. Instrumentacao inicial com `System.Diagnostics.Metrics` no worker:
   - mensagens recebidas;
   - enfileiradas;
   - desenfileiradas;
   - persistidas;
   - rejeitadas;
   - duplicadas;
   - falhas de banco;
   - falhas transitorias;
   - falhas de processamento.

## 7) Modelagem e banco (estado atual)

Melhorias aplicadas:

1. Indice unico de idempotencia.
2. Indice composto para consultas por tenant/workspace/device/tempo.
3. Tabela de rejeicoes com indices operacionais.

Observacao de escala:

- Cenário alvo: 1000 devices, 1 evento/10s (aprox. 100 msg/s, 8.640.000 msg/dia).
- A modelagem atual sustenta evolucao inicial, mas **particionamento temporal** e **politica de retencao** seguem como etapa obrigatoria antes de producao plena.

## 8) Riscos ainda existentes

1. Broker MQTT ainda sem estrategia de alta disponibilidade.
2. Sem reprocessador automatico para `telemetry_ingestion_rejections`.
3. Sem politica formal de retencao/arquivamento.
4. Sem particionamento por tempo na tabela de ingestao.
5. Sem politica de autenticação/autorização de dispositivo em runtime (somente base preparada).
6. Sem tracing distribuido completo fim-a-fim.

## 9) Proximos passos priorizados

### 9.1 Antes do primeiro piloto real

1. Habilitar autenticação MQTT por dispositivo com ACL estrita por topico.
2. Implementar job de reprocessamento de rejeicoes.
3. Definir contrato de schema versionado por `schemaVersion`.
4. Criar testes de carga de ingestao (100 msg/s sustentado).

### 9.2 Antes de producao com 1000 devices

1. Particionamento temporal em `telemetry_ingestion_records`.
2. Retencao/arquivamento por janela (hot/warm/cold).
3. Estratégia de HA para broker e para worker.
4. Alertas operacionais (latencia, backlog, taxa de rejeicao, erro de banco).

## 10) Artefatos relevantes no repositorio

1. Worker: `src/Fluxo.Worker.Ingestion/`
2. Processor: `src/Fluxo.Worker.Ingestion/Services/TelemetryIngestionProcessor.cs`
3. Config ingestao: `src/Fluxo.Worker.Ingestion/Options/MqttIngestionOptions.cs`
4. Tabela de ingestao: `src/Fluxo.Domain/Entities/TelemetryIngestionRecord.cs`
5. Tabela de rejeicao: `src/Fluxo.Domain/Entities/TelemetryIngestionRejectionRecord.cs`
6. Config EF ingestao: `src/Fluxo.Infrastructure/Configurations/TelemetryIngestionRecordConfiguration.cs`
7. Config EF rejeicao: `src/Fluxo.Infrastructure/Configurations/TelemetryIngestionRejectionRecordConfiguration.cs`
8. Migration hardening: `src/Fluxo.Infrastructure/Migrations/*HardenIngestionPipeline.cs`

## Resumo

A Sprint 2 passou a ter uma base de ingestao mais segura e confiavel, com idempotencia inicial, validacao topico/payload, trilha de rejeicoes e melhor separacao interna no worker. Ainda nao e producao escalavel completa, mas a base ficou significativamente mais preparada para evoluir rumo a um produto IoT real.
