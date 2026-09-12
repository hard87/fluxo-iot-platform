# Checklist de release visual do Fluxo Portal

Checklist reutilizável para qualquer entrega que toque `portal-web/`. Combina o "Gate comum" de `ui-roadmap.md` com o roteiro de regressão manual e a cobertura de estados vazio/carregamento/erro. Copie esta lista para a descrição do PR e marque cada item.

## Antes de alterar

- [ ] `git status` limpo antes de começar.
- [ ] Escopo da mudança entendido e delimitado (qual etapa do `ui-roadmap.md`, quais páginas).
- [ ] Entrega identificada no `portal-evolution.md`, com estado inicial, aceite e evidências esperadas.

## Testes automatizados

- [ ] `npm run test` (vitest) aprovado em `portal-web/`.
- [ ] `npm run build` (`tsc --noEmit && vite build`) aprovado em `portal-web/`.
- [ ] Se algum contrato de API mudou (ex.: novo campo em DTO), `dotnet build` do projeto backend afetado aprovado.
- [ ] Componentes compartilhados novos ou alterados têm teste de comportamento/semântica proporcional ao risco (não snapshot massivo).

## Consistência visual

- [ ] Cores, radius, shadow, spacing e tipografia usam os tokens de `design-system.md`/`tokens.css` — nenhum valor literal novo sem justificativa.
- [ ] Badges de status usam `StatusBadge`/`.status-badge` com texto visível (nunca só cor).
- [ ] Timestamps usam `Timestamp`/`formatHumanTimestamp` (apresentação humana como texto principal, ISO disponível via `title`/`dateTime`) — nenhum ISO bruto solto na tela.
- [ ] Estados de carregamento/vazio/erro usam `LoadingState`/`EmptyState`/`ErrorState` (`components/feedback/FeedbackStates.tsx`), não `<p>` ad-hoc.

## Acessibilidade

- [ ] Navegação completa por teclado (Tab/Shift+Tab) nas telas alteradas, incluindo skip link.
- [ ] `:focus-visible` visível em todo elemento interativo alterado.
- [ ] Todo input/select tem label associado.
- [ ] Nenhuma informação depende só de cor (badges, estado selecionado, erros).
- [ ] Um `h1` por página (via `PageHeader`), landmarks semânticos preservados.

## Responsividade

Revisar sem quebra (sem scroll horizontal, sem conteúdo cortado) em:

- [ ] 1440 px
- [ ] 1024 px
- [ ] 768 px

Não é necessário entregar uma experiência mobile completa nesta fase, apenas não quebrar.

## Regressão manual — fluxos principais

Percorrer manualmente contra um backend real (não mockado):

- [ ] Login
- [ ] Workspaces (listar, criar, selecionar)
- [ ] Workspace Dashboard
- [ ] Devices (lista e detalhes)
- [ ] Telemetry Explorer
- [ ] Platform Health (`/status`)
- [ ] Logout

## Depois de alterar

- [ ] `git diff --check` sem espaços em branco problemáticos.
- [ ] `git diff` revisado linha a linha.
- [ ] Diff limitado ao frontend e à documentação da etapa (sem refatoração não relacionada).
- [ ] `docs/frontend/design-system.md` e/ou `docs/frontend/ui-roadmap.md` atualizados se algo do escopo documentado mudou de estado.
- [ ] `docs/frontend/portal-evolution.md` atualizado com o estado real da entrega, comandos executados, resultado e validações ainda pendentes.
- [ ] Nenhum item marcado como comprovado apenas com base na existência do código; evidências automatizadas, visuais e operacionais estão separadas.
- [ ] Commit único e semanticamente coerente.
- [ ] `git status` limpo ao final.

## Fora do escopo desta fase (não bloqueiam a release)

- Componentes React formais `Button`/`Field`/`Input`/`Select` com variantes (Etapa 2).
- `DataTable` para listas grandes (Etapa 8).
- Drawer de navegação mobile completo e dark mode.
- Novos health checks de infraestrutura — mudanças em `/api/status` devem apenas apresentar melhor o que já é monitorado, nunca inventar componentes.
