# Fluxo - Roadmap tecnico para producao com 1000 dispositivos

## Meta de referencia

- 1000 dispositivos globais
- envio medio: 1 mensagem a cada 10 segundos
- carga media esperada: ~100 msg/s
- volume diario esperado: ~8.640.000 eventos

## Fase 1 - Hardening basico (concluido nesta iteracao)

1. Segredos removidos de arquivos versionados.
2. Middleware de erro sem vazamento de detalhes internos em producao.
3. Estrutura inicial de autenticacao/autorizacao na API.
4. MQTT sem anonymous por default e base para ACL.
5. Idempotencia inicial na ingestao.
6. Tabela de rejeicoes para trilha operacional.
7. Buffer interno no worker e metricas iniciais.

## Fase 2 - Piloto real controlado

1. Autenticacao MQTT obrigatoria por device.
2. ACL por topico (tenant/workspace/device).
3. Reprocessador de `telemetry_ingestion_rejections`.
4. Dashboard operacional minimo:
   - taxa de ingestao;
   - taxa de rejeicao;
   - duplicatas;
   - falhas de banco;
   - backlog interno.
5. Teste de carga sustentado (>= 100 msg/s por 30-60 min).

## Fase 3 - Preparacao de producao

1. Particionamento temporal em `telemetry_ingestion_records`.
2. Politica de retencao e arquivamento.
3. Planejamento de HA:
   - broker MQTT redundante;
   - worker com escala horizontal;
   - estrategia de failover.
4. Tracing distribuido e alertas operacionais.
5. Revisao de custos de armazenamento para crescimento de longo prazo.

## Fase 4 - Producao inicial escalavel

1. SLOs operacionais definidos (latencia, disponibilidade, perda toleravel).
2. Execucao recorrente de testes de carga/chaos.
3. Playbooks de incidente e recuperacao.
4. Governanca de schema e versao de payload.

## Riscos tecnicos em aberto

1. Falta de particionamento temporal no estado atual.
2. Sem HA completo de broker.
3. Reprocessamento ainda manual.
4. Observabilidade ainda sem tracing fim-a-fim.

## Criterio de saida para "producao com 1000 devices"

O Fluxo so deve ser classificado como producao pronta para 1000 devices quando:

1. autenticação + ACL estiverem obrigatorias;
2. ingestao tiver idempotencia validada sob carga;
3. retencao + particionamento estiverem implantados;
4. metricas e alertas operacionais estiverem ativos;
5. testes de carga sustentados comprovarem estabilidade.
