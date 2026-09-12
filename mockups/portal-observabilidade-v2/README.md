# POC — Observabilidade do dispositivo (Fluxo Portal)

Protótipo navegável que acompanha o relatório
[`docs/frontend/revisao-portal-2026-09-07/relatorio-avaliacao.md`](../../docs/frontend/revisao-portal-2026-09-07/relatorio-avaliacao.md).

**Isto NÃO é o portal oficial.** Nada aqui é importado de `portal-web/`, e o portal oficial
não foi alterado. Todos os dados são **simulados** (`demo-data.js`, seed fixa) e estão marcados
como "DEMONSTRAÇÃO" na interface.

## O que a POC demonstra

- **Visão operacional consolidada** de métricas nativas do dispositivo (relatório §7): tiles com
  valor atual em unidade nativa, tendência, pico, faixa esperada e estado de freshness real,
  agrupados em Recursos / Conectividade / Processamento. Cada tile marca a origem do dado
  (`derivado` = calculado no portal, `backend` = depende de mudança no backend).
- **Gráfico consolidado com duas estratégias de escala** (relatório §8):
  - *Pequenos múltiplos* (padrão) — uma faixa por métrica, todas no mesmo eixo de tempo, escala
    Y independente com unidade nativa, cursor sincronizado entre as faixas.
  - *Sobreposição normalizada* (opcional) — todas as séries em 0–100%, com aviso permanente e
    visível de que a escala foi normalizada e o valor real no cursor.
- **Períodos** 1 h / 6 h / 24 h / **Ver tudo**.
- **Legenda interativa** — clique isola uma série, clique de novo restaura; Ctrl/Shift+clique
  combina; séries ocultas aparecem riscadas; "Ver tudo" restaura.
- **Alternativa textual ao gráfico** — resumo do comportamento por série (atual/mín/máx/média/
  tendência/lacunas) e tabela de dados acessível em `<details>`.
- **Tooltip/cursor acessível** — no modo sobreposição o SVG é focável e as setas ←/→ (Home/End)
  percorrem o tempo; a leitura sai em `aria-live`.
- **Eventos correlacionados** — co-ocorrência de mudanças, sem inferir causa.
- **Estados**: operação normal, dispositivo offline, dado atrasado (atraso de ingestão), sem
  histórico anterior, rejeições subindo — troque pelo seletor "Cenário".
- **Preferências combináveis** (botão "Aparência", relatório §6.2): tema (automático/claro/
  escuro), contraste (padrão/alto/reduzido), escala de texto (100/112/125%), movimento
  (conforme o sistema / sempre reduzido). Persistem em `localStorage`; o padrão respeita
  `prefers-color-scheme` / `prefers-reduced-motion` / `prefers-contrast`.
- **Paleta de gráfico daltônico-segura** (Okabe–Ito) + padrão de traço por série — sempre ativa.
- Responsivo (desktop / tablet / 375 px) e navegável por teclado.

## Como executar

Sem build, sem dependências.

### Opção A — abrir o arquivo

```
start D:\Officina404\Fluxo\mockups\portal-observabilidade-v2\index.html
```

(ou duplo clique em `index.html`)

### Opção B — servir a pasta (recomendado; evita restrições de `file://`)

```
cd D:/Officina404/Fluxo/mockups/portal-observabilidade-v2
python -m http.server 4180
```

Abra `http://localhost:4180/index.html`.

## Arquivos

| Arquivo | Papel |
|---|---|
| `index.html` | estrutura e regiões acessíveis |
| `styles.css` | sistema visual + camadas de tema (`data-theme` / `data-contrast` / `data-text-scale` / `data-motion`) |
| `demo-data.js` | gerador determinístico de telemetria simulada (unidades imitam `gateway-spool.js`) |
| `app.js` | render, gráficos SVG (sem biblioteca), interações |

## Limites conscientes da POC

- Dados sintéticos; volumes e taxas são ilustrativos.
- Os gráficos SVG são simplificados (downsampling fixo, sem zoom/pan) — a implementação real
  deve decidir entre evoluir isso ou usar o Recharts já presente no portal.
- "Faixa esperada" nos tiles usa valores fixos do `demo-data.js`; no portal real viria de
  `MetricDefinition.Min/MaxExpectedValue` (hoje no domínio, ainda não no DTO — relatório §7).
- Não há chamada de rede, autenticação nem persistência de servidor.
