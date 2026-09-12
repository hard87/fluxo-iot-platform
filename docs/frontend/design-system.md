# Design system mínimo do Fluxo Portal

## Princípios

O Fluxo é uma console operacional, não uma vitrine. O sistema visual deve priorizar leitura rápida, comparação, rastreabilidade temporal e comportamento previsível.

1. **Semântica antes de decoração:** cor reforça texto, nunca o substitui.
2. **Densidade controlada:** informação operacional cabe na tela sem sacrificar legibilidade.
3. **Tempo explícito:** timestamp, fuso e freshness não podem ser implícitos.
4. **Unidade junto do valor:** unidade aparece no valor, eixo e tooltip relevantes.
5. **Estados estáveis:** loading e atualização preservam contexto sempre que possível.
6. **Acessibilidade por padrão:** teclado, foco, contraste e anúncios assistivos fazem parte do contrato.
7. **Dependências mínimas:** manter React, CSS próprio e Recharts; nenhuma biblioteca grande é necessária nesta fase.

## Tokens propostos

Os nomes são contratos semânticos. Componentes não devem consumir cores literais. Os valores abaixo formam o tema claro inicial; um tema escuro futuro deve substituir valores sem alterar os nomes.

### Cores

| Token | Valor inicial | Uso |
|---|---:|---|
| `--color-primary` | `#0B5C4B` | ação principal, link e navegação ativa |
| `--color-primary-hover` | `#08483B` | hover de ação principal |
| `--color-primary-subtle` | `#E6F2EE` | fundo selecionado/realce de marca |
| `--color-on-primary` | `#FFFFFF` | conteúdo sobre primary |
| `--color-background` | `#F4F7F6` | fundo geral da aplicação |
| `--color-surface` | `#FFFFFF` | cards, menus e controles |
| `--color-surface-subtle` | `#F8FAF9` | grupos, linhas alternadas e code blocks |
| `--color-border` | `#CBD5D1` | borda padrão |
| `--color-border-strong` | `#94A39D` | separação forte e controles ativos |
| `--color-text` | `#17221E` | texto principal |
| `--color-text-muted` | `#52615B` | texto secundário; validar contraste em 14 px |
| `--color-success` | `#15803D` | saudável, online, concluído |
| `--color-success-subtle` | `#EAF7EE` | fundo de sucesso |
| `--color-warning` | `#B45309` | degradado, atenção, truncamento |
| `--color-warning-subtle` | `#FFF4E5` | fundo de warning |
| `--color-danger` | `#B42318` | falha, offline, destructive |
| `--color-danger-subtle` | `#FDECEA` | fundo de erro |
| `--color-info` | `#1D4ED8` | informação, operação em andamento |
| `--color-info-subtle` | `#EAF0FF` | fundo informativo |
| `--color-neutral-status` | `#475569` | unknown/indeterminado |
| `--color-focus` | `#2563EB` | anel de foco, independente da marca |

Regras:

- success, warning, danger e info são estados, não decoração;
- offline usa danger; unknown usa neutral; warning fica reservado para degradação/atenção;
- badges sólidos usam branco somente após validação WCAG AA;
- estados sutis usam texto semântico escuro sobre fundo correspondente;
- gráficos usam uma paleta categórica própria (`--chart-1` a `--chart-8`) e também padrões de traço/símbolo; não reutilizar cores de status para identificar séries;
- bordas não devem ser a única indicação de foco ou erro.

### Spacing

Escala baseada em 4 px:

| Token | Valor |
|---|---:|
| `--space-0` | `0` |
| `--space-0-5` | `2px` |
| `--space-1` | `4px` |
| `--space-2` | `8px` |
| `--space-3` | `12px` |
| `--space-4` | `16px` |
| `--space-5` | `20px` |
| `--space-6` | `24px` |
| `--space-8` | `32px` |
| `--space-10` | `40px` |
| `--space-12` | `48px` |
| `--space-16` | `64px` |

Usar 8 px como ritmo mais comum; 4 px apenas para relações estreitas entre label/valor, e 24–32 px entre seções.

### Radius

| Token | Valor | Uso |
|---|---:|---|
| `--radius-sm` | `4px` | chips técnicos e elementos pequenos |
| `--radius-md` | `8px` | inputs, botões e cards compactos |
| `--radius-lg` | `12px` | painéis principais/overlays |
| `--radius-full` | `999px` | badges de status |

Raio não comunica hierarquia sozinho. Evitar arredondamento excessivo em tabelas e superfícies operacionais.

### Shadow

| Token | Valor | Uso |
|---|---|---|
| `--shadow-none` | `none` | painel padrão separado por borda |
| `--shadow-sm` | `0 1px 2px rgb(23 34 30 / 8%)` | card interativo |
| `--shadow-md` | `0 6px 18px rgb(23 34 30 / 12%)` | menu, popover |
| `--shadow-lg` | `0 16px 40px rgb(23 34 30 / 16%)` | modal somente |

Cards de conteúdo devem preferir borda + `shadow-none`; elevação fica reservada a elementos realmente acima do plano.

### Typography

Usar fontes do sistema para previsibilidade, privacidade e compatibilidade com a CSP:

```css
--font-sans: Inter, ui-sans-serif, system-ui, -apple-system, "Segoe UI", sans-serif;
--font-mono: ui-monospace, "Cascadia Code", "SFMono-Regular", Consolas, monospace;
```

Inter só deve ocupar a primeira posição se for auto-hospedada. Caso contrário, iniciar em `ui-sans-serif`.

| Papel | Tamanho / linha | Peso | Uso |
|---|---|---:|---|
| `display-sm` | 32/40 px | 700 | título de autenticação, uso raro |
| `heading-lg` | 24/32 px | 700 | título de página |
| `heading-md` | 20/28 px | 600 | título de seção/card |
| `body-md` | 16/24 px | 400 | conteúdo geral |
| `body-sm` | 14/20 px | 400 | tabelas e conteúdo secundário |
| `label` | 14/20 px | 600 | labels e colunas |
| `caption` | 12/16 px | 500 | metadados não essenciais |
| `data-lg` | 28/32 px | 650 | KPI |
| `code` | 13/20 px | 400 | IDs, tópicos e JSON |

Valores tabulares, durações e timestamps devem usar `font-variant-numeric: tabular-nums`. Não usar caixa alta extensa em labels.

### Container width e layout

| Token | Valor | Uso |
|---|---:|---|
| `--container-reading` | `760px` | formulários e texto longo |
| `--container-content` | `1200px` | páginas comuns |
| `--container-wide` | `1600px` | dashboards, tabelas e Explorer |
| `--page-gutter` | `clamp(16px, 3vw, 32px)` | margem horizontal |
| `--header-height` | `64px` | header desktop |
| `--nav-width` | `240px` | navegação desktop |

O `MainContent` escolhe o container conforme a tarefa; não limitar gráficos/tabelas ao container de leitura.

## Componentes básicos

### Button

Contrato:

- variantes: `primary`, `secondary`, `ghost`, `danger`;
- tamanhos: `sm` (32 px), `md` (40 px) e `lg` (44 px);
- estados: default, hover, focus-visible, active, disabled e loading;
- `loading` mantém largura, mostra indicador, define `aria-busy=true` e bloqueia cliques;
- `disabled` usa cursor default/not-allowed; `wait` somente em loading;
- ícone nunca substitui accessible name;
- links com aparência de botão usam o mesmo primitive, renderizado como link, sem duplicar classes.

### Input

Composto por `Field`, `Label`, controle, hint e erro. Deve aceitar prefixo/sufixo (por exemplo, unidade), preservar `id`, `name`, autocomplete e tipo nativo. Erro define `aria-invalid=true` e referencia texto por `aria-describedby`. Altura padrão: 40 px; textarea é exceção.

### Select

Segue o contrato de `Input`. Preferir select nativo enquanto busca/multiseleção não for necessária. Para listas longas do Explorer, criar um componente de seleção pesquisável próprio apenas em etapa posterior e com navegação completa por teclado.

### Card

Partes: `Card`, `CardHeader`, `CardBody`, `CardFooter`. Variantes:

- `default`: seção com borda;
- `compact`: listas e dados densos;
- `metric`: label, valor, unidade, tendência/freshness opcional;
- `interactive`: foco e hover, somente quando todo o card é acionável.

Não aninhar cards por hábito. Alertas e estados não devem usar `Card` quando `ErrorState`/`EmptyState` forem semanticamente corretos.

### Badge

Para categorias e metadados sem estado crítico. Variantes `neutral`, `primary` e `info`; tamanhos `sm/md`. Texto curto, sem interação e sem depender apenas de cor.

### StatusBadge

Contrato semântico central para dispositivos e saúde:

```ts
type StatusTone = "success" | "warning" | "danger" | "info" | "neutral";
type StatusBadgeProps = {
  label: string;
  tone: StatusTone;
  detail?: string;
  showDot?: boolean;
};
```

Mapeamento de domínio fica fora do componente:

- Online/Healthy/Operational → success;
- Degraded → warning;
- Offline/Unhealthy/Failed → danger;
- Checking/Processing → info;
- Unknown/Not reported → neutral.

Exibir sempre texto; o ponto/ícone é redundante à cor. Status desconhecido recebido da API deve cair em neutral, nunca em success.

### PageHeader

Estrutura:

- breadcrumb opcional;
- título único (`h1`);
- descrição/contexto;
- metadados como workspace e “atualizado em”;
- área de ações primária/secundária.

Em mobile, ações descem para uma linha própria e permanecem na ordem de prioridade.

### EmptyState

Props: `title`, `description`, `primaryAction?`, `secondaryAction?`, `size: compact|page`. Deve explicar o estado e o próximo passo. Não usar ilustrações grandes em telas operacionais. Exemplo: “Nenhum dispositivo cadastrado” + “Cadastrar dispositivo”.

### LoadingState

Variantes:

- `inline`: texto/indicador ao lado de uma pequena região;
- `skeleton`: mantém a geometria de cards/listas;
- `page`: apenas no primeiro carregamento sem conteúdo anterior.

Usar `role=status`, `aria-live=polite` e texto acessível. Durante refresh, manter os dados e sinalizar “Atualizando”, em vez de apagar o conteúdo.

### ErrorState

Props: `title`, `message`, `retry?`, `traceId?`, `scope: inline|section|page`. Usa `role=alert` para erro novo. Deve diferenciar:

- validação: junto ao campo;
- falha recuperável: retry e preservação de dados anteriores;
- autorização/sessão: orientação específica;
- timeout/limite: ação recomendada;
- falha fatal da página: estado de página.

Detalhes técnicos e trace ID podem ser expansíveis e copiáveis, sem expor dados sensíveis.

## Componentes operacionais recomendados

Não fazem parte do núcleo mínimo obrigatório, mas evitam duplicação imediata:

- `DataTable`: caption, headers com scope, densidade, scroll e estado vazio;
- `MetricCard`: valor, unidade, status, freshness e link de detalhe;
- `Timestamp`: formato local, timezone explícito, relativo opcional e UTC no detalhe;
- `TelemetryChart`: título semântico, eixo/unidade, legenda, resumo e tabela alternativa;
- `FilterPanel`: filtros, contador ativo, limpar e aplicar;
- `WorkspaceSwitcher`: contexto global sem derivar dados do pathname;
- `CopyableCode`: IDs, secrets e tópicos com quebra segura e ação copiar;
- `DefinitionList`: pares label/valor em detalhes de dispositivo.

## Estados e linguagem

Vocabulário de UI recomendado em português:

- “Desconhecido” em vez de “Unknown”;
- “Média”, “Mínimo”, “Máximo”, “Soma”, “Contagem”, “Último” como labels, mantendo valores técnicos da API internamente;
- “Intervalo” em vez de “Bucket”, com o valor técnico como ajuda contextual;
- “Data e hora” em vez de “Timestamp” quando não for uma coluna técnica;
- nomes de produto consolidados, como “Telemetry Explorer”, podem permanecer em inglês se tratados como nome próprio.

Mensagens devem dizer o que ocorreu e o que fazer. Evitar “Ocorreu um erro inesperado” como única informação quando a API oferece detalhe seguro.

## Tempo, números e unidades

- formato principal: locale `pt-BR`, timezone visível (`BRT`, `UTC−03:00` ou preferência futura);
- tooltip/detalhe: ISO 8601 UTC para diagnóstico;
- datas relativas (“há 3 min”) sempre acompanhadas do timestamp absoluto acessível;
- duração: `125 ms`, `2,4 s`, `3 min`, com precisão adequada;
- valores: `Intl.NumberFormat("pt-BR")`, precisão definida pela métrica;
- unidade: usar `canonicalUnit`; “sem unidade” deve ser informação explícita, não string vazia;
- atraso: quando ambos existirem, mostrar diferença entre ocorrência e ingestão sem reclassificar status no frontend.

## Acessibilidade e responsividade

- alvo mínimo de 44 × 44 px para ações principais em touch; 40 px aceito em tabelas desktop densas;
- `:focus-visible` global com anel de 2 px e offset de 2 px;
- contraste WCAG AA: 4.5:1 para texto normal, 3:1 para texto grande e componentes gráficos essenciais;
- não depender apenas de cor, posição, hover ou tooltip;
- ordem DOM deve permanecer lógica em todos os breakpoints;
- navegação desktop lateral vira drawer/menu no mobile, com controle nomeado e foco gerenciado;
- tabelas mantêm scroll horizontal, primeira coluna identificável e alternativa em cards somente quando necessário;
- respeitar zoom de 200%, `prefers-reduced-motion` e preferências de contraste quando possível.

## Organização proposta

```text
src/
├── components/
│   ├── app-shell/
│   ├── data-display/
│   ├── feedback/
│   ├── forms/
│   └── navigation/
├── styles/
│   ├── tokens.css
│   ├── reset.css
│   ├── globals.css
│   └── utilities.css
└── pages/
```

Começar com CSS global organizado e classes com nomes de componente. CSS Modules pode ser adotado gradualmente se colisões surgirem; não é pré-requisito. Um catálogo interno simples de estados dos componentes, coberto por testes, é suficiente antes de considerar Storybook ou biblioteca externa.

## AppShell implementado

Primeira implementação compartilhada criada em 2026-08-08:

```text
AppShell
├── SkipLink
├── AppHeader
│   ├── marca Fluxo / identificação Portal IoT
│   ├── usuário autenticado
│   └── logout
├── AppNavigation
│   ├── Workspaces
│   ├── Dashboard
│   ├── Dispositivos
│   ├── Telemetry Explorer
│   └── Saúde da plataforma
└── MainContent
    └── PageHeader + conteúdo da rota
```

Componentes em `portal-web/src/components`:

- `AppShell`: resolve o contexto de workspace, compõe as regiões compartilhadas e preserva o `Outlet` do React Router;
- `AppHeader`: apresenta marca, produto, conta e logout sem recriar a identidade visual;
- `AppNavigation`: mantém o mesmo conjunto e ordem de destinos em todas as páginas autenticadas;
- `MainContent`: fornece landmark, alvo do skip link, largura máxima e gutters comuns;
- `PageHeader`: fornece um único `h1`, descrição opcional e área futura de ações.

Decisões desta etapa:

- a identidade verde e o gradiente existentes foram mantidos;
- a navegação permanece horizontal para minimizar ruptura nesta primeira entrega e usa overflow horizontal controlado em viewports estreitas;
- Dashboard, Dispositivos e Explorer ficam visíveis, porém semanticamente indisponíveis, enquanto não existe workspace selecionado;
- a seleção de workspace em `sessionStorage` é sincronizada entre consumidores do hook para manter os destinos estáveis ao navegar por Saúde e Workspaces;
- `NavLink` fornece `aria-current="page"`; o estado atual também usa peso, fundo e marcador inferior, sem depender apenas de cor;
- links possuem hover, active e focus-visible; itens indisponíveis não simulam links clicáveis;
- um skip link permite alcançar diretamente `MainContent` por teclado;
- o header não empilha: email usa truncamento, logout não encolhe e a identificação do produto permanece visível;
- painéis e filhos de grids recebem `min-width: 0`/`max-width: 100%` para não extrapolar a viewport;
- nenhuma biblioteca visual foi adicionada e nenhum conteúdo interno de página foi redesenhado.

## Tokens, StatusBadge, Timestamp e Saúde da plataforma implementados (2026-08-08)

Segunda entrega incremental sobre o AppShell, cobrindo a Etapa 1 do roadmap por completo e partes das Etapas 3, 6 e 7 (escopo: Saúde da plataforma, Workspaces, Dispositivos, Detalhes do dispositivo, Login/Register e consistência global de CSS).

Implementado nesta etapa:

- `portal-web/src/styles/tokens.css`: todos os tokens de cor, spacing, radius, shadow, tipografia e container descritos acima, com os valores exatos deste documento;
- `styles.css` migrado para consumir os tokens (nomes legados como `--brand`, `--online` etc. viraram aliases que resolvem para os tokens novos, para não exigir uma reescrita de todas as classes numa única entrega);
- import remoto do Google Fonts removido; tipografia usa a pilha de sistema (`--font-sans`/`--font-mono`) prevista para quando não há fonte auto-hospedada;
- `:focus-visible` consolidado num único contrato de 2 px/2 px em todo o portal (inputs, botões, links, opções do Explorer);
- cards/painéis (`.panel`, `.auth-card`) passaram a usar borda + `--shadow-none` em vez de sombra pesada, com radius por camada (`--radius-lg` em painéis, `--radius-md` em inputs/botões/campos, `--radius-full` em badges/pills);
- `StatusBadge` (`components/StatusBadge.tsx`): implementação mínima do contrato `{ label, tone, detail? }` já documentado acima, renderizada sobre as classes `.status-badge.<tone>`;
- `Timestamp` (`components/Timestamp.tsx`) e `formatHumanTimestamp` (`utils/formatTimestamp.ts`): apresentação humana única (`dd/mm/aaaa hh:mm:ss`, pt-BR) com o ISO original disponível via `title`/`dateTime`; os formatadores previamente duplicados em `metricPresentation.ts` e `TelemetryActivity.tsx` agora delegam para essa função;
- `utils/healthStatus.ts`: mapeamento dos estados reais do backend (`Healthy`/`Degraded`/`Unhealthy`) para a taxonomia padronizada `Healthy`/`Degraded`/`Unavailable`/`Unknown`, e `deriveOverallStatus` — o status geral da página Saúde é sempre calculado a partir dos componentes recebidos, nunca herdado diretamente do campo `status` de nível superior (que por padrão do ASP.NET `HealthCheckService` retorna "Healthy" quando zero checks estão registrados);
- `HealthPage` reescrita sobre `LoadingState`/`ErrorState`/`EmptyState`, com retry no erro e um `EmptyState` explícito quando não há componente monitorado (nenhum dado é inventado);
- `WorkspacePage`, `DevicesPage` e a lista de telemetria em `DeviceDetailsPage` migradas para os mesmos primitives de feedback, com estados de carregamento/vazio/erro distintos.

Ainda não implementado (fora do escopo desta entrega, ver roadmap): `Button`/`Field`/`Input`/`Select` como componentes React com variantes formais (Etapa 2), `DataTable` (Etapa 8), dark mode.

## UX cleanup + fundação de marca (2026-08-08, segunda entrega)

Consolidação sobre a entrega anterior — sem redesign, sem mudança de backend/contratos. Detalhes completos em [`brand-foundation.md`](brand-foundation.md) e [`interaction-guidelines.md`](interaction-guidelines.md).

- **Correção de foco:** `.main-content:focus` (alvo do skip link) usava um anel bespoke de 3px com offset negativo, cobrindo a área de conteúdo inteira — o "contorno azul em bloco grande" relatado. Corrigido para `:focus-visible` com o anel padrão de 2px. Causa e reprodução documentadas em `interaction-guidelines.md`.
- **Camada semântica de marca** adicionada em `tokens.css`: `--brand-deep`, `--brand-primary`, `--brand-accent`, `--signal` (+ `-subtle`), `--surface`, `--canvas`, `--border`, `--text`, `--muted`, e aliases `--success`/`--warning`/`--danger`/`--info`/`--unknown`. Regras de uso em `brand-foundation.md`.
- **Estados de interação padronizados**: hover real em botões (antes ausente), `cursor: not-allowed` em disabled (era `wait`, incorreto), `accent-color` de marca em checkboxes/radios, e os três tratamentos divergentes de "selecionado" (`.button-secondary.active`, `.explorer-preset.is-active`, `.explorer-option:has(input:checked)`) unificados na mesma fórmula (borda + fundo sutil `--brand-accent`, nunca bloco sólido).
- **Tipografia**: quatro tamanhos quase-duplicados de `h2` (1.05/1.08/1.12/1.15rem) consolidados em `--text-card-title-size`; títulos de seção sem estilo próprio (`.panel > h2` em Workspaces/Health/Device Details) padronizados em `--text-heading-md-size`; valor de KPI ancorado em `--text-data-lg-size`.
- **Densidade**: `min-height` de cards de KPI (132→104px) e atividade/saúde (190→150px) reduzido; margem do `.auth-card` (4rem→3rem).
- **Proof-of-concept da linguagem visual**: indicador `signal-pulse` (ponto ciano + anel, `--signal`, respeita `prefers-reduced-motion`) ao lado de "Última telemetria" no Dashboard quando há timestamp real — único elemento novo de JSX desta entrega, único uso de `--signal` em produção.

## Ajustes de paleta e apresentação de métricas (2026-08-08, terceira entrega)

- **Paleta de marca revisada** com valores fornecidos pelo produto: `--brand-deep` (#073d35), `--brand-accent` (#00a98f), `--signal` (#40d9d0); `--color-primary` (verde petróleo, #0b5c4b) foi **preservado sem alteração** por decisão explícita — a paleta nova constrói um tier ao redor dele, não o substitui. `--color-background`/`--color-border`/`--color-text` também ajustados para os tons neutros fornecidos (`#f4f7f5`/`#d8e1dd`/`#17211e`). O gradiente do header (`app-header-bar`), que usava hex literal e nunca havia sido migrado para o token `--brand-deep` na entrega anterior, agora referencia `var(--brand-deep)`/`var(--brand)` corretamente.
- **Nomes de métrica em pt-BR**: `metricPresentation.ts` ganhou `curatedMetricNames`, um dicionário de nomes revisados para as métricas reais em uso (`temperature_c` → "Temperatura", `humidity_percent` → "Umidade", `memory_used_percent` → "Memória utilizada" etc.), consumido por `metricDisplayName()` antes do fallback genérico `humanizeMetricKey()`. Chaves de métrica não mapeadas continuam caindo no fallback em inglês — nenhuma tradução é inventada para métricas futuras/desconhecidas (mantém o contrato do teste existente sobre `humanizeMetricKey`). Puramente apresentacional: não altera `canonicalUnit`, `valueType` nem nenhum contrato de API.
- **Badge "Unidade não informada"** deixou de usar o tom de warning (laranja) — a ausência de unidade é metadado, não um alerta; agora usa o mesmo estilo neutro das badges de tipo (`Numérica`/`Booleana`/`Texto`).
- **Login/Register**: o card não usava mais margem superior fixa (`3rem auto`) — em telas grandes ele ficava perto do topo com um vazio enorme abaixo. `body:has(.auth-card)` agora centraliza o card verticalmente e horizontalmente, e ganhou a primeira aplicação real da linguagem visual documentada em `brand-foundation.md` ("trilha sutil atrás do card de login"): um `::before` fixo com dois glows radiais suaves (`--brand-accent`/`--brand-deep`, 8–10% de opacidade) e linhas diagonais finas em 5% de opacidade — puramente CSS, `pointer-events: none`, sem SVG/imagem nova.

## Governança mínima

- novos valores visuais devem usar token existente ou justificar novo token;
- novos estados assíncronos usam os primitives de feedback;
- componente compartilhado exige teste de comportamento/semântica, não snapshot massivo;
- mudanças de status, tempo e unidade exigem revisão conjunta de produto e domínio;
- nenhuma decisão visual deve alterar contrato ou interpretação de telemetria.
