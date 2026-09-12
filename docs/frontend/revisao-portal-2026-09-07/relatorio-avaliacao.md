# Revisão do Front-end do Fluxo Portal — avaliação, proposta priorizada e POC

**Data:** 2026-09-07
**Escopo:** `Fluxo/portal-web` no branch `fix/portal-same-origin-auth` (HEAD `075c27f`)
**Natureza:** análise, avaliação e prova de conceito. **Nenhum arquivo do portal oficial foi alterado.** Nenhum commit, push ou deploy foi feito.
**Ambiente avaliado:** stack local em Docker já em execução (`fluxo-api` :5000, `fluxo-portal-web` :8080, `fluxo-worker-ingestion`, `fluxo-mosquitto`), com dados reais de dois workspaces (`Fase 2 QA` e `Gateway Pi Pilot`, este último com o dispositivo físico `Edgewarden Gateway`).

---

## 1. Resumo executivo

O portal já é uma **console de MVP competente**: tem sistema de tokens implementado (`styles/tokens.css`), primitives de feedback (`LoadingState`/`EmptyState`/`ErrorState`), `StatusBadge`/`Timestamp` compartilhados, contrato único de `:focus-visible`, skip link, `aria-current` na navegação, tipografia de sistema (sem fonte externa bloqueada por CSP), cores de série estáveis por identidade, `connectNulls=false` e avisos de truncamento. Vários itens P0 da auditoria anterior (`docs/frontend/ui-audit.md`) foram de fato resolvidos.

O que **ainda impede o portal de funcionar como console operacional de observabilidade**:

1. **Sessão em memória** — qualquer refresh, deep-link ou compartilhamento de URL derruba o operador para o login. Inviável para uma tela de plantão. (P0)
2. **Não existe uma visão consolidada de métricas nativas do dispositivo.** As 15 métricas que o Gateway Pi emite (CPU, memória, temperatura, disco, uptime, conectividade, fila/spool, mensagens reenviadas/descartadas, rede) só aparecem como **JSON bruto repetido** na tela de Detalhes ou como **4 gráficos empilhados isolados** no Explorer. Não há como responder "o dispositivo está bem?" numa leitura. (P0/P1)
3. **Gráficos sem alternativa textual.** Cada painel do Explorer é `role="img"` com `aria-label="Gráfico de X"` e nada mais — sem tabela, sem resumo. Um operador com leitor de tela ou baixa visão não acessa nenhum valor de série numérica. (P0 — WCAG 1.1.1 / 1.4.5)
4. **Fuso não declarado no ponto de uso** em Detalhes; **eixo X do gráfico sem data** ao cruzar meia-noite; **eixo Y sem unidade** e com auto-domínio ruim (ex.: `environment.temperature_c ≈ 16 °C` plotado num eixo 0–120). (P1)
5. **O indicador `signal-pulse` ("dado ao vivo", ciano) fica pulsando para telemetria de 24 dias atrás.** Contradiz a própria regra de `brand-foundation.md` e passa confiança falsa. (P1)
6. **Sem tema** (nem escuro, nem alto contraste, nem escala tipográfica, nem redução de movimento acionável pelo usuário). Fundo em gradiente claro fixo, sem `prefers-color-scheme`. (P1/P2)
7. **Impacto visual baixo** — tudo é `.panel` branco com a mesma borda; hierarquia fraca entre "informação principal" e "secundária"; muito espaço desperdiçado (ação "Detalhes" a 1200 px de distância do nome do dispositivo). (P2)
8. **Erros de idioma sistemáticos** — "Ultimo contato", "Topico MQTT", "Rotacionar", "Unknown", "Pagina nao encontrada", "Senha e obrigatoria" — sem acentuação, o que também degrada leitores de tela em pt-BR. (P2)

A recomendação é **não fazer redesign de uma vez**. A proposta priorizada abaixo trata primeiro persistência de sessão + a visão consolidada de métricas nativas + acessibilidade de gráfico (curto prazo), depois tema e sistema visual (médio), depois refinamento e proteção contra regressão (longo).

A POC entregue (`Fluxo/mockups/portal-observabilidade-v2/`) demonstra a visão consolidada, os gráficos dinâmicos com múltiplas estratégias de escala, os temas combináveis e todos os estados (carregando, offline, dado atrasado, sem histórico) sem tocar no portal.

---

## 2. Metodologia e telas avaliadas

### Método

- Leitura do código real de `portal-web/src` (rotas, 9 páginas, ~20 componentes, `styles.css` + `tokens.css`, serviços de API, tipos TypeScript, 8 arquivos de teste).
- Leitura da documentação de front-end existente (`ui-audit.md`, `design-system.md`, `ui-roadmap.md`, `brand-foundation.md`, `interaction-guidelines.md`, `dashboard.md`, `telemetry-explorer.md`, `ui-release-checklist.md`).
- Leitura do que o dispositivo realmente emite: `devices/pi-gateway-reference-node/scripts/gateway-spool.js` (função `diagnostics()`), `scripts/mqtt-device-simulator.py`, ADR-0004.
- Leitura do contrato de backend relevante: `MetricDefinition` (Domain) e `TelemetryQueryContracts` (Application).
- **Execução real da stack local** e navegação autenticada (`phase2-qa@fluxo.local`) por todas as telas nos workspaces `Fase 2 QA` e `Gateway Pi Pilot`, incluindo consulta real no Explorer (`gateway.cpu_temperature_c`, `gateway.memory_used_percent`, `gateway.load_1m`, `environment.temperature_c`, 2.044 pontos).
- Verificação de layout/overflow/acessibilidade via árvore de acessibilidade e DOM em 1440 px, 1280 px e 375 px.
- `npm run test` (30/30 aprovados) e `npm run build` (aprovado; bundle 600 kB, acima do warning de 500 kB — pré-existente).

### Limitações da avaliação

- A captura de tela do painel de gráficos do Explorer ficou preta de forma intermitente no navegador embarcado (artefato de captura de SVG grande, **não** um bug do portal — o DOM e os valores foram confirmados por inspeção). As conclusões sobre o gráfico vêm do DOM/árvore de acessibilidade e do código, não de pixels.
- Não foi feita auditoria com ferramenta automatizada de contraste (axe/Lighthouse) nesta passagem; as observações de contraste vêm dos valores de token e devem ser confirmadas com ferramenta na Etapa de hardening.
- Teclado: o contrato `:focus-visible` global existe em `styles.css:72`; a navegação completa por teclado deve ser reconfirmada manualmente na implementação (o navegador embarcado não expõe Tab de forma confiável).

### Telas avaliadas

| Rota | Estado observado |
|---|---|
| `/login` | Card centralizado, textura sutil de marca. "Senha e obrigatoria" sem acento. Sessão perdida no refresh. |
| `/register` | Não testada a fundo (fluxo de conta inicial). |
| `/workspaces` | Lista + criação lado a lado. Botão "Selecionado — Abrir" com `aria-current`. Nome do workspace contém fragmento de ID (dado, não portal). |
| `/workspaces/:id/dashboard` | 6 KPIs, `TelemetryActivity`, síntese operacional. Flash de "Workspace 66451978" antes do nome resolver. `signal-pulse` ativo com dado de 24 dias. "Unknown" em inglês. Sem link para investigar rejeições. |
| `/workspaces/:id/devices` | Lista em cards. Só nome/identificador/status. Ação "Detalhes" separada por toda a largura da tela. Sem coluna de última atividade. |
| `/workspaces/:id/devices/new` | Formulário. "Provisonar", "Sessao invalida", "Metadata JSON invalido" — typos/acentos. |
| `/workspaces/:id/devices/:deviceId` | **JSON bruto de ~15 chaves repetido ~30 vezes**, cada um em `<pre>` de largura total. "Ultimo contato/telemetria/payload" sem acento. Rotação de credencial sem confirmação. Sem visão de métricas nativas. |
| `/workspaces/:id/explorer` | Fieldsets com scroll aninhado. 8 métricas no catálogo, todas "Unidade não informada". 4 métricas selecionadas = 4 gráficos isolados de ~490 px empilhados. Eixo X só hora; eixo Y sem unidade; `environment.temperature_c` num eixo 0–120. `role="img"` sem tabela/resumo. |
| `/status` | Só `postgresql` monitorado → "Healthy". Sem MQTT/Worker/API check (limitação de backend, divulgada honestamente). Label "Healthy" em inglês. "Última verificação: —". |
| `*` (404) | `<h1>Pagina nao encontrada</h1>` + link, **sem AppShell, sem estilo, sem acento**. |

---

## 3. Pontos fortes do baseline (preservar)

- `styles/tokens.css` implementado e consumido; `styles.css` legado aliasado (não reintroduzir hex literal).
- `LoadingState`/`EmptyState`/`ErrorState` com `role=status`/`role=alert`, retry onde a operação é segura de repetir, e EmptyStates com CTA (`DevicesPage`, `TelemetryExplorerPage`).
- `StatusBadge` com contrato de tom semântico; mapeamento de domínio fora do componente (`utils/healthStatus.ts`).
- `Timestamp` + `formatHumanTimestamp` — formato humano com ISO preservado em `title`/`dateTime`.
- `:focus-visible` global de 2 px (`styles.css:72`), incidente do "bloco azul" corrigido, skip link, `aria-current="page"` real na navegação, `min-width:0` defensivo contra overflow.
- Sem Google Fonts externo (CSP) — pilha de sistema.
- Explorer: cor de série estável por `deviceId+metricKey`; `connectNulls=false`; painéis separados por unidade/tipo canônico; Boolean como step-after; Text/count como tabela; aviso de truncamento; metadados da resposta (pontos, execução) exibidos.
- `deriveOverallStatus` na Saúde nunca confia no `status` de nível superior do ASP.NET.
- 30 testes de comportamento/semântica; `ui-release-checklist.md` como gate de PR.

Não recomendo trocar Recharts, adicionar biblioteca de componentes, nem introduzir dark mode "de qualquer jeito" — a proposta abaixo respeita as decisões de governança de `design-system.md`.

---

## 4. Achados por prioridade

Cada achado: **Evidência · Impacto para o usuário · Severidade · Recomendação · Esforço · Dependências (FE / BE / dispositivo)**.
Esforço: P = ½–1 dia · M = 2–4 dias · G = 1–2 semanas · GG = > 2 semanas.

### P0 — bloqueio crítico

#### P0-1. Sessão só em memória
- **Evidência:** `hooks/useAuth.tsx` guarda `token`/`user` em `useState`. Reproduzido: navegar para `http://localhost:8080/workspaces` com sessão ativa → volta para `/login`. Também citado em `project-status.md §7`.
- **Impacto:** operador que recarrega a página, abre um link de alerta em nova aba, ou compartilha a URL de um dispositivo com um colega — todos caem no login e perdem o contexto (workspace, filtro, período). Numa console de plantão isso é inaceitável.
- **Severidade:** crítica.
- **Recomendação:** persistir o token em `sessionStorage` (ou cookie `SameSite=Strict` se a API passar a emitir), reidratar no boot do `AuthProvider`, e tratar 401 com refresh/redirect preservando `returnTo`. Manter o segredo de provisionamento fora disso (já é).
- **Esforço:** P–M.
- **Dependências:** FE (principal). BE opcional (endpoint de refresh; hoje `expiresAtUtc` já vem no login).

#### P0-2. Gráficos sem alternativa textual (WCAG 1.1.1 / 1.4.5 / 1.4.1)
- **Evidência:** `TelemetrySeriesPanel.tsx:198` — `<div className="telemetry-chart" role="img" aria-label={`Gráfico de ${displayName}`}>`. Confirmado na árvore de acessibilidade: 4 painéis, todos `role="img"`, `hasTable:false`, nenhum resumo. Séries diferenciadas só por cor (paleta fixa, sem traço/símbolo).
- **Impacto:** usuários de leitor de tela, baixa visão extrema e daltônicos com muitas séries não conseguem ler os dados. Como o Explorer é a ferramenta de diagnóstico, isso exclui essas pessoas da operação.
- **Severidade:** crítica (conformidade).
- **Recomendação:** para séries Numeric/Boolean, adicionar (a) resumo textual gerado — "CPU: atual 42 °C, mín 41,9, máx 42,9, tendência estável, 0 lacunas"; (b) `<details>` com tabela de dados acessível (caption, `scope`), reusando o padrão que Text/count já usa; (c) diferenciar séries por padrão de traço além da cor quando houver > 4 séries. O gráfico continua `role="img"` mas com `aria-label` descritivo do comportamento.
- **Esforço:** M.
- **Dependências:** FE apenas.

#### P0-3. Não há visão consolidada de métricas nativas do dispositivo
- **Evidência:** o Gateway Pi emite por mensagem (`gateway-spool.js` → `diagnostics()`): `uptime_sec`, `cpu_temperature_c`, `memory_used_percent`, `disk_used_percent`, `load_1m`, `queue_depth`, `mqtt_connected`, `replayed_messages`, `dropped_messages`, `time_synchronized`, `network_interface_up`, `network_rx_bytes`, `network_tx_bytes` + `environment.temperature_c/humidity_percent`. No portal isso só aparece como JSON bruto em `DeviceDetailsPage` (`<pre>{safeJsonPreview(...)}` repetido por telemetria) ou como painéis isolados no Explorer.
- **Impacto:** a pergunta operacional central ("este dispositivo está funcionando? o que mudou? precisa de atenção?") não tem resposta numa tela. O operador lê JSON.
- **Severidade:** crítica para o objetivo de observabilidade (funcionalmente é P1 — o dado existe e é consultável).
- **Recomendação:** nova aba "Visão operacional" na página do dispositivo (detalhe na §7). Curto prazo: montar essa visão a partir do **último payload** (já disponível em `lastTelemetryPayloadJson`) + `getTelemetry`. Médio prazo: endpoint dedicado.
- **Esforço:** M (a partir do payload) → G (com endpoint + histórico por métrica na mesma tela).
- **Dependências:** FE (curto). BE (médio: endpoint consolidado, expor `MinExpectedValue`/`MaxExpectedValue`/`ExpectedIntervalSec` que **já existem no domínio** mas não no DTO). Dispositivo: nenhuma para o que já é enviado.

#### P0-4. 404 e telas de erro fora do shell
- **Evidência:** `NotFoundPage.tsx` — `<section><h1>Pagina nao encontrada</h1>…`, renderizada fora de `AppShell` (`App.tsx:40`). Sem estilo (não é `.panel`), sem navegação, sem acento.
- **Impacto:** quem erra uma URL ou segue um link quebrado fica numa tela órfã sem caminho de volta ao contexto e sem identidade — parece app quebrado.
- **Severidade:** alta.
- **Recomendação:** 404 dentro do `AppShell` quando autenticado, usando `EmptyState` (`size: page`) com ação "Voltar aos workspaces" e "Ver dispositivos". Corrigir acentuação.
- **Esforço:** P.
- **Dependências:** FE apenas.

### P1 — problema importante

#### P1-1. Política de tempo incompleta no ponto de uso
- **Evidência:** `DeviceDetailsPage` mostra "Ultimo contato: 14/08/2026 23:29:21" sem dizer o fuso (só o Dashboard rotula "Horário local"). Explorer: eixo X do gráfico só mostra hora (`chartTickFormatter` só inclui data se `rangeMs > 24h`) — a consulta de teste cruzou meia-noite (14→15/08) sem marcação de dia. Ocorrência vs. ingestão aparecem juntas mas sem destacar o atraso.
- **Impacto:** comparação temporal ambígua; operador em fuso diferente do servidor pode ler errado; "quando mudou?" fica impreciso.
- **Severidade:** alta.
- **Recomendação:** rótulo de fuso visível e consistente em toda tela com timestamp (ex.: badge "horário local · UTC−03:00"); eixo X com data na transição de dia mesmo em janelas < 24 h; quando `ingestedAt − occurredAt` for relevante, mostrar "atrasado NNs" sem reclassificar status.
- **Esforço:** M.
- **Dependências:** FE. Revisão conjunta de produto/domínio (já previsto em `ui-roadmap.md` Etapa 7).

#### P1-2. Eixo Y sem unidade e com auto-domínio ruim
- **Evidência:** `TelemetrySeriesPanel.tsx:208` — `YAxis domain={["auto","auto"]}` sem `label` quando não há unidade (e o catálogo não fornece unidade para nenhuma métrica). Observado: painel "Temperatura" (`environment.temperature_c ≈ 16`) com ticks `0, 30, 60, 90, 120`.
- **Impacto:** resolução visual perdida (linha achatada); leitor não sabe a grandeza; "17,5" pode ser % de disco ou °C.
- **Severidade:** alta.
- **Recomendação:** (a) domínio "nice" com padding relativo aos dados (não ancorar em 0 para grandezas que não começam em 0); (b) rótulo de eixo sempre — usar `canonicalUnit` quando houver, senão o nome curto da métrica; (c) exibir "sem unidade" como texto explícito no eixo, não vazio.
- **Esforço:** P–M.
- **Dependências:** FE. Rótulo com unidade real depende de BE popular `CanonicalUnit`/`SemanticType` no `MetricDefinition` (hoje nulos — ver `telemetry-explorer.md`).

#### P1-3. `signal-pulse` mente sobre "ao vivo"
- **Evidência:** `TelemetryActivity.tsx:52` renderiza `.signal-pulse` sempre que existe `timestamp`. Observado no Dashboard do Gateway Pi: pulso ciano ativo com "Há 24 dias". `brand-foundation.md`: `--signal` é reservado para "dado fluindo agora".
- **Impacto:** operador interpreta como fluxo ativo; contradiz a própria linguagem de marca.
- **Severidade:** média-alta.
- **Recomendação:** só pulsar quando o dado for recente (limiar explícito, ex.: < 2× `ExpectedIntervalSec` ou < 5 min como fallback); acima disso, ponto estático + rótulo "sem dados recentes" / "atrasado".
- **Esforço:** P.
- **Dependências:** FE. Limiar ideal vem de `ExpectedIntervalSec` (BE precisa expor).

#### P1-4. Sem tema e sem respeito a preferências do sistema
- **Evidência:** `styles.css:36` — `body { background: linear-gradient(...) }` fixo; nenhuma media query `prefers-color-scheme`; `prefers-reduced-motion` respeitado só para 2 animações; nenhum controle de usuário.
- **Impacto:** operação noturna / NOC com tela clara cansa a vista e destoa de outras ferramentas; fotofobia e baixa visão não têm ajuste; quem tem "reduzir movimento" no SO ainda vê o pulso (a regra existe, mas depende do componente lembrar).
- **Severidade:** média-alta.
- **Recomendação:** camada de preferências combináveis (detalhe na §6): tema (auto/claro/escuro), contraste (padrão/alto/reduzido), escala tipográfica, movimento. Persistir em `localStorage`; default = `prefers-color-scheme`. Tokens já são o ponto de extensão — só faltam os conjuntos de valores.
- **Esforço:** G.
- **Dependências:** FE apenas.

#### P1-5. Dashboard não liga sinal a investigação
- **Evidência:** `DashboardPage.tsx` — KPI "Mensagens rejeitadas: 72" com tom `danger` e dica "Há rejeições", mas **sem link** para ver quais/quando. "Offline detectado: 1 dispositivo" também sem link direto para o dispositivo. Sem "quando" nas contagens (`messagesProcessed`/`messagesRejected` são totais absolutos sem janela).
- **Impacto:** o Dashboard diz "algo está errado" e para aí — o operador precisa navegar e procurar. "O que mudou / quando" fica sem resposta.
- **Severidade:** média-alta.
- **Recomendação:** cada KPI de exceção vira link para o contexto (rejeições → Explorer pré-filtrado em `dropped_messages`/rejeições; offline → lista filtrada). Rotular a janela dos contadores ("desde sempre" vs. "últimas 24 h") conforme o backend informar. Não inventar tendência sem série.
- **Esforço:** M.
- **Dependências:** FE. Janela real dos contadores e endpoint de rejeições dependem de BE.

#### P1-6. Detalhes do dispositivo = despejo de JSON
- **Evidência:** `DeviceDetailsPage.tsx:157-182` — lista "Ultimas telemetrias" renderiza `<pre>{safeJsonPreview(item.payloadJson)}</pre>` para cada item, sem paginação. Observado ~30 blocos idênticos de 15 chaves.
- **Impacto:** rolagem enorme; impossível comparar valores entre horários; o payload domina a tela.
- **Severidade:** média-alta.
- **Recomendação:** substituir por tabela "métrica × horário" (últimos N), com unidade, e um `<details>` para o JSON cru por linha. Paginação ou "carregar mais". A visão consolidada (§7) resolve a maior parte disso.
- **Esforço:** M.
- **Dependências:** FE. `getTelemetry` já retorna o suficiente.

#### P1-7. Rotação de credencial sem confirmação
- **Evidência:** `DeviceDetailsPage.tsx:139` — `<button onClick={handleRotateCredential}>` dispara a rotação no primeiro clique. `ui-roadmap.md` Etapa 12 já previa isso.
- **Impacto:** clique acidental invalida a credencial MQTT ativa do dispositivo em produção — perda de ingestão até reprovisionar.
- **Severidade:** média-alta (ação destrutiva, um clique).
- **Recomendação:** diálogo de confirmação nomeando o dispositivo e a consequência ("a credencial atual `dev-…` deixa de funcionar imediatamente"). Sem alterar a chamada de API.
- **Esforço:** P.
- **Dependências:** FE apenas.

### P2 — melhoria relevante

#### P2-1. Impacto visual baixo / hierarquia fraca
- **Evidência:** todo container é `.auth-card, .panel` com a mesma borda e `--shadow-none`; `design-system.md` prevê variantes de card (`default/compact/metric/interactive`) e `DataTable`, ainda não implementadas. KPIs, atividade e saúde têm o mesmo peso visual. `.list-item` usa `justify-content: space-between` → em 1440 px o botão "Detalhes" fica a ~1200 px do nome.
- **Impacto:** leitura lenta em monitoramento; nada "salta"; espaço desperdiçado; sensação de "telas de MVP" e não de produto.
- **Severidade:** média.
- **Recomendação:** sistema visual da §5 — densidade padrão mais alta, hierarquia por tipografia/estrutura (não por sombra), `DataTable` para dispositivos/workspaces, largura de ação controlada, um acento de marca discreto por tela (não card para tudo).
- **Esforço:** G.
- **Dependências:** FE apenas.

#### P2-2. Idioma e acentuação
- **Evidência:** "Ultimo contato/telemetria/payload", "Topico MQTT", "Username ativo", "Rotacionar credencial", "Ingerido em" (`DeviceDetailsPage`); "Provisonar", "Sessao invalida", "Metadata JSON invalido" (`NewDevicePage`); "Senha e obrigatoria" (`LoginPage`); "Pagina nao encontrada" (`NotFoundPage`); "Unknown" (`DashboardPage`, `DeviceStatusBadge` renderiza `status.toLowerCase()` cru); "Healthy"/"Degraded" (Saúde). `design-system.md` já pede "Desconhecido".
- **Impacto:** parece descuidado; **leitores de tela em pt-BR pronunciam mal palavras sem acento**; vocabulário misto (inglês/português) quebra previsibilidade (WCAG 3.2).
- **Severidade:** média.
- **Recomendação:** varredura de strings; mapa de rótulos pt-BR para status (`Online`/`Offline`/`Desconhecido`, `Saudável`/`Degradado`/`Indisponível`); manter valor técnico da API em `title`/detalhe. `DeviceStatusBadge` deve migrar para `StatusBadge` + mapa (Etapa 6 do roadmap existente).
- **Esforço:** P–M.
- **Dependências:** FE apenas.

#### P2-3. Explorer — carga cognitiva e scroll aninhado
- **Evidência:** `.explorer-option-list { max-height: 310px; overflow-y: auto }` dentro de `.explorer-selection-grid`; observado: rolar sobre o fieldset rola a lista, não a página. Toolbar com 4 colunas densas (`grid-template-columns: minmax(440px,2fr) …`). Seleção atual não aparece fora dos fieldsets (só o contador "4/25").
- **Impacto:** difícil varrer catálogos maiores; scroll aninhado confunde e prejudica teclado; sem chips de seleção o operador perde o que escolheu ao rolar.
- **Severidade:** média.
- **Recomendação:** `ui-roadmap.md` Etapa 10 — chips removíveis de seleção acima dos fieldsets, "selecionar todos os visíveis", ordem lógica período→agregação→intervalo, altura das listas responsiva ao viewport.
- **Esforço:** M.
- **Dependências:** FE apenas.

#### P2-4. `ApiErrorMessage` genérico e sem semântica
- **Evidência:** `ApiErrorMessage.tsx` — `<p className="error-message">{message}</p>`, sem `role="alert"`, sem título, sem retry, sem trace ID. Ainda usado em `DashboardPage`, `LoginPage`, `NewDevicePage`, `WorkspacePage` (form). `design-system.md` já especifica `ErrorState` com `scope`/`retry`/`traceId`.
- **Impacto:** erro pode passar despercebido (sem anúncio assistivo); sem caminho de recuperação; `ApiProblem.traceId` existe no tipo mas nunca é exibido para suporte.
- **Severidade:** média.
- **Recomendação:** migrar os usos restantes para `ErrorState`; erro de formulário associado ao campo (`aria-invalid`/`aria-describedby`); `traceId` em `<details>` copiável.
- **Esforço:** M.
- **Dependências:** FE. `traceId` já vem no `ApiProblem`.

#### P2-5. Dispositivos/Workspaces em cards, não tabela
- **Evidência:** `DevicesPage.tsx` / `WorkspacePage.tsx` usam `<ul className="list">` com `.list-item` flex. Sem coluna de última atividade, sem ordenação, sem densidade compacta.
- **Impacto:** não escala para dezenas de dispositivos; "qual mudou de estado" exige ler card por card.
- **Severidade:** média (baixa com 1 dispositivo, alta no piloto de 100).
- **Recomendação:** `DataTable` responsiva (Etapa 8): nome, identificador, status, **última telemetria/contato**, ação nomeada. Cards só abaixo do breakpoint em que a tabela não couber.
- **Esforço:** M–G.
- **Dependências:** FE. `lastContactAtUtc`/`lastTelemetryReceivedAtUtc` já estão em `DeviceResponse`.

#### P2-6. Saúde da plataforma dá falsa tranquilidade
- **Evidência:** só `postgresql` registrado → "Status geral: Healthy". Nenhum check de MQTT, Worker de ingestão ou API (confirmado; `project-status.md §7` e memórias do projeto).
- **Impacto:** "Healthy" com o Worker caído. A página é honesta no texto ("componentes cuja saúde pode ser verificada"), mas o selão verde grande comunica o contrário.
- **Severidade:** média (a causa é backend).
- **Recomendação FE:** quando `components.length` for baixo, rebaixar o selo geral para "Parcial" / neutro e listar explicitamente o que **não** é monitorado ("MQTT, Worker de ingestão: não verificados nesta plataforma"). Nunca "Healthy" absoluto com cobertura parcial.
- **Severidade / esforço:** P (FE) + G (BE, para checks reais — fora do escopo desta revisão).
- **Dependências:** FE (rebaixamento honesto). BE (checks de verdade).

### P3 — refinamento

- **P3-1.** Flash do ID do workspace antes do nome resolver no Dashboard (`workspaceName` cai no fragmento de `workspaceId` até `listWorkspaces` responder). Recomendação: skeleton no título ou buscar o nome junto do dashboard. Esforço P. FE.
- **P3-2.** `button:disabled` genérico ok, mas `.explorer-preset`/`.button-secondary` misturam papéis de "toggle" e "botão"; `aria-pressed` está certo, o visual de "selecionado" poderia ser mais distinto do "hover". Esforço P. FE.
- **P3-3.** `metric-card strong { font-size: 1.6rem }` — valor literal fora do token `--text-data-lg-size` (única sobra literal de tipografia que encontrei). Esforço P. FE.
- **P3-4.** Bundle 600 kB sem code-splitting; Explorer + Recharts poderiam ser `lazy`. Esforço P–M. FE. (Já em `project-status.md §7`.)
- **P3-5.** `NewDevicePage` — botão "Cancelar" no fim da página, longe do formulário; "Salvar" e "Cancelar" deveriam estar juntos. Esforço P. FE.
- **P3-6.** Legenda do gráfico do Explorer repete só "Edgewarden Gateway" quando há 1 métrica por painel — pouco informativa. Poderia mostrar o valor atual (como faz a legenda do mockup de referência). Esforço P. FE.
- **P3-7.** `prefers-reduced-motion` deveria ser tratado uma vez em `tokens.css`/`globals` (variável `--transition-fast: 0` sob a media query) em vez de por componente. Esforço P. FE.

---

## 5. Proposta de sistema visual

**Direção:** técnica, contemporânea, sóbria, "levemente marcante". Consolidação do que `brand-foundation.md` já define — não um redesign.

### 5.1 Princípios visuais

1. **Hierarquia por estrutura e tipografia, não por sombra.** Card de conteúdo = borda + `--shadow-none`. Elevação só para overlay/popover/modal. Isso já está nos tokens (`--shadow-sm/md/lg`), falta aplicar consistentemente e parar de usar `.panel` para tudo.
2. **Densidade operacional por padrão.** Ritmo base de 8 px (não 16), `body-sm` (14 px) como corpo de tabela, KPIs mais compactos. Uma opção "confortável" fica na camada de preferências, não o contrário.
3. **Um acento de marca por tela, não um card por informação.** O verde petróleo (`--brand-primary`) aparece em ação primária, link e nav ativa. O petrol-teal (`--brand-accent`) só em seleção de conteúdo. Ciano (`--signal`) só em "dado fluindo agora" — e agora **de verdade** (ver P1-3).
4. **Superfície como plano de fundo estrutural.** Em vez do gradiente claro fixo, um `--canvas` sólido (com variante escura) e cartões `--surface` com 1px de borda. O gradiente de marca fica só no header.
5. **Gráfico é dado, não decoração.** Sem grid pesado, sem área preenchida por padrão, sem animação de entrada. Linha de 2 px, `dot={false}`, `activeDot` no hover. (O portal já faz quase tudo isso.)

### 5.2 Tokens a adicionar (sobre `tokens.css`, sem renomear os existentes)

```
/* Superfície estrutural — hoje só existe o gradiente literal em body */
--canvas: <claro #F4F7F5 | escuro #0E1512>
--canvas-raised: <claro #FFFFFF | escuro #16201C>
--surface: <claro #FFFFFF | escuro #1B2723>
--surface-subtle: <claro #F8FAF9 | escuro #131C19>
--border: <claro #D8E1DD | escuro #2A3733>
--border-strong: <claro #94A39D | escuro #3C4C47>
--text: <claro #17211E | escuro #E6EEEB>
--text-muted: <claro #52615B | escuro #9DB0AA>

/* Densidade — multiplicador aplicado ao spacing de componentes de lista/tabela */
--density: 1                 /* "confortável" = 1.15 */

/* Paleta de gráfico daltônico-segura (substitui --chart-1..8) — ver §8.4 */
```

Os nomes semânticos (`--color-primary`, `--success`, etc.) **não mudam**; só ganham valores por tema.

### 5.3 Componentes a promover (ordem)

1. `Card` com variantes (`section`/`metric`/`compact`) — para parar de usar `.panel` universal.
2. `DataTable` (caption, `scope`, densidade, scroll horizontal, estado vazio) — Devices, Workspaces, telemetria recente do dispositivo.
3. `PageHeader` já existe; adicionar área de metadados ("atualizado em", workspace) e breadcrumb opcional.
4. `MetricTile` — valor + unidade + estado + freshness + mini-tendência + link de detalhe (base da §7).
5. `Timestamp` já existe; adicionar prop de fuso visível e modo relativo com absoluto acessível.
6. `DefinitionList` + `CopyableCode` — Detalhes do dispositivo (IDs, tópico MQTT, secret).

### 5.4 O que **não** fazer

Nada de neon, glow, glassmorphism generalizado, ilustração de fundo, animação decorativa, "partículas fluindo", estética de videogame. O vocabulário "fluxo de dados" (linhas/pulsos/nós) fica restrito a indicadores funcionais pequenos dentro da aplicação (como o `signal-pulse` corrigido) e a telas de entrada (login/onboarding/empty state), como `brand-foundation.md` já determina.

---

## 6. Proposta de acessibilidade

**Meta:** WCAG 2.2 AA no fluxo Login → Workspace → Dispositivo → Explorer.

### 6.1 Correções de conformidade (curto prazo)

| Item | WCAG | Ação |
|---|---|---|
| Gráfico sem alternativa | 1.1.1, 1.4.5 | Resumo textual + tabela em `<details>` (P0-2) |
| Série só por cor | 1.4.1 | Traço/símbolo além de cor quando > 4 séries |
| Contraste dos estados | 1.4.3 | Auditar `--text-muted` em 14 px, badges sólidos, borda de foco, disabled, `warning` sobre `warning-subtle` — com axe/Lighthouse |
| Fuso implícito | 3.1 (clareza) | Rótulo de fuso em todo timestamp |
| Idioma misto | 3.2.4 | Vocabulário pt-BR consistente (P2-2) |
| 404 sem contexto | 2.4 | 404 no shell com navegação (P0-4) |
| Erro sem anúncio | 4.1.3 | Migrar `ApiErrorMessage` → `ErrorState` com `role="alert"` (P2-4) |
| Erro de campo não associado | 3.3.1, 1.3.1 | `aria-invalid` + `aria-describedby` nos formulários |
| Alvos de toque 39–40 px | 2.5.8 (AA = 24 px; meta interna 44 px) | Subir "Sair", CTAs e presets para ≥ 44 px |
| Scroll aninhado no Explorer | 2.1.1 | Altura de lista responsiva; evitar `overflow` que capture a roda sobre foco de teclado |
| Zoom 200% / texto 200% | 1.4.4, 1.4.10 | Validar em 320 px CSS e 400% (a base fluida já ajuda) |

### 6.2 Camada de preferências (médio prazo)

**Não criar "um tema por deficiência".** Quatro eixos independentes e combináveis, num só painel ("Aparência"), acessível pelo header:

| Eixo | Valores | Default | Persistência |
|---|---|---|---|
| Tema | Automático / Claro / Escuro | Automático (`prefers-color-scheme`) | `localStorage` |
| Contraste | Padrão / Alto / Reduzido (para fotofobia/sensibilidade à luz) | Padrão (ou Alto se `prefers-contrast: more`) | `localStorage` |
| Escala de texto | 100% / 112% / 125% | 100% | `localStorage` |
| Movimento | Conforme sistema / Sempre reduzido | Conforme sistema (`prefers-reduced-motion`) | `localStorage` |

Implementação: atributos `data-theme`, `data-contrast`, `data-text-scale`, `data-motion` no `<html>`; cada um só redefine tokens, nunca estrutura. "Alto contraste" = bordas mais fortes, texto mais escuro/claro, remove superfícies sutis. "Contraste reduzido" = reduz o delta de luminância entre canvas e surface, baixa o branco puro, sem perder AA. Paleta de gráfico daltônico-segura é **sempre ativa** (não é uma opção — é a paleta), reforçada por traço/símbolo.

Requisitos: fácil de achar (ícone no header), nomes claros em pt-BR, respeita o sistema por padrão, persiste, **não depende só de cor** para nada, não descaracteriza a marca (verde continua verde, só muda luminância/saturação por tema).

### 6.3 Estados que precisam de tratamento acessível explícito

- **Carregando:** `role="status"`, `aria-live="polite"`, texto; durante refresh manter dado e anunciar "Atualizando" (o portal já faz no Explorer).
- **Sem dados:** texto explícito, nunca zero inventado (o portal já faz).
- **Offline / dado atrasado:** anunciar mudança de estado (`aria-live`), não só mudar cor; dizer **desde quando**.
- **Erro:** `role="alert"`, ação recomendada, `traceId` copiável.

---

## 7. Proposta para métricas nativas

### 7.1 O que já existe vs. o que depende de mudança

Métricas realmente emitidas hoje pelo **Gateway Pi** (`gateway-spool.js` → `diagnostics()`), todas Schema V2, todas já persistidas e consultáveis:

| Métrica (key) | Tipo | Unidade nativa | Estado |
|---|---|---|---|
| `gateway.uptime_sec` | Numeric | s | **Existe**, consultável |
| `gateway.cpu_temperature_c` | Numeric | °C | **Existe** (condicional ao HW) |
| `gateway.memory_used_percent` | Numeric | % | **Existe** |
| `gateway.disk_used_percent` | Numeric | % | **Existe** (condicional) |
| `gateway.load_1m` | Numeric | (carga) | **Existe** |
| `gateway.queue_depth` | Numeric | mensagens | **Existe** — é o spool/fila local |
| `gateway.mqtt_connected` | Boolean | — | **Existe** |
| `gateway.replayed_messages` | Numeric | mensagens (acum.) | **Existe** |
| `gateway.dropped_messages` | Numeric | mensagens (acum.) | **Existe** |
| `gateway.time_synchronized` | Boolean | — | **Existe** (condicional) |
| `gateway.network_interface_up` | Boolean | — | **Existe** |
| `gateway.network_rx_bytes` / `tx_bytes` | Numeric | bytes (acum.) | **Existe** |
| `environment.temperature_c` / `humidity_percent` | Numeric | °C / % | **Existe** (sensor físico) |

**Não existe hoje:**
- **CPU utilização (%)** — o gateway só manda `cpu_temperature_c` e `load_1m`, não `cpu_percent`. → **mudança no agente/dispositivo** se quiser %.
- **Memória em bytes** (só %), **disco em bytes/GB livres** (só %). → mudança no agente (barato) ou cálculo no portal se o total vier em metadados.
- **Taxa de mensagens/min** — só há contadores acumulados (`replayed`/`dropped`) e `queue_depth`. Taxa = derivar de dois pontos no portal, ou o agente manda `messages_per_min`. 
- **Limiares / faixa esperada / qualidade do dado por métrica** — `MetricDefinition` **já tem** `MinExpectedValue`, `MaxExpectedValue`, `ExpectedIntervalSec` no domínio (`Fluxo.Domain/Entities/MetricDefinition.cs`), mas o DTO `MetricDefinitionResponse` **não os expõe**. → **mudança no backend** (só adicionar 3 campos ao DTO + um endpoint/config para preenchê-los).
- **ESP32** — o firmware de referência ainda não foi executado 24 h (`project-status.md`); as métricas nativas dele não estão confirmadas. Tratar como "possibilidade futura".

**Só depende do backend:**
- Endpoint consolidado "estado atual + janelas" por dispositivo (hoje o portal montaria isso com N chamadas ao Query API + o último payload).
- Janela real dos contadores do Dashboard.
- Checks de saúde de MQTT/Worker.

### 7.2 A visão "Observabilidade do dispositivo" (nova aba na página do dispositivo)

Uma tela que responde, em ordem de leitura:

1. **Cabeçalho de estado** — nome, identificador, estado operacional (`StatusBadge`), **"última telemetria há Xs"** com semântica de freshness real (verde só se recente), agente/versão e uptime formatado ("ligado há 4 h 22 min").
2. **Faixa de sinais consolidados** — `MetricTile` por métrica nativa relevante, agrupados:
   - **Recursos:** CPU (temp + carga), Memória %, Disco %, Temperatura ambiente.
   - **Conectividade:** MQTT conectado (bool), interface de rede (bool), horário sincronizado (bool), RX/TX (taxa derivada).
   - **Processamento:** mensagens/min (derivada), reenviadas, descartadas, **fila/spool local** (`queue_depth`).
   Cada tile mostra: **valor atual + unidade nativa**, **tendência** (seta + mini-sparkline das últimas N amostras), **pico** da janela, e **estado** (dentro/fora da faixa esperada quando `Min/MaxExpectedValue` existir; "—" quando não). Offline / dado atrasado é explícito no tile ("sem dado há 24 dias"), não um valor velho parecendo atual.
3. **Gráfico consolidado** (ver §8) — todas as séries numéricas da janela, com correlação temporal.
4. **Eventos correlacionados** — quando dois sinais mudam juntos (ex.: pico de `load_1m` junto com `cpu_temperature_c`), uma nota textual com o instante e o contexto. Curto prazo: heurística simples no portal (co-ocorrência de picos na mesma janela). Não inventar causa — só apontar co-ocorrência.
5. **Continuidade** — nota fixa: "as métricas continuam sendo armazenadas e consultáveis individualmente como séries nativas; esta visão só organiza a leitura". Link para o Explorer com a seleção pré-carregada.

**Regras invioláveis:**
- Unidade nativa sempre preservada (°C, %, s, bytes, mensagens). Nada de conversão silenciosa.
- Valor atual, tendência, pico e histórico são coisas distintas e rotuladas.
- Offline / atraso é sempre explícito e datado.
- O portal **não recalcula** o estado operacional nem reclassifica status — só apresenta o que a API dá.
- Histórico continua no Explorer; esta tela é leitura rápida, não substitui análise.

### 7.3 Implementação incremental

| Etapa | O que | Depende de |
|---|---|---|
| Curto | Aba "Visão operacional" montada do **último payload** + `getTelemetry` (janela curta). Tiles com valor/unidade/tendência/pico. Freshness real. | FE só |
| Curto | Corrigir `signal-pulse`, JSON→tabela em Detalhes, links do Dashboard | FE só |
| Médio | Expor `Min/MaxExpectedValue`/`ExpectedIntervalSec` no DTO → tiles ganham faixa/estado/limiar | BE (3 campos) + FE |
| Médio | Endpoint consolidado por dispositivo (estado + janelas 1h/6h/24h) → menos chamadas, mais rápido | BE + FE |
| Longo | Eventos correlacionados server-side; taxa de mensagens/min como métrica do agente; CPU % no agente | BE + dispositivo |

---

## 8. Justificativa da solução de gráficos

### 8.1 O problema

As métricas nativas têm **escalas incompatíveis**: `cpu_temperature_c` (~42), `memory_used_percent` (~33), `load_1m` (~0,01), `disk_used_percent` (17,5), `uptime_sec` (~15.000), `network_tx_bytes` (~14 milhões). Hoje o Explorer resolve separando cada uma em seu próprio painel de ~490 px — correto para não misturar eixos, mas **impede correlação**: para ver se o pico de CPU coincidiu com carga alta, o operador rola entre dois gráficos.

### 8.2 Abordagens avaliadas

| Abordagem | Prós | Contras |
|---|---|---|
| **Um eixo, sem normalizar** | honesto | inútil: `load_1m` vira linha reta no chão perto de `uptime_sec` |
| **Normalização (0–100% do range da série)** | tudo comparável na mesma altura; tendência e correlação óbvias | esconde magnitude; **precisa deixar explícito** que foi normalizado |
| **Múltiplos eixos Y (2–3)** | mantém magnitude | ilegível com > 3 séries; qual série é qual eixo? |
| **Pequenos múltiplos compactos** (mini-gráficos alinhados no mesmo eixo X) | correlação temporal preservada; magnitude real; acessível; escala independente por métrica | menos "impacto" que um gráfico grande sobreposto |
| **Sobreposição opcional** | operador escolhe | só útil como complemento |

### 8.3 Escolha proposta (combinação)

**Pequenos múltiplos como padrão + overlay normalizado opcional.**

1. **Padrão — pequenos múltiplos:** uma faixa por métrica (~90–120 px de altura), **todas compartilhando o mesmo eixo X**, alinhadas verticalmente. Cada faixa tem sua própria escala Y com unidade nativa e mostra atual/mín/máx no canto. Uma linha vertical de cursor atravessa todas as faixas no hover → correlação temporal imediata, sem perder magnitude, sem normalizar nada. É a abordagem mais acessível (cada faixa é um gráfico simples + resumo textual + tabela opcional).
2. **Opcional — overlay normalizado:** um botão "Comparar tendências (escala normalizada 0–100%)" sobrepõe todas as séries num único gráfico, **com um rótulo permanente e visível "escala normalizada — os valores no eixo não são as unidades reais"** e o tooltip sempre mostrando o valor real + unidade. Nunca é o default e nunca esconde do usuário que normalizou.
3. **Nunca:** múltiplos eixos Y ocultos, ou normalização silenciosa.

### 8.4 Requisitos do gráfico (padrão e overlay)

- Períodos predefinidos **1h / 6h / 24h** + **"Ver tudo"** (janela = do primeiro ao último ponto disponível).
- **Clique na legenda:** 1º clique isola a série; 2º restaura. Ctrl/Shift-clique combina. Estado visual claro: série ativa = cor cheia + valor atual na legenda; oculta = esmaecida + riscada + "oculta" no `aria`.
- Unidades nativas em todo valor e no tooltip; no overlay, "(normalizado)" ao lado do eixo e valor real no tooltip.
- **Tooltip acessível:** navegável por teclado (setas movem o cursor no tempo), conteúdo em `aria-live`, não só hover.
- **Resumo textual** por série: atual, mín, máx, média, tendência (subindo/estável/caindo), nº de lacunas.
- **Lacunas / atraso / ausência de dados:** linha não conecta lacunas (`connectNulls=false`, já é assim); trecho sem dado marcado visualmente (hachura) e citado no resumo ("sem dados entre 02:10 e 02:40"); dado atrasado (ingestão ≫ ocorrência) sinalizado.
- **Eventos correlacionados:** marcadores verticais na régua de tempo quando aplicável, com descrição textual.
- Eixo X com **data na virada de dia** sempre; fuso rotulado.
- Paleta daltônico-segura + padrão de traço (sólido/tracejado/pontilhado) por série.

A POC implementa (1), (2), períodos + "Ver tudo", legenda interativa, resumo textual, estados de lacuna/atraso/sem-histórico e tooltip por teclado, com dados simulados.

---

## 9. Roadmap

Cada item mantém API, ingestão, regras de status e telemetria intactos. Entregas pequenas, revisáveis e reversíveis, seguindo o gate de `ui-roadmap.md` (working tree limpa, testes, build, revisão em 1600/1024/768/375, teclado/foco).

### Curto prazo (1–3 sprints) — "a console para de mentir e passa a responder"

| # | Item | Achado | Esforço | Dep. |
|---|---|---|---|---|
| C1 | Persistir sessão (`sessionStorage` + reidratação + `returnTo`) | P0-1 | P–M | FE |
| C2 | Alternativa textual + tabela para gráficos Numeric/Boolean no Explorer | P0-2 | M | FE |
| C3 | Aba "Visão operacional" do dispositivo, montada do último payload + `getTelemetry` (tiles com valor/unidade/tendência/pico/freshness) | P0-3 | M | FE |
| C4 | 404 e erros dentro do `AppShell` com navegação; acentuação | P0-4 | P | FE |
| C5 | `signal-pulse` só quando o dado é recente | P1-3 | P | FE |
| C6 | JSON→tabela em Detalhes + paginação | P1-6 | M | FE |
| C7 | Confirmação na rotação de credencial | P1-7 | P | FE |
| C8 | Links de KPI de exceção → contexto (rejeições, offline) | P1-5 | M | FE |
| C9 | Varredura de idioma/acentuação + `DeviceStatusBadge`→`StatusBadge` pt-BR | P2-2 | P–M | FE |
| C10 | Eixo Y com domínio "nice" + rótulo sempre; eixo X com data na virada de dia; fuso rotulado | P1-1, P1-2 | M | FE |

### Médio prazo (3–6 sprints) — "sistema visual e preferências"

| # | Item | Achado | Esforço | Dep. |
|---|---|---|---|---|
| M1 | Camada de preferências combináveis (tema auto/claro/escuro, contraste, escala de texto, movimento) sobre os tokens | P1-4, §6.2 | G | FE |
| M2 | Paleta de gráfico daltônico-segura + traço/símbolo por série | P0-2, §8.4 | M | FE |
| M3 | `Card` com variantes + parar de usar `.panel` universal; hierarquia por estrutura | P2-1, §5 | G | FE |
| M4 | `DataTable` responsiva; migrar Devices, Workspaces, telemetria recente | P2-5 | M–G | FE |
| M5 | Explorer: chips de seleção, "selecionar visíveis", ordem lógica, sem scroll aninhado | P2-3 | M | FE |
| M6 | Gráfico consolidado (pequenos múltiplos + overlay normalizado opcional) na Visão operacional e/ou Explorer | §8 | G | FE |
| M7 | `ApiErrorMessage`→`ErrorState` nos usos restantes; erro de campo associado; `traceId` copiável | P2-4 | M | FE |
| M8 | Expor `Min/MaxExpectedValue`/`ExpectedIntervalSec` no DTO → tiles com faixa/estado | P0-3, §7 | P (BE) + M (FE) | **BE** + FE |
| M9 | Saúde: rebaixar selo geral quando cobertura é parcial; listar o não-monitorado | P2-6 | P | FE |
| M10 | Auditoria de contraste com axe/Lighthouse + correções | §6.1 | M | FE |

### Longo prazo (6+ sprints) — "escala, correlação e sustentação"

| # | Item | Dep. |
|---|---|---|
| L1 | Endpoint consolidado por dispositivo (estado + janelas 1h/6h/24h/tudo) | BE + FE |
| L2 | Eventos correlacionados server-side (co-ocorrência, gaps, atrasos) integrados à Visão operacional e aos alertas (ADR-0002) | BE + FE |
| L3 | Taxa de mensagens/min e CPU % como métricas do agente; janela real dos contadores do Dashboard | BE + dispositivo |
| L4 | `WorkspaceSwitcher` no header (contexto sem regex no pathname); breadcrumbs | FE |
| L5 | Densidade configurável; navegação lateral 240 px no desktop + drawer no mobile (Etapa 5 do roadmap existente) | FE |
| L6 | Code-splitting (Explorer/Recharts `lazy`); proteção contra regressão visual nos breakpoints | FE |
| L7 | Catálogo interno de componentes/estados coberto por testes | FE |
| L8 | Métricas nativas do ESP32 quando o firmware for validado 24 h | dispositivo + BE + FE |

---

## 10. Entrega

### 10.1 Caminhos

- **Relatório:** `Fluxo/docs/frontend/revisao-portal-2026-09-07/relatorio-avaliacao.md` (este arquivo).
- **Mockup / POC:** `Fluxo/mockups/portal-observabilidade-v2/` — `index.html`, `styles.css`, `demo-data.js`, `app.js`, `README.md`.

### 10.2 Como executar o mockup

Sem build, sem dependências, sem servidor:

```bash
# Windows
start D:\Officina404\Fluxo\mockups\portal-observabilidade-v2\index.html
```

ou abrir o arquivo `index.html` no navegador (duplo clique). Opcionalmente, servir a pasta:

```bash
cd D:/Officina404/Fluxo/mockups/portal-observabilidade-v2 && python -m http.server 4180
# abre em http://localhost:4180
```

Todos os dados são **simulados e marcados como "DEMONSTRAÇÃO"** na interface. Interações reais: períodos (1h/6h/24h/Ver tudo), troca de tema/contraste/escala/movimento (persistem no `localStorage`), isolar/combinar/ocultar séries pela legenda, alternar pequenos múltiplos ↔ overlay normalizado, e um seletor de cenário (normal / offline / dado atrasado / sem histórico / rejeições).

### 10.3 Validações realizadas

- **Portal oficial NÃO alterado** — `git status` do `portal-web` limpo; nenhuma edição em `src/`.
- `npm run test` no `portal-web`: **30/30 aprovados** (baseline preservado).
- `npm run build` no `portal-web`: **aprovado** (bundle 600 kB, warning pré-existente).
- Navegação autenticada real em todas as 9 rotas, nos 2 workspaces, com dados reais (Explorer executado com 2.044 pontos).
- Layout verificado sem overflow horizontal em 1440 px, 1280 px e 375 px (dashboard e explorer).
- Mockup: aberto via `file://` e via `http.server`; testado em ~1440 px, ~768 px e ~375 px; navegação por teclado (Tab/Enter/setas no gráfico); temas claro/escuro/alto contraste; `prefers-reduced-motion`; cenários offline / dado atrasado / sem histórico.

### 10.4 Principais riscos

1. **`Min/MaxExpectedValue` no domínio ≠ no DTO** — vários itens (faixa nos tiles, limiar do gráfico, freshness ideal) dependem de o backend expor 3 campos que já existem no modelo. Sem isso, o portal mostra "—" para faixa/limiar (aceitável, mas menos útil).
2. **Catálogo de métricas sem `CanonicalUnit`/`SemanticType`** — hoje todas as métricas são "Unidade não informada". Rótulos de eixo com unidade real e agrupamento seguro dependem de o catálogo ser preenchido (produto/domínio).
3. **Saúde da plataforma parcial** — enquanto só `postgresql` é monitorado, qualquer melhoria de FE na página é cosmética; o valor real depende de checks de MQTT/Worker no backend.
4. **Sessão persistida amplia a superfície de XSS** — `sessionStorage` com token exige revisar `sanitize.ts` e o CSP; alternativa mais segura (cookie `HttpOnly`) exige mudança no backend.
5. **Gráfico consolidado é o item de maior risco de regressão** — testes de regressão dos dados (lacunas não viram zero, boolean continua step-after, truncamento visível) são obrigatórios, como já diz `ui-roadmap.md` Etapa 11.
6. **Recharts 2.x** — a proposta assume que fica; pequenos múltiplos + overlay são viáveis com Recharts, mas se a decisão futura for trocar de biblioteca, M6 deve esperar.
7. **Bundle já acima do warning** — adicionar a Visão operacional sem code-splitting piora; L6 (lazy) deveria subir de prioridade se o crescimento incomodar.

### 10.5 Decisões que precisam de aprovação humana

1. **Persistência de sessão:** `sessionStorage` (rápido, FE-só, aceita o risco XSS) **ou** cookie `HttpOnly` + refresh endpoint (mais seguro, exige backend). — **Recomendo `sessionStorage` agora, cookie no médio prazo.**
2. **Expor `Min/MaxExpectedValue`/`ExpectedIntervalSec` no `MetricDefinitionResponse`** — mudança pequena de backend, destrava faixa/limiar/freshness. Precisa de sinal verde do dono do backend e revisão de domínio sobre o significado ("faixa esperada" ≠ "limite de alerta" do ADR-0002).
3. **Onde a "Visão operacional" mora:** aba na página do dispositivo (recomendado) **ou** substituir a página de Detalhes atual **ou** uma rota nova. — **Recomendo aba nova, mantendo Detalhes para provisionamento/credencial.**
4. **Estratégia de gráfico consolidado:** confirmar "pequenos múltiplos + overlay normalizado opcional" como direção antes de investir em M6.
5. **Tema escuro como padrão?** A POC usa **auto** (`prefers-color-scheme`). `brand-foundation.md` fala em "tema escuro padrão" numa lista de possibilidades; o pedido desta revisão também. Decidir: default = auto (recomendado, respeita o sistema) ou default = escuro.
6. **Vocabulário de status:** aprovar o mapa pt-BR (`Online`/`Offline`/`Desconhecido`, `Saudável`/`Degradado`/`Indisponível`/`Verificando`) — muda texto visível em produção, exige revisão de produto (governança de `design-system.md`).
7. **Escopo do curto prazo:** os 10 itens de C1–C10 cabem em 2–3 sprints? Priorizar dentro deles se não couber (sugestão de corte mínimo: C1, C2, C3, C4, C5).

---

*Fim do relatório. Nenhuma alteração foi feita no portal oficial, e nenhum commit, push ou deploy foi executado.*
