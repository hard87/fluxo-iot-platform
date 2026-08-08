# Roadmap de evolução visual do Fluxo Portal

## Estratégia

O trabalho deve avançar em entregas pequenas, independentes e reversíveis. Cada etapa abaixo pode gerar um PR/commit próprio e precisa manter APIs, ingestão, regras de status e comportamento de telemetria intactos.

## Gate comum a todas as etapas

- working tree limpa no início e no fim;
- testes existentes do portal aprovados;
- `npm run build` aprovado;
- revisão em 1600, 1024, 768 e 375 px;
- navegação por teclado e foco visível verificadas;
- nenhum restart/rebuild da stack do ensaio sem autorização explícita;
- diff limitado ao frontend e documentação da etapa;
- novos componentes com testes semânticos proporcionais ao risco.

## Etapa 1 — Fundação de tokens

**Objetivo:** substituir decisões visuais dispersas por contratos CSS, sem alterar estrutura de tela.

Escopo:

- criar `tokens.css`, reset mínimo e globals;
- migrar cores, bordas, spacing, radius, shadow e typography;
- remover import remoto de Google Fonts ou preparar fonte auto-hospedada em entrega separada;
- definir `:focus-visible`, seleção de texto e números tabulares;
- mover paleta de gráficos para tokens.

Aceite:

- nenhuma cor/spacing recorrente literal nas páginas;
- aparência funcionalmente equivalente ao baseline;
- CSP sem tentativa de carregar fonte externa;
- contraste dos tokens documentado/validado.

Dependência: nenhuma. Risco: baixo.

## Etapa 2 — Primitives de ação e formulário

**Objetivo:** unificar Button, Input e Select.

Escopo:

- implementar `Button`, `Field`, `Input` e `Select`;
- consolidar link com aparência de botão;
- separar `disabled` de `loading`;
- associar hint/erro a campos com ARIA;
- migrar primeiro Login e Register como prova de contrato.

Aceite:

- variantes e estados cobertos por testes;
- foco, teclado, autocomplete e mensagens de validação preservados;
- nenhuma alteração nos payloads ou fluxo de autenticação.

Dependência: etapa 1. Risco: baixo.

## Etapa 3 — Feedback reutilizável

**Objetivo:** padronizar LoadingState, ErrorState e EmptyState.

Escopo:

- implementar os três componentes e estado inline;
- adicionar `role=status`/`role=alert` adequadamente;
- incluir retry apenas onde a operação existente pode ser repetida com segurança;
- preservar conteúdo anterior durante refresh quando possível;
- migrar Workspace, Devices, Dashboard e Health.

Aceite:

- toda coleção possui loading, erro e vazio distintos;
- empty states têm próximo passo quando aplicável;
- falhas de API continuam usando mensagens seguras e não expõem segredo;
- layout não sofre salto significativo no carregamento inicial.

Dependência: etapa 1; pode rodar em paralelo lógico com a etapa 2, em arquivos distintos. Risco: médio.

## Etapa 4 — PageHeader e estrutura de conteúdo

**Objetivo:** criar hierarquia consistente antes de mudar a navegação.

Escopo:

- implementar `PageHeader`, containers e espaçamento de seções;
- centralizar título, descrição, breadcrumbs e ações;
- migrar uma tela por commit: Workspaces, Dashboard, Devices, Device Details, Explorer e Health;
- garantir exatamente um `h1` por página.

Aceite:

- ações primárias aparecem em posição previsível;
- breadcrumbs e descrição não duplicam navegação;
- containers reading/content/wide usados conforme a tarefa.

Dependência: etapa 1. Risco: baixo.

## Etapa 5 — AppShell e navegação contextual

**Objetivo:** representar claramente contexto global e de workspace.

Arquitetura alvo:

```text
AppShell
├── Header
│   ├── Brand
│   ├── WorkspaceSwitcher
│   └── UserMenu
├── Navigation
│   ├── Global: Workspaces, Saúde da plataforma
│   └── Workspace: Dashboard, Dispositivos, Telemetry Explorer
└── MainContent
    ├── SkipLinkTarget
    ├── PageHeader
    └── PageContent
```

Desktop: header fixo opcional e navegação lateral de 240 px. Mobile: header compacto e drawer com foco gerenciado. O conteúdo deve continuar utilizável sem sticky positioning.

Escopo:

- criar componentes de shell e configuração única de itens;
- marcar rota ativa com `aria-current=page`;
- obter o workspace pelo contexto/rota tipada, não por regex isolada no pathname;
- adicionar skip link;
- manter logout e redirecionamentos atuais.

Aceite:

- Dashboard, Dispositivos e Explorer sempre alcançáveis no contexto correto;
- nome/ID do workspace atual visível;
- troca de workspace leva a destino previsível;
- teclado e mobile validados sem scroll horizontal.

Dependências: etapas 1 e 4. Risco: médio; não alterar autorização ou seleção persistida.

## Etapa 6 — Status semântico

**Objetivo:** tornar estado operacional consistente em todo o portal.

Escopo:

- implementar `Badge` e `StatusBadge` genéricos;
- criar mapeamentos explícitos para dispositivo e saúde;
- migrar dashboard, lista/detalhes de dispositivos e Health;
- exibir texto em português e manter valor técnico em detalhe quando útil;
- incluir timestamp/freshness recebido pela API, sem recalcular o status.

Aceite:

- online, offline, unknown, healthy, degraded e failed têm semântica previsível;
- nenhuma informação depende apenas de cor;
- valores desconhecidos caem em neutral;
- testes garantem os mapeamentos 1/2/3 e string existentes.

Dependências: etapas 1 e 3. Risco: médio devido à relevância operacional.

## Etapa 7 — Política de tempo, números e unidades

**Objetivo:** remover ambiguidade dos dados sem mudar contratos.

Escopo:

- criar `Timestamp`, formatadores de número/duração e apresentação de unidade;
- declarar timezone na interface;
- diferenciar ocorrência, ingestão, último contato e recebimento;
- aplicar em Dashboard, Device Details, Health e tabelas do Explorer;
- manter ISO UTC disponível para diagnóstico.

Aceite:

- nenhum timestamp bruto aparece como valor principal;
- timezone visível e consistente;
- unidade acompanha valores relevantes;
- ausência de dado é representada por texto, não zero inventado.

Dependência: etapa 1. Risco: médio; revisão de domínio obrigatória, sem alterar datas enviadas à API.

## Etapa 8 — Listas operacionais escaláveis

**Objetivo:** melhorar Workspaces e Devices para maior volume.

Escopo:

- criar `DataTable` responsiva com caption e headers acessíveis;
- migrar cards repetidos para linhas com nome, identificador, status, última atividade e ação;
- manter empty/loading/error dos primitives;
- prever toolbar de busca/filtro visual sem implementar API nova;
- oferecer cards apenas em viewport estreita se a tabela não permanecer legível.

Aceite:

- 50+ itens podem ser escaneados sem sombras/cards excessivos;
- ações por linha são nomeadas com contexto;
- tabela funciona com teclado, zoom e scroll horizontal;
- nenhuma paginação client-side enganosa é introduzida.

Dependências: etapas 3, 4, 6 e 7. Risco: baixo a médio.

## Etapa 9 — Dashboard operacional

**Objetivo:** priorizar exceções e freshness no dashboard existente.

Escopo:

- usar `MetricCard` com unidade/contexto;
- agrupar inventário, conectividade e processamento;
- destacar offline/rejeitadas por semântica, não somente cor;
- mostrar momento de atualização e link para investigação;
- criar skeleton que preserve a grade.

Aceite:

- offline, unknown e rejeições são identificáveis em poucos segundos;
- contadores não sugerem tendência quando não há série histórica;
- “última telemetria” usa o padrão temporal;
- nenhuma consulta ou cálculo novo é criado no frontend.

Dependências: etapas 3, 4, 6 e 7. Risco: baixo.

## Etapa 10 — Explorer: filtros e contexto da consulta

**Objetivo:** reduzir carga cognitiva sem modificar a Telemetry Query API.

Escopo:

- extrair `TelemetryFilters` e `QuerySummary`;
- organizar período, agregação e intervalo numa sequência lógica;
- traduzir labels mantendo enums internos;
- oferecer busca local nas listas já carregadas e chips de seleção;
- explicar limites antes do erro;
- manter resultado anterior durante refresh e mostrar metadados da resposta.

Aceite:

- seleção atual fica visível fora dos fieldsets;
- limites 10/10/25 e janelas 24h/90d continuam respeitados;
- 504 e bucket mínimo sugerem correção específica;
- testes atuais permanecem verdes e ganham casos de teclado/erro.

Dependências: etapas 2, 3, 4 e 7. Risco: médio; não tocar na montagem do request além da apresentação.

## Etapa 11 — Explorer: visualização acessível

**Objetivo:** tornar séries comparáveis e diagnosticáveis.

Escopo:

- extrair `TelemetryChart` e tabela de dados;
- rotular eixos e unidades;
- formatar ticks conforme duração e transição de dia;
- combinar cor com traço/símbolo e permitir destacar/ocultar séries;
- oferecer resumo textual e tabela alternativa para Numeric/Boolean;
- manter lacunas e truncamento explicitamente visíveis.

Aceite:

- gráfico continua separando `canonicalUnit` e ValueType;
- Boolean permanece step-after; Text/count permanecem tabulares;
- dados ausentes não viram zero e lacunas não são conectadas;
- uma pessoa sem acesso visual ao SVG consegue acessar os valores;
- paleta/legenda comportam o limite real de séries.

Dependência: etapa 10. Risco: alto relativo; testes de regressão dos dados são obrigatórios.

## Etapa 12 — Detalhes e provisionamento seguro

**Objetivo:** melhorar leitura técnica e ações sensíveis.

Escopo:

- usar `DefinitionList` e `CopyableCode` para identificadores/tópicos;
- separar estado operacional, conectividade e provisionamento;
- enfatizar a natureza de uso único do segredo;
- adicionar confirmação clara à rotação sem alterar sua chamada;
- tornar JSON legível e recolhível com fallback de texto.

Aceite:

- segredo não é persistido nem registrado;
- ação de rotação permanece explicitamente iniciada pelo usuário;
- strings longas não quebram layout;
- ocorrência e ingestão são distinguíveis na telemetria recente.

Dependências: etapas 2, 4, 6 e 7. Risco: médio devido a credenciais.

## Etapa 13 — Hardening responsivo e acessível

**Objetivo:** validar o sistema como um todo.

Escopo:

- auditoria WCAG AA com ferramenta e teste manual;
- fluxo completo por teclado;
- zoom 200%, reduced motion e alto contraste;
- revisão de targets touch, overflow e rolagens aninhadas;
- matriz de breakpoints e estados extremos;
- correção de idioma, acentuação e accessible names.

Aceite:

- zero violações críticas/serious na ferramenta adotada;
- fluxo Login → Workspace → Device → Explorer executável por teclado;
- sem perda de conteúdo em 320 px e zoom 200%;
- status e séries não dependem apenas de cor.

Dependência: etapas 5–12. Risco: baixo, mas pode revelar correções distribuídas.

## Etapa 14 — Proteção contra regressão

**Objetivo:** tornar a evolução sustentável.

Escopo:

- testes de componentes para variantes e estados;
- smoke tests de rotas principais;
- snapshots visuais seletivos nos breakpoints definidos;
- página interna de catálogo dos primitives, se o volume justificar;
- checklist de revisão visual no processo de contribuição.

Aceite:

- regressões de layout/estado crítico são detectadas antes do merge;
- snapshots cobrem estados, não todos os pixels da aplicação;
- documentação e tokens permanecem próximos do código.

Dependência: pode começar após etapa 2 e crescer incrementalmente; fechamento após etapa 13. Risco: baixo.

## Ordem recomendada de releases

1. **Fundação:** etapas 1–4.
2. **Estrutura operacional:** etapas 5–7.
3. **Telas de operação:** etapas 8–9 e 12.
4. **Telemetry Explorer:** etapas 10–11.
5. **Qualidade contínua:** etapas 13–14.

Cada etapa deve manter uma comparação visual com o baseline e registrar decisões que afetem status, tempo ou unidade. Troca de biblioteca de gráficos, dark mode, dashboards configuráveis e editor de widgets ficam fora deste roadmap até existir requisito comprovado.
