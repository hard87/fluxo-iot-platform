# Dashboard operacional do workspace

**Implementado em:** 2026-08-08

## Objetivo

O Dashboard foi reorganizado como uma leitura operacional rápida. A tela deve permitir identificar inventário, conectividade, atividade de telemetria e sinais de atenção sem reproduzir as funções analíticas do Telemetry Explorer.

Nenhum contrato de API, regra de ingestão ou interpretação da telemetria foi alterado.

## Dados utilizados

Todos os valores operacionais continuam vindo de `DashboardResponse`:

| Apresentação | Campo existente |
|---|---|
| Dispositivos | `devicesTotal` |
| Online | `devicesOnline` |
| Offline | `devicesOffline` |
| Unknown | `devicesUnknown` |
| Mensagens processadas | `messagesProcessed` |
| Mensagens rejeitadas | `messagesRejected` |
| Última telemetria | `lastTelemetryReceivedAtUtc` |
| Tenant | `tenantId` |

O nome do workspace é resolvido por `workspaceService.listWorkspaces`, endpoint já utilizado pelo portal. Se o nome não estiver disponível, a interface usa uma versão curta do `workspaceId`; uma falha nessa consulta auxiliar não altera os KPIs.

## Estrutura

```text
Dashboard
├── PageHeader
│   ├── nome do workspace e tenant
│   └── Ver dispositivos / Explorar telemetria
├── Indicadores
│   └── 6 DashboardKpiCard
└── Atividade e situação
    ├── TelemetryActivity
    └── WorkspaceHealthStatus
```

### KPIs

Os seis cards têm superfície neutra. A semântica aparece apenas numa borda superior e num pequeno indicador:

- online: success;
- offline com valor maior que zero: danger;
- unknown com valor maior que zero: warning;
- mensagens processadas: info;
- rejeitadas com valor maior que zero: danger;
- totais sem exceção: neutral.

Os números são formatados com locale `pt-BR`. Não são calculadas taxas, tendências ou percentuais que o backend não forneça.

### Última telemetria

O timestamp original continua preservado em `dateTime` e `title`. A informação principal passa a ser relativa, por exemplo “Há 2 minutos”, seguida pelo valor absoluto no horário local. Quando não há timestamp, a tela informa explicitamente “Sem telemetria recebida”; zero ou horário artificial não são inventados.

### Síntese operacional

`WorkspaceHealthStatus` é uma síntese visual dos mesmos indicadores, não um novo status de domínio e não substitui a página Saúde da plataforma. A precedência é deliberadamente conservadora:

1. `offline`: existe ao menos um dispositivo offline;
2. `unknown`: não há dispositivos ou existe dispositivo com estado desconhecido;
3. `warning`: existem mensagens rejeitadas;
4. `unknown`: não existe última telemetria;
5. `healthy`: nenhum dos sinais anteriores está presente;
6. `error`: o Dashboard não pôde ser carregado.

Cada estado combina texto, ponto, borda e fundo sutil; a leitura não depende apenas de cor. O texto explica qual dado acionou a classificação.

## Navegação

- **Ver dispositivos** é a ação primária e leva à listagem do workspace;
- **Explorar telemetria** é secundária e leva ao Explorer;
- nenhum gráfico, filtro ou consulta analítica foi duplicado no Dashboard.

## Responsividade

- acima de 1200 px: seis KPIs em uma linha;
- entre 801 e 1200 px: três KPIs por linha;
- até 800 px: dois KPIs por linha e atividade empilhada;
- até 480 px: um KPI por linha;
- o header contextual e os CTAs empilham abaixo de 700 px.

Cards e grids usam colunas `minmax(0, 1fr)` e limites de largura para evitar overflow. A prioridade continua sendo desktop, com adaptação explícita para desktop comum e tablet.

## Componentes e testes

Componentes específicos em `portal-web/src/components/dashboard`:

- `DashboardKpiCard`;
- `TelemetryActivity` e `formatDashboardTimestamp`;
- `WorkspaceHealthStatus` e `getWorkspaceHealthSummary`.

Os testes cobrem contexto do workspace, seis KPIs, formatação relativa, CTAs, ausência de telemetria, erro de carregamento e a precedência dos estados operacionais.
