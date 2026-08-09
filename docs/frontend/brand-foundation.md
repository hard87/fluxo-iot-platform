# Fundação de marca do Fluxo

## Por que este documento existe

O portal evoluiu em consistência técnica (tokens, primitives, estados) sem uma direção de identidade explícita. Este documento registra essa direção — não como um redesign, mas como a base para que futuras telas (login, onboarding, empty states, site, documentação) compartilhem a mesma linguagem visual sem reinventá-la a cada entrega.

## Princípios

O Fluxo representa **conectividade, fluxo de dados, IoT, edge computing, telemetria e observabilidade**. A direção visual traduz isso como:

- **Tecnológica** — precisão, dados reais, nada ilustrativo por ilustrar.
- **Profissional** — usada por operadores tomando decisão, não por um público casual.
- **Industrial** — sóbria, funcional, confiável sob pressão (um alarme às 3h precisa ser lido rápido).
- **Sóbria** — cor comunica estado, não humor da marca.
- **Moderna** — sem parecer datada, sem perseguir tendência.

### Anti-padrões (explicitamente evitados)

- Cyberpunk exagerado, neon, glow.
- Gradientes decorativos fora do header.
- Glassmorphism generalizado.
- Animações decorativas sem função (a única animação com movimento contínuo hoje, `signal-pulse`, existe para indicar dado ao vivo, não para "dar vida" à tela).
- Visual de videogame.
- Template SaaS genérico — cards grandes demais, ícones fofos, ilustrações de "empty state feliz".

Quando em dúvida entre "mais expressivo" e "mais sóbrio", escolher sóbrio.

## Paleta

A paleta técnica completa (valores hex, escala de espaçamento, radius, shadow, tipografia) vive em `portal-web/src/styles/tokens.css` e está documentada em `design-system.md`. Este documento cobre só a camada semântica de marca, adicionada em `tokens.css`:

| Token | Papel |
|---|---|
| `--brand-deep` | Chrome estrutural — fundo do header/gradiente. Nunca usado em texto ou em telas de conteúdo. |
| `--brand-primary` | Verde petróleo, a cor de marca. Ações primárias, links, navegação ativa. Preservado como base — não muda. |
| `--brand-accent` | Petrol-teal, segunda cor de marca. Reservado para **seleção de conteúdo** (workspace selecionado, opção marcada no Explorer, preset de período ativo) — nunca para navegação, que continua com `--brand-primary`. Essa separação é deliberada: navegação responde "onde eu estou", seleção responde "o que eu escolhi", e são conceitos diferentes que não deveriam competir pela mesma cor. |
| `--signal` | Ciano vivo. Uso limitado e literal: só em indicadores de fluxo de dados ativo (o pulso ao lado de "Última telemetria" no Dashboard). Nunca em texto, nunca em fundo de superfície, nunca como decoração. |
| `--surface` / `--canvas` | Fundo de cartão vs. fundo de página. |
| `--border`, `--text`, `--muted` | Neutros de interface. |
| `--success`, `--warning`, `--danger`, `--info`, `--unknown` | Estados semânticos, independentes de marca — nunca reutilizados para decoração. |

Valores hex de referência (2026-08-08, revisados a partir de proposta de produto):

| Token | Hex |
|---|---|
| `--brand-deep` | `#073D35` |
| `--brand-primary` | `#0B5C4B` (preservado, não faz parte da revisão) |
| `--brand-accent` | `#00A98F` |
| `--signal` | `#40D9D0` |
| `--canvas` | `#F4F7F5` |
| `--surface` | `#FFFFFF` |
| `--border` | `#D8E1DD` |
| `--text` | `#17211E` |

### Regras de aplicação

- Cor de marca (`--brand-primary`/`--brand-accent`) só aparece em elementos interativos ou de identidade (header, links, botão primário, seleção). Texto de corpo, dados operacionais e tabelas permanecem neutros.
- `--signal` é a única cor reservada exclusivamente para o conceito de "dado fluindo agora". Se um componente novo precisar comunicar "atividade", a pergunta certa é "isso é literalmente telemetria ao vivo?" — se não for, usar `--info` ou `--success`, não `--signal`.
- Um elemento nunca deve depender só de `--brand-accent` vs. `--brand-primary` para ser compreendido — texto e posição sempre acompanham a cor (ver `interaction-guidelines.md`).

## O conceito visual "fluxo de dados"

Vocabulário visual permitido para expressar a marca em telas futuras (login, onboarding, empty states, site, docs): **linhas, trilhas, pulsos, nós, conexões, fluxos direcionais** — elementos que descrevem literalmente uma rede de sensores enviando dados para uma plataforma central, não uma metáfora abstrata.

Regras para esse vocabulário:

- **Discreto dentro da aplicação operacional.** Nas telas internas (Dashboard, Devices, Explorer, Health), esses elementos só aparecem como indicadores funcionais pequenos — como o `signal-pulse` já implementado — nunca como ilustração de fundo ou hero.
- **Sem ilustrações grandes nas telas internas ainda.** Este documento define o princípio; a aplicação em maior escala (login, onboarding, site) é trabalho futuro e deve ser proposta separadamente, não implementada retroativamente aqui.
- **Direcional, não decorativo.** Uma linha ou trilha, se usada, deve sugerir um caminho real (de dispositivo → gateway → plataforma), não um padrão abstrato de fundo.
- **Nó = ponto de dado real.** Se o vocabulário evoluir para incluir "nós" visuais, cada nó deve corresponder a uma entidade real (dispositivo, workspace, gateway) — nunca decoração sem referente.

### Exemplo correto já em produção

O indicador `signal-pulse` (`portal-web/src/components/dashboard/TelemetryActivity.tsx`, estilizado em `styles.css`) é a aplicação mínima e correta do conceito: um ponto ciano com um anel que se expande e desaparece, ao lado do valor "Última telemetria" — só aparece quando existe timestamp real, respeita `prefers-reduced-motion`, e comunica literalmente "há um pulso de dado chegando", não é decoração genérica.

### Anti-exemplo

Adicionar uma ilustração de rede de nós conectados como plano de fundo do Dashboard, ou uma animação de "partículas fluindo" no header — isso violaria "sóbrio", "profissional" e a regra explícita de não inserir ilustrações grandes nas telas operacionais.

## Onde isso deve aparecer depois

Login, onboarding, empty states maiores, o site institucional e a documentação pública são os lugares corretos para expressar esse vocabulário com mais presença (por exemplo, uma trilha sutil atrás do card de login, ou um nó pulsante no empty state de "nenhum dispositivo cadastrado"). Cada aplicação futura deve ser uma decisão própria, revisada contra os princípios acima — este documento não pré-aprova nenhuma implementação específica.
