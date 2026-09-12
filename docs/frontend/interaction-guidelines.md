# Diretrizes de interação do Fluxo Portal

Este documento registra o contrato de estados interativos (`hover`, `focus-visible`, `active`, `selected`, `disabled`) por tipo de componente, e documenta o incidente de foco resolvido em 2026-08-08 como exemplo do que evitar.

## Incidente resolvido: contorno azul em bloco grande

**Sintoma relatado:** após certas interações, uma grande área da página (o container de conteúdo inteiro, abaixo do header) recebia um contorno visual azul.

**Causa técnica:** `.main-content:focus` em `styles.css` aplicava `outline: 3px solid var(--focus); outline-offset: -3px;` ao `<main id="main-content" tabIndex={-1}>` (`portal-web/src/components/MainContent.tsx`) — o alvo do skip link (`AppShell.tsx`). Isso é comportamento de acessibilidade legítimo (WCAG 2.4.1 — o alvo de um skip link precisa mostrar visualmente que recebeu foco), mas usava `:focus` (não `:focus-visible`) com uma regra própria de 3px/-3px, inconsistente com o anel de foco de 2px usado no resto da aplicação. Como `<main>` ocupa a largura inteira da página, o anel de 3px inset ficava visualmente enorme — exatamente o efeito "bloco grande" relatado. Reproduzido de forma determinística clicando o skip link com mouse real e inspecionando `document.activeElement`/`getComputedStyle` — não ocorre em nenhuma outra interação (botões, links de navegação, cards) testada.

**Correção:** `.main-content:focus` → `.main-content:focus-visible`, anel reduzido para `2px solid var(--color-focus)` / `outline-offset: -2px`, igual ao padrão usado em todo o resto do app.

**Lição para novos componentes:** nunca criar uma regra de foco própria por componente. Usar sempre `:focus-visible` (não `:focus` puro) e o token de foco padrão. Se um elemento realmente precisa de um tratamento de foco maior que o padrão (landmarks, containers grandes), isso deve ser uma decisão consciente e documentada aqui — não um efeito colateral esquecido.

## Matriz de estados por componente

Todos os tempos de transição usam `--transition-fast` (120ms). Cor nunca é o único sinal de estado — texto, peso de fonte ou posição sempre acompanham.

### Button (primário e secundário)

| Estado | Tratamento |
|---|---|
| default | `--brand-primary` (primário) / `--surface-strong` + borda (secundário) |
| hover | `--brand-primary-hover` (primário) / `--color-surface-subtle` + borda mais forte (secundário) |
| `:focus-visible` | anel de 2px, token de foco padrão |
| active (clique) | herdado do navegador, sem tratamento customizado adicional |
| selected (`.button-secondary.active`) | borda + texto em `--brand-accent`, fundo `--color-primary-subtle`, `font-weight: 600` — nunca herda o hover genérico (regra `:not(.active)` evita que o hover "desselecione" visualmente) |
| disabled | `opacity: .65`, `cursor: not-allowed` (nunca `wait` — `wait` é reservado para operações de carregamento, não para indisponibilidade) |

### Link / Nav

| Estado | Tratamento |
|---|---|
| default | cor de texto padrão, sem sublinhado |
| hover | sublinhado (link de corpo) / fundo `--surface-strong` (nav) |
| `:focus-visible` | anel de 2px |
| active/current (`aria-current="page"`) | `--brand-primary` — cor de navegação, nunca `--brand-accent` (ver `brand-foundation.md`: navegação e seleção de conteúdo são conceitos diferentes) |
| disabled (nav sem workspace) | `--text-muted`, `cursor: not-allowed`, sem simular link clicável |

### Cards

Cards informativos (KPI, atividade, saúde do workspace) não são clicáveis e não têm estados de interação — só o conteúdo interno (botões/links) os tem. Cards genuinamente clicáveis (`.explorer-option`) seguem:

| Estado | Tratamento |
|---|---|
| default | borda neutra, fundo `--surface` |
| hover | borda levemente mais forte, fundo sutil |
| `:focus-visible` (no input interno) | anel de 2px no card inteiro (`:has(input:focus-visible)`) |
| selected (`:has(input:checked)`) | borda + fundo `--brand-accent`/`--color-primary-subtle` — tratamento leve, não um bloco de cor sólida cobrindo o card |

### Inputs / Select

| Estado | Tratamento |
|---|---|
| default | borda `--color-border`, fundo `--surface` |
| `:focus` | borda muda para `--color-primary` (mudança sutil e imediata, complementar ao anel de `:focus-visible` do navegador) |
| erro (`.field-error`) | borda `--danger` + outline sutil vermelho |
| disabled | não usado atualmente no portal — se necessário, seguir o mesmo padrão de botão (`opacity: .65`, `cursor: not-allowed`) |

### Checkbox / Radio

`accent-color: var(--brand-accent)` global — a marca de seleção nativa do navegador usa a cor de marca em vez do azul padrão do sistema operacional, evitando colisão visual com o anel de foco (que é azul, `--color-focus`).

### Tabs (navegação principal)

Já cobertos em "Link / Nav" — a navegação principal do AppShell é implementada como uma lista de links, não um componente de abas separado.

## Regra geral de "selected"

Selecionar algo deve ser lido rapidamente sem transformar o elemento inteiro em um bloco de cor. O padrão em toda a aplicação: **borda + fundo sutil (`--color-primary-subtle`) + peso de fonte maior**, nunca fundo sólido saturado cobrindo o elemento inteiro. Essa é a mesma regra aplicada retroativamente a `.button-secondary.active`, `.explorer-preset.is-active` e `.explorer-option:has(input:checked)` em 2026-08-08, unificando três tratamentos antes divergentes.
