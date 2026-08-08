# Telemetry Explorer

## Objetivo e escopo

O Telemetry Explorer é a ferramenta de análise técnica de séries temporais do Fluxo Portal. Esta evolução atua exclusivamente na apresentação e na interação do portal. Aquisição, MQTT, ingestão, persistência, schema e contratos HTTP permanecem inalterados.

## Fluxo da consulta

A configuração segue uma ordem fixa:

1. **Dispositivos:** até 10 dispositivos. Cada opção mostra nome amigável, identificador técnico e estado operacional já disponível.
2. **Métricas:** até 10 métricas. A lista é pesquisável e mostra nome de exibição, `metricKey`, tipo e unidade canônica fornecida pelo catálogo.
3. **Período:** atalhos de última hora, 6 horas e 24 horas, além dos campos manuais `De` e `Até`. Datas são exibidas no horário local e enviadas à API em UTC, como antes.
4. **Agregação:** somente operações compatíveis com todos os tipos selecionados são oferecidas.
5. **Bucket:** habilitado para consultas agregadas e ajustado ao menor intervalo aceito para a duração escolhida.
6. **Executar consulta:** envia o mesmo payload existente. O limite do backend de 25 séries por consulta é antecipado na interface.

O período selecionado fica visível na configuração e o período efetivamente retornado aparece no cabeçalho dos resultados, junto de agregação, bucket, quantidade de pontos e tempo de execução.

## Nomes e metadata

O nome configurado pelo backend é prioritário quando difere da `metricKey`. Na ausência de um nome próprio, a interface apenas transforma separadores técnicos em espaços e preserva siglas conhecidas. Isso melhora a leitura sem criar traduções de domínio potencialmente incorretas.

A `metricKey` original permanece sempre visível como identificação secundária. `canonicalUnit` e `semanticType` são tratados como fonte de verdade:

- uma unidade só é exibida quando fornecida pelo backend;
- metadata ausente aparece como **Unidade não informada**;
- o portal não deduz `°C`, `%`, bytes ou segundos a partir do nome da métrica;
- a ausência de metadata não altera nem corrige dados persistidos.

## Painéis e unidades

As séries só compartilham um painel quando tipo de valor, tipo semântico e unidade canônica são explicitamente compatíveis. Quando unidade ou tipo semântico estão ausentes, cada `metricKey` recebe seu próprio painel. Séries da mesma métrica em dispositivos diferentes continuam comparáveis no mesmo painel.

Essa regra evita colocar temperatura, umidade, disco, carga e contadores no mesmo eixo. Também elimina o título genérico “Numeric · sem unidade”. Cada painel usa o nome da métrica, a chave técnica, o tipo e a informação de unidade disponível.

Valores booleanos usam eixo `true`/`false` e linha em degraus. Texto e agregação `count` são apresentados em tabela, pois uma linha contínua não representa esses dados corretamente.

## Gráficos

- As cores vêm de uma paleta fixa e são derivadas da identidade `deviceId + metricKey`; portanto, não mudam a cada renderização.
- A legenda compacta mostra nomes úteis e mantém a identificação completa no atributo de ajuda.
- O tooltip mostra timestamp local completo, dispositivo, nome, chave técnica e valor com unidade, quando existente.
- O eixo temporal adapta o nível de detalhe à duração retornada.
- Valores nulos não são convertidos em zero e não são conectados artificialmente.
- Séries parcialmente vazias são informadas sem ocultar as demais.

## Agregações e buckets

Compatibilidade preservada:

| Tipo | Agregações |
|---|---|
| Numeric | `raw`, `avg`, `min`, `max`, `sum`, `count`, `last` |
| Boolean | `raw`, `count`, `last` |
| Text | `raw`, `count`, `last` |

Quando há tipos diferentes selecionados, a interface oferece apenas a interseção segura. `raw` mantém o bucket nulo. Consultas raw continuam limitadas a 24 horas e agregadas a 90 dias.

Buckets disponíveis: `1m`, `5m`, `15m`, `1h`, `6h` e `1d`. O mínimo aumenta com a duração para respeitar os limites atuais da API; erros de bucket retornados pelo backend continuam visíveis ao operador.

## Estados da interface

- **LoadingState:** distingue carregamento de dispositivos, catálogo de métricas, primeira consulta e atualização de um resultado existente.
- **EmptyState:** orienta configuração inicial, ausência de dispositivos e ausência de pontos no período.
- **ErrorState:** apresenta a mensagem da API e, quando a seleção ainda é válida, permite tentar novamente.
- Resultados anteriores são preservados visualmente durante atualização ou erro, evitando perda de contexto.
- Respostas truncadas recebem aviso explícito para reduzir período ou seleção.

## Validação com o EdgeWarden

A validação foi feita em modo somente leitura sobre os dados existentes do dispositivo `Edgewarden Gateway` (`edgewarden`). Foram confirmadas séries reais para:

- `gateway.cpu_temperature_c`;
- `gateway.disk_used_percent`;
- `environment.temperature_c`;
- `environment.humidity_percent`.

O catálogo atual dessas métricas não fornece `CanonicalUnit` nem `SemanticType`, e o `DisplayName` repete a chave técnica. Por isso, a interface exibe **Unidade não informada** e mantém essas grandezas em painéis separados. Nenhum valor ou registro foi alterado para a validação visual.

## Limitações atuais

- Sem `CanonicalUnit` e `SemanticType`, o portal não pode rotular eixos com unidades nem agrupar métricas equivalentes com segurança.
- O mecanismo de nome amigável é deliberadamente mecânico quando o catálogo não fornece `DisplayName`; não substitui metadata de domínio.
- A consulta aceita no máximo 10 dispositivos, 10 métricas e 25 séries combinadas, conforme os guardrails existentes.
- Timestamps são apresentados no fuso do navegador; o valor UTC original permanece no payload e no atributo `dateTime`.
- A visualização não faz downsampling no cliente. Agregação, limites e truncamento continuam sob responsabilidade da API existente.
