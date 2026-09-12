# Auditoria de UI do Fluxo Portal

**Data:** 2026-08-08

**Escopo:** `portal-web` no branch `fix/portal-same-origin-auth`

**Natureza:** análise e arquitetura; nenhuma funcionalidade, API ou regra de negócio foi alterada

## Resumo executivo

O portal cobre o fluxo funcional do MVP e já possui uma base visual reconhecível: verde como cor de marca, superfícies claras, cards, formulários consistentes e visualização de telemetria separada por tipo e unidade. A interface ainda se comporta, porém, como um conjunto de telas de MVP, não como uma console operacional escalável.

Os maiores riscos de UX são:

1. a navegação não expõe de forma persistente Dashboard e Dispositivos no contexto do workspace;
2. datas UTC são mostradas em formato bruto em várias telas, enquanto o Explorer usa horário local sem declarar fuso;
3. estados de carregamento, erro e vazio são mensagens locais sem padrão, ação de recuperação ou semântica assistiva;
4. status de dispositivo, saúde da plataforma e contadores do dashboard usam representações diferentes;
5. o Explorer tem boa separação por tipo/unidade, mas filtros densos, legenda limitada, eixos sem unidade explícita e gráficos sem alternativa acessível;
6. tokens existentes são parciais: cores e um raio/sombra estão centralizados, mas bordas, foco, espaçamento, tipografia e cores dos gráficos continuam literais.

A recomendação é evoluir por camadas: tokens, primitives, estados, shell/navegação e, somente então, telas. O redesign completo não deve ser feito em uma única entrega.

## Método e limites

Foram inspecionados rotas, páginas, componentes, contratos TypeScript, testes do Explorer, `styles.css`, configuração Vite/nginx e documentação do baseline. A conexão com o navegador automatizado não estava disponível no ambiente; portanto, as conclusões visuais e responsivas foram derivadas do DOM/JSX e das regras CSS, não de uma sessão autenticada renderizada. Essa limitação não afeta o inventário estrutural, mas recomenda-se validar visualmente os breakpoints quando a implementação começar.

## Inventário atual

### Stack visual

- React 18, React Router 6 e TypeScript;
- Vite como build tool;
- Recharts 2 para séries temporais;
- um stylesheet global, `portal-web/src/styles.css`, com 269 linhas;
- fontes declaradas: Space Grotesk e Source Code Pro via Google Fonts, com fallback para Segoe UI/monospace;
- a CSP de produção permite apenas fontes locais; portanto, o import externo é bloqueado e o visual efetivo depende do fallback do sistema;
- nenhuma biblioteca de componentes ou ícones.

### Rotas e páginas

| Rota | Página | Layout | Conteúdo visual principal |
|---|---|---|---|
| `/` | redirecionamento | — | login ou workspaces conforme sessão |
| `/login` | `LoginPage` | isolado | card de autenticação e formulário |
| `/register` | `RegisterPage` | isolado | card de autenticação e formulário |
| `/workspaces` | `WorkspacePage` | `AppLayout` | lista de workspaces e criação lado a lado |
| `/workspaces/:id/dashboard` | `DashboardPage` | `AppLayout` | sete cards de métricas |
| `/workspaces/:id/devices` | `DevicesPage` | `AppLayout` | lista de dispositivos e ações |
| `/workspaces/:id/devices/new` | `NewDevicePage` | `AppLayout` | formulário e credencial de uso único |
| `/workspaces/:id/devices/:deviceId` | `DeviceDetailsPage` | `AppLayout` | dados, provisionamento e telemetria recente |
| `/workspaces/:id/explorer` | `TelemetryExplorerPage` | `AppLayout` | filtros, gráficos e tabela |
| `/status` | `HealthPage` | `AppLayout` | estado geral e componentes da plataforma |
| `*` | `NotFoundPage` | isolado | mensagem e link de retorno |

### Layout e navegação

`AppLayout` reúne:

- `.app-shell`: container central com largura máxima de 1520 px;
- `.topbar`: marca, subtítulo, email e logout;
- `.nav-links`: Workspaces, Telemetry Explorer quando há ID na URL e Saúde da plataforma;
- `.content`: região principal em grid.

Dashboard e Dispositivos não aparecem na navegação persistente. O contexto de workspace é inferido por expressão regular sobre o pathname. Não existe indicação de rota ativa, breadcrumb, seletor de workspace, navegação móvel dedicada ou link “pular para conteúdo”.

### Componentes compartilhados

| Componente | Responsabilidade atual | Limite observado |
|---|---|---|
| `AppLayout` | shell autenticado | concentra header e nav, mas sem hierarquia global/workspace |
| `ProtectedRoute` | proteção de rotas | não possui impacto visual próprio |
| `ApiErrorMessage` | texto de erro | mensagem genérica, sem `role=alert`, título, retry ou trace ID |
| `DeviceStatusBadge` | online/offline/unknown | específico de dispositivo e não reutilizado na saúde/dashboard |

Os demais padrões são classes CSS aplicadas diretamente nas páginas, não componentes: `panel` (22 usos), `metric-card` (7), `muted` (6), `form-grid`, `list`, `list-item`, `button-link`, `button-secondary`, `success-box`, banners e estados textuais.

### Estilos globais e tokens existentes

Tokens atuais:

- fundos: `--bg-start`, `--bg-end`;
- superfícies: `--surface`, `--surface-strong`;
- texto: `--text-main`, `--text-muted`;
- marca: `--brand`, `--brand-contrast`, `--accent`;
- semântica parcial: `--danger`, `--online`, `--offline`, `--unknown`;
- elevação e forma: um único `--shadow` e `--radius`.

Há 20+ cores literais adicionais no CSS e cinco cores de série literais no componente do Explorer. Bordas repetem `#c2d4c8` e `#d4e3d9`; foco repete `#79b59e`; os espaçamentos usam valores isolados entre `.2rem` e `4rem`. Não existe escala formal.

### Tipografia

- Space Grotesk é declarada para toda a UI, mas indisponível em produção sob a CSP atual;
- Source Code Pro é usada em `pre` e `code`, sujeita ao mesmo bloqueio;
- apenas marca (`1.2rem`) e valor de métrica (`1.6rem`) têm tamanho explícito;
- títulos dependem do estilo padrão do navegador;
- não há tokens de peso, line-height, tracking ou escala responsiva;
- textos técnicos, timestamps e identificadores longos não têm política consistente de quebra/truncamento.

### Espaçamento, raio e sombra

- gaps mais comuns: `0.5`, `0.6`, `0.75`, `0.8` e `1rem`;
- padding de cards: `1rem 1.2rem`, reduzido para `.85rem` em mobile;
- raios: 8, 10, 14 px e pill 999 px;
- todos os painéis usam sombra forte `0 12px 32px`, inclusive áreas de trabalho densas;
- não há distinção entre separação por borda, elevação de overlay e card interativo.

### Botões, inputs e selects

- botão primário e link-botão compartilham aparência por seletores globais;
- variante secundária é aplicada tanto a `button` quanto a `Link`;
- estado ativo existe apenas para o botão de workspace;
- todo `button:disabled` recebe `cursor: wait`, mesmo quando desabilitado por validação;
- não há variantes destructive/ghost/icon, tamanhos ou estado loading separado;
- inputs, textarea e select compartilham estilo e foco visível;
- labels envolvem os controles, o que preserva associação acessível básica;
- erros de formulário são globais; o único erro de campo (`bucket`) é predominantemente visual e sem `aria-describedby`/`aria-invalid`.

### Cards, listas e tabelas

- `panel` é usado como card universal para formulários, status, detalhes e gráficos;
- listas usam cards internos flexíveis em vez de tabela, mesmo para dispositivos e workspaces;
- a única tabela está no Explorer para texto/count, com scroll horizontal;
- headers de tabela não declaram `scope`;
- não há ordenação, paginação, densidade compacta, coluna de última atividade ou toolbar de filtros.

### Gráficos e telemetria

Pontos positivos:

- séries Numeric, Boolean e Text recebem representações diferentes;
- Boolean usa linha em degrau e domínio 0/1;
- séries são separadas por unidade canônica;
- resultados truncados, timeout, limites de intervalo e séries vazias têm algum feedback;
- a linha não conecta lacunas (`connectNulls=false`).

Limites:

- título do painel expõe o tipo técnico (`Numeric`, `Boolean`, `Text`) antes do significado operacional;
- eixo Y não tem rótulo/unidade e valores não têm formatação/precisão definida;
- eixo X mostra apenas hora, ficando ambíguo em intervalos de múltiplos dias;
- timezone não é comunicado;
- legenda combina dispositivo e `metricKey`, mas pode crescer até 25 séries e não tem mecanismo de foco/ocultação;
- paleta tem cinco cores para potencialmente até 25 séries, causando repetição;
- distinção depende de cor; não há variação de traço/símbolo ou tabela equivalente para séries numéricas;
- metadados já recebidos (`totalPoints`, `executionTimeMs`, intervalo, agregação, bucket) não são apresentados;
- seleção em dois fieldsets roláveis exige varredura longa e não oferece busca, “selecionar todos visíveis” ou resumo removível;
- o resultado anterior é apagado ao executar nova consulta, eliminando contexto durante loading/erro;
- o código de visualização e a página estão altamente compactados, dificultando evolução segura, embora isso seja dívida de manutenção e não falha visual direta.

## Duplicações e inconsistências

| Tema | Evidência | Consequência |
|---|---|---|
| Page header | cada página renderiza `h1`, descrição e ações manualmente | espaçamento e hierarquia variam |
| Loading | textos “Carregando...”, “Consultando...” e rótulo de botão | layout salta e não há anúncio assistivo padronizado |
| Empty state | frases soltas, painel simples e CTA apenas em alguns casos | próximo passo nem sempre é claro |
| Error | `ApiErrorMessage` global + mensagens complementares locais | contexto e recuperação fragmentados |
| Success | `.success-box` usa borda laranja (`--accent`) | sucesso não usa a semântica verde esperada |
| Status | badge de dispositivo, badge neutro da saúde e números do dashboard | mesma semântica tem três linguagens visuais |
| Datas | ISO/UTC bruto em dashboard/detalhes; locale no Explorer | comparação temporal ambígua |
| Idioma | mistura português com Workspace, Dashboard, Device, Unknown, raw/count e Timestamp | vocabulário operacional inconsistente |
| Ações | `button`, `Link`, `.button-link`, `.button-secondary` | estados hover/focus/disabled não são uniformes |
| Cards | `.panel` atende todos os níveis de informação | pouca diferenciação entre seção, métrica e alerta |
| Cores | tokens parciais + literais no CSS/TSX | temas e conformidade ficam difíceis de manter |

## Avaliação de UX

### Hierarquia visual

Há uma hierarquia básica por `h1`, `h2` e painéis, mas falta um cabeçalho de página com contexto, descrição e ações consistentes. A sombra forte em todos os painéis reduz a diferença entre informação principal e secundária. No dashboard, todos os indicadores recebem o mesmo peso, embora offline, rejeições e recência da telemetria tenham maior relevância operacional.

### Navegação

A navegação global é rasa e não representa a arquitetura real do produto. O usuário chega a Dashboard e Dispositivos por links dentro do conteúdo; não há estado ativo nem nome do workspace atual. Em escala, esse padrão aumenta desorientação e torna novas áreas difíceis de encaixar.

### Legibilidade e densidade

A base clara e o texto escuro favorecem leitura. Em contrapartida, cards largos, sombras e listas verticais usam espaço demais para monitoramento de muitos dispositivos. Dados técnicos longos e JSON podem dominar a tela. A futura interface industrial deve oferecer densidade confortável por padrão e uma opção compacta para tabelas, sem reduzir fonte abaixo de 14 px para conteúdo essencial.

### Tempo e unidades

O portal não define uma política visual única para tempo. Deve distinguir explicitamente:

- horário do evento (`occurredAtUtc`);
- horário de ingestão (`ingestedAtUtc`);
- último contato;
- última telemetria recebida;
- idade relativa/freshness.

Exibir `08/08/2026 14:32:10 BRT` como valor principal e o UTC ISO em tooltip/detalhe técnico elimina ambiguidade. Para gráficos longos, o eixo deve incluir data nas transições de dia. Unidades canônicas devem acompanhar eixos, valores, tooltips e exports, nunca apenas o título do painel.

### Online, offline e unknown

`DeviceStatusBadge` inclui texto e cor, o que é positivo. Ainda faltam definição visível de freshness, timestamp da avaliação e linguagem consistente em português. `Unknown` deve ser neutro, não parecer warning sem explicação. Saúde da plataforma deve usar o mesmo componente semântico, com mapeamento controlado por domínio.

### Loading, erro e vazio

- loading: textos evitam tela em branco, mas não reservam espaço, não usam `aria-live` e não distinguem espera local de bloqueio de página;
- erro: não há retry, ação recomendada, preservação consistente do conteúdo anterior ou acesso ao trace ID;
- vazio: mensagens são compreensíveis, porém apenas o Explorer oferece CTA diretamente associado;
- submissão: trocar o texto do botão é adequado, mas falta indicador visual e prevenção de mudança de largura;
- disabled: o cursor de espera comunica processamento em controles apenas indisponíveis por pré-requisito.

### Responsividade

Existem breakpoints em 800 e 480 px, grids fluidos, quebra da topbar, listas empilhadas e scroll de tabela. Isso cobre o básico. Riscos restantes:

- nav apenas quebra linhas, sem priorização ou controle móvel;
- fieldsets do Explorer podem criar rolagens aninhadas;
- gráfico fixo em 340 px e legendas extensas competem em telas estreitas;
- cards de dashboard mudam de largura sem ordem de prioridade;
- ações secundárias e strings técnicas podem exceder a viewport;
- não há breakpoint intermediário dedicado a tablets/console em janela dividida.

### Acessibilidade

Pontos positivos: `lang=pt-BR`, landmarks `header/nav/main`, headings, labels envolvendo campos, fieldset/legend e texto junto às cores de status.

Pendências prioritárias:

- foco visível só é definido para inputs/selects/textarea, não para links e botões;
- não há skip link nem `aria-current` na navegação;
- erros e loading não usam `role=alert`, `role=status` ou regiões live;
- erros de campo não estão programaticamente associados aos controles;
- gráficos Recharts não possuem resumo, tabela alternativa ou descrição textual;
- séries se diferenciam somente por cor e repetem uma paleta de cinco cores;
- alvos interativos tendem a ficar abaixo de 44 px de altura;
- `th` não declara escopo e não há `caption` contextual;
- contraste deve ser validado em todos os estados, especialmente muted, warning, bordas e controles desabilitados;
- animação/transição futura deve respeitar `prefers-reduced-motion`;
- imports de fonte externos são bloqueados em produção e devem ser removidos ou auto-hospedados.

## Priorização

### P0 — antes de escalar o uso operacional

- convenção única de tempo/fuso e unidades;
- navegação persistente do workspace com estado ativo;
- componentes compartilhados de loading, erro, vazio e status;
- foco visível, anúncios assistivos e alternativa tabular/resumida para gráficos;
- semântica consistente para offline, unknown, degradação e falha.

### P1 — ganho de clareza e escala

- tokens completos e primitives;
- PageHeader e arquitetura AppShell;
- dispositivos/workspaces em representação tabular responsiva;
- filtros pesquisáveis e resumo de seleção no Explorer;
- metadados da consulta e estado anterior preservado durante atualização.

### P2 — refinamento

- densidade configurável;
- ícones próprios e microinterações discretas;
- preferência de timezone;
- visual regression e documentação interativa de componentes.

## Critérios para a evolução

- manter contratos de API e lógica de telemetria intactos;
- não derivar online/offline no frontend: apenas representar o estado recebido;
- manter Recharts até existir necessidade comprovada de troca;
- preferir CSS custom properties e componentes React pequenos a uma biblioteca visual grande;
- todo incremento deve ser revisável, testável e reversível isoladamente;
- testar desktop, 1024 px, 768 px e 375 px, teclado e contraste antes de promover cada tela.
