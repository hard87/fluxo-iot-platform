# Escopo do MVP comercial do Fluxo

## Tese

O Fluxo é uma plataforma que **serve e interpreta dados sem assumir o processo de negócio do
cliente** — não uma ferramenta de automação/workflow visual, e não uma suíte industrial completa.
"Genérico" significa: um envelope versionado, um conjunto pequeno de tipos escalares (ver
[ADR-0001](../adr/0001-telemetry-schema-v2.md)), catálogo de métricas por workspace,
auto-descoberta controlada e projeção consultável independente do fabricante do device.
"Inteligente" significa reduzir trabalho de configuração e transformar dado em sinal útil — no
MVP, majoritariamente via lógica determinística e estatística barata, não IA generativa no hot
path (ver regra de custo abaixo).

## Regra de custo — não negociável

Nenhum modelo de IA participa do caminho MQTT → ingestão → persistência → alerta. A plataforma
continua recebendo e avaliando telemetria mesmo com a camada de IA desabilitada, sem crédito de
API ou acesso externo. IA generativa, quando adicionada, é uma assistente sob demanda que recebe
resumos agregados (nunca stream bruto, credenciais ou secrets), com quota, cache e
provider abstraction — ver seção "IA generativa" abaixo.

## O que entra no MVP

| Capacidade | O cliente percebe | Por que entra |
|---|---|---|
| Provisionamento seguro | Cada device tem identidade e credencial próprias | Já implementado e endurecido — diferencial operacional existente |
| Telemetria histórica genérica | Qualquer device compatível envia métricas sem pedir mudança de banco | Remove a rigidez do schema atual (ADR-0001) |
| Explorer de telemetria | Seleciona devices, métricas, período e agregação | Transforma armazenamento em produto |
| Alertas configuráveis | Cria regras de negócio simples sem alterar firmware | Valor imediato para operação (ADR-0002) |
| Saúde e anomalias básicas | Fluxo mostra dispositivo parado, sensor travado ou mudança incomum | Entrega "inteligência" com baixo custo |
| Dashboard operacional | Administrador vê ingestão, rejeição, duplicatas e backlog | Permite operar o SaaS com confiança |
| Templates por perfil | Começa com dashboard sugerido para ambiente, máquina ou mobilidade | Reduz curva de adoção sem criar vertical fechada |

## Inteligência barata (sem LLM no hot path)

| Insight | Algoritmo MVP | Saída para o cliente |
|---|---|---|
| Device stale | `now - last_seen > max(2 × expected_interval, stale_threshold)` | Offline/stale com tempo sem dados |
| Sensor travado | N amostras consecutivas idênticas, ou variância abaixo de epsilon configurável | Flag "possível sensor travado" |
| Valor fora do esperado | `MinExpectedValue`/`MaxExpectedValue` da `MetricDefinition` | Evento de anomalia + sugestão de regra |
| Mudança abrupta | Taxa de variação por unidade de tempo acima do limite configurado | Spike/drop detectado |
| Anomalia estatística | Mediana móvel + MAD em janela; dispara quando desvio robusto cruza limiar | Anomalia sem exigir modelo treinado |
| Qualidade de ingestão | Rejeições, duplicatas e gaps de sequence por device | Health score de comunicação |
| Sugestão de dashboard | `SemanticType` → widget/template determinístico | Dashboard inicial em poucos cliques |

Persistir apenas eventos de insight relevantes em `TelemetryInsightEvent` — não criar uma segunda
linha de "análise" para cada `TelemetryPoint" (controla volume e custo).

## IA generativa — recurso opcional e comercialmente controlado

Fora do hot path. Entrada permitida: lista de `MetricDefinition`, estatísticas agregadas,
últimos `InsightEvent`, contexto do usuário. Entrada proibida por padrão: stream bruto contínuo,
credenciais, tokens MQTT, secrets, banco inteiro. Controles obrigatórios: feature flag, provider
abstraction, quota por workspace/plano, timeout, cache por hash da entrada agregada, registro de
custo/tokens, fallback (plataforma funciona sem IA). Caso de uso inicial sugerido: "assistente de
configuração" — sugere nomes amigáveis, `SemanticType`, dashboards e regras iniciais a partir do
catálogo; sugestão é revisável e não altera alertas/dispositivos autonomamente no MVP.

## Confronto de escopo — o que NÃO entra no MVP

"Dashboard flexível" e "filtros dinâmicos" **não autorizam construir um editor visual de
centenas de widgets no MVP** (não é para replicar o front-end do ThingsBoard). A primeira tela de
série histórica é um **Explorer**, não um construtor de dashboard universal:

1. Primeiro: Explorer funcional e rápido (seleciona devices, métricas, intervalo, agregação e
   bucket; visualiza séries).
2. Depois: salvar a consulta como `ViewPreset` (nome, filtros, visualização).
3. Só depois — e só se clientes reais demonstrarem necessidade: composição livre de widgets.

O produto precisa vender **consulta e entendimento do dado**, não um editor genérico.

## Critério de "vendável"

A plataforma é genericamente útil quando os três perfis piloto — máquina/energia,
ambiente/bateria e mobilidade — usam o mesmo contrato V2, o mesmo catálogo de métricas, a mesma
Query API e o mesmo motor de alertas sem migration ou fork de backend. É inteligente quando
explica problemas úteis e reduz configuração com mecanismos baratos. É vendável quando faz isso
de forma segura, observável e repetível para até 100 devices em produção controlada — **e**
quando pelo menos um piloto real confirma isso qualitativamente, não só os gates técnicos.

## Referências

- [ADR-0001 — Contrato de telemetria V2](../adr/0001-telemetry-schema-v2.md)
- [ADR-0002 — Motor de alertas](../adr/0002-alert-evaluation-state-and-delivery.md)
- [ADR-0003 — Telemetry Query API](../adr/0003-telemetry-query-api.md)
- `Fluxo_Documento_Confronto_Arquitetural_MVP.docx` (seções 01, 05, 06)
