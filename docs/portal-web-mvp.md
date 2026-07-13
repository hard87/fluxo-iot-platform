# Portal Web MVP do Fluxo

Estado consolidado após a Produto Fase 2, em 12 jul 2026.

## Stack e estrutura

- React, Vite e TypeScript;
- Recharts 2.x para visualização de séries temporais;
- `src/services/api`: chamadas HTTP e tratamento unificado de erro;
- `src/hooks`: sessão e seleção de workspace;
- `src/components`: layout e componentes reutilizáveis;
- `src/pages`: telas do MVP;
- `src/types`: contratos tipados com a API;
- `src/utils`: sanitização e validações.

## Telas implementadas

- Login (`/login`);
- cadastro inicial (`/register`);
- criação, listagem e seleção de workspace (`/workspaces`);
- dashboard (`/workspaces/{workspaceId}/dashboard`);
- listagem e cadastro de devices (`/workspaces/{workspaceId}/devices` e `/devices/new`);
- detalhes do device com última telemetria e status (`/workspaces/{workspaceId}/devices/{deviceId}`);
- Telemetry Explorer (`/workspaces/{workspaceId}/explorer`);
- credencial exibida somente no momento de criação/rotação;
- saúde da plataforma (`/status`).

## Telemetry Explorer

O Explorer consulta dinamicamente o catálogo de métricas e a Telemetry Query API. Permite
selecionar devices, métricas, período, agregação e bucket, respeitando limites básicos visíveis
na UI: até 10 devices, 10 métricas e 25 séries por consulta.

- Numeric: gráficos de linha, separados por unidade canônica;
- Boolean: gráfico em degrau com domínio 0/1;
- Text: tabela cronológica;
- `count`: tabela com `SampleCount`;
- painéis agrupados por unidade;
- estados de loading, vazio, truncamento, validação e timeout.

O contrato completo, matriz de agregações e guardrails pertencem ao
[ADR-0003](adr/0003-telemetry-query-api.md).

## Segurança aplicada no frontend

- token não persistido em `localStorage`;
- segredos de provisionamento exibidos apenas em memória na tela atual;
- sanitização básica de entradas e validações de formulário;
- erros `ProblemDetails` tratados de forma padronizada;
- sem uso de `dangerouslySetInnerHTML`.

## Execução local

```powershell
cd portal-web
npm install
npm run dev
```

A aplicação usa `VITE_API_BASE_URL` e, por padrão local, sobe em `http://localhost:5173`.

## Fluxo funcional esperado

1. Criar conta e fazer login.
2. Criar ou selecionar um workspace.
3. Cadastrar e provisionar um device; copiar a credencial exibida uma vez.
4. Acompanhar status e última telemetria.
5. Abrir o Telemetry Explorer, escolher devices e métricas do catálogo.
6. Definir período, agregação e bucket; consultar Numeric, Boolean ou Text sem SQL.

## Limitações atuais do MVP

- sessão não persiste após refresh;
- sem multi-idioma;
- sem recuperação de senha;
- bundle principal de 575,37 kB minificado, acima do warning de 500 kB do Vite;
- Explorer ainda carregado no bundle principal, sem lazy loading;
- fonte Google externa bloqueada pela CSP do nginx, com fallback local ativo;
- não é um editor universal de dashboards e não oferece exportação/paginação de grandes volumes.
