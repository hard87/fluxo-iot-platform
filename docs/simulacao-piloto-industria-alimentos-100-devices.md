# Simulação de piloto — Indústria de alimentos e bebidas — 100 devices

## 1. Objetivo

Evoluir o simulador MQTT existente para representar um piloto industrial plausível, determinístico
e reproduzível com **um tenant, 10 workspaces e 10 devices por workspace**. A simulação deve
exercitar ingestão, isolamento, catálogo de métricas, dashboards, Explorer, alertas e inteligência
operacional sem pretender substituir validação com hardware físico.

Este cenário pertence ao E3 (piloto controlado). Ele não é o benchmark de 1000 devices do E4.

Vertical escolhida: fábrica de alimentos e bebidas refrigerados. Essa vertical combina processo,
cadeia fria, utilidades, qualidade, logística, manutenção, segurança e áreas administrativas. O
Fluxo continua genérico: os perfis e nomes vivem no simulador; nenhuma regra específica dessa
vertical deve ser adicionada ao domínio, schema ou hot path de ingestão.

## 2. Topologia organizacional

Tenant sugerido: `aurora-alimentos`.

| Workspace | Finalidade | Devices representados |
|---|---|---|
| `01-recepcao-materias-primas` | Docas, silos e recebimento refrigerado | temperatura de doca, duas câmaras, dois silos, balança, portão, compressor, gateway e estação ambiental |
| `02-preparo-e-mistura` | Tanques, misturadores e dosagem | quatro tanques, dois misturadores, dosador, painel elétrico, gateway e estação ambiental |
| `03-pasteurizacao` | Tratamento térmico e transferência | três pasteurizadores, dois trocadores, duas bombas, painel, gateway e estação ambiental |
| `04-envase-e-embalagem` | Linhas de envase, selagem e inspeção | duas envasadoras, duas seladoras, rotuladora, esteiras, detector, painel, gateway e estação ambiental |
| `05-camaras-frias` | Armazenamento refrigerado | quatro câmaras, dois compressores, duas portas monitoradas, gateway e estação ambiental |
| `06-utilidades-e-energia` | Vapor, água, ar comprimido e energia | caldeira, chiller, dois compressores, tratamento de água, medidores elétricos, gerador, gateway e estação ambiental |
| `07-qualidade-e-laboratorio` | Laboratório e retenção de amostras | duas geladeiras, freezer, incubadora, sala limpa, bancada, balança, UPS, gateway e estação ambiental |
| `08-logistica-e-expedicao` | Armazém, docas e preparação de carga | duas docas, duas áreas refrigeradas, empilhadeira, balança, portões, painel, gateway e estação ambiental |
| `09-manutencao-e-seguranca` | Oficina, almoxarifado e EHS | torno, compressor, estoque de peças, estoque químico, bomba de incêndio, reservatório, painel, gateway e duas estações ambientais |
| `10-administrativo-ti-facilities` | Escritórios, sala de reunião, CPD e infraestrutura predial | dois ambientes de escritório, sala de reunião, CPD, UPS, quadro elétrico, HVAC, acesso, gateway e estação ambiental |

Os workspaces são fronteiras reais de navegação e autorização, não apenas etiquetas. Cada device
pertence a exatamente um workspace. Testes devem comprovar que usuários restritos a um workspace
não consultam devices, métricas, alertas ou diagnósticos dos demais.

## 3. Famílias de device e métricas

Os 100 devices devem ser instâncias de perfis reutilizáveis. Cada perfil define intervalo nominal,
estado interno, métricas, unidade, faixa plausível, precisão e transições permitidas.

| Perfil | Métricas principais |
|---|---|
| Ambiente | `temperature_c`, `humidity_percent`, `co2_ppm`, `door_open`, `occupancy` |
| Câmara fria | `temperature_c`, `setpoint_c`, `humidity_percent`, `door_open`, `compressor_running`, `power_kw` |
| Tanque/processo | `product_temperature_c`, `level_percent`, `pressure_bar`, `agitator_running`, `batch_state` |
| Pasteurizador/trocador | `inlet_temperature_c`, `outlet_temperature_c`, `flow_l_min`, `pressure_bar`, `cycle_state` |
| Motor/bomba/esteira | `current_a`, `power_kw`, `vibration_mm_s`, `bearing_temperature_c`, `running` |
| Energia/utilidade | `voltage_v`, `current_a`, `active_power_kw`, `power_factor`, `energy_kwh`, `status` |
| Água/ar/vapor | `pressure_bar`, `flow_m3_h`, `temperature_c`, `consumption_total`, `valve_open` |
| Gateway | `cpu_temperature_c`, `memory_used_percent`, `load_1m`, `disk_used_percent`, `spool_depth`, `network_rssi_dbm` |
| Qualidade/laboratório | temperatura, umidade ou peso conforme equipamento, `within_spec`, `calibration_due_days`, `device_state` |
| Predial/administrativo | `temperature_c`, `humidity_percent`, `co2_ppm`, `occupancy`, `power_kw`, `access_state` |

Tipos Numeric, Boolean e Text do Schema V2 devem aparecer em todos os workspaces onde fizer
sentido. Unidades e chaves devem ser estáveis; não criar uma nova `MetricDefinition` a cada
mensagem ou device.

## 4. Modelo de comportamento

### 4.1 Sinais contínuos plausíveis

Não gerar números independentes e uniformemente aleatórios. Cada twin mantém estado e usa:

- valor-base por device;
- ciclo diário e turno de produção;
- ruído limitado e suavizado;
- inércia/rampa para temperatura, pressão, nível e vibração;
- correlação entre métricas relacionadas;
- limites físicos e precisão coerentes com o sensor;
- acumuladores monotônicos para energia, água, vapor e uptime.

Exemplos de correlação:

- porta de câmara aberta eleva gradualmente temperatura e umidade e aumenta o duty cycle do
  compressor;
- motor ligado eleva corrente imediatamente e temperatura/vibração com inércia;
- ocupação aumenta CO2 e carga do HVAC no administrativo;
- início de lote altera `batch_state`, nível, agitador e temperatura numa sequência válida;
- queda de sinal aumenta chance de desconexão e crescimento de `spool_depth` no gateway;
- produção parada reduz consumo das linhas, mas mantém cadeia fria e cargas de base.

### 4.2 Calendário operacional

Usar timezone configurável, default `America/Sao_Paulo`, mantendo timestamps MQTT em UTC.

- produção: dois turnos, segunda a sábado;
- higienização/CIP: janela diária após produção;
- administrativo: horário comercial em dias úteis;
- cadeia fria, CPD, gateways e utilidades críticas: operação contínua;
- manutenção planejada: janela configurável;
- execução acelerada opcional: um dia simulado em poucos minutos, sem falsificar timestamps nos
  testes que medem latência real.

### 4.3 Intervalos de publicação

Defaults sugeridos:

- processo crítico e motores ativos: 5 s;
- cadeia fria, utilidades e gateways: 10 s;
- ambiente, qualidade e facilities: 30 s;
- acumuladores administrativos de baixa variação: 60 s.

O intervalo pertence ao perfil/device e deve ser exportado no manifesto do cenário. O relatório
deve calcular a taxa esperada antes da execução. Para uma comparação simples com o baseline,
também deve existir modo uniforme configurável, por exemplo 100 devices a 1 mensagem/s.

## 5. Eventos e anomalias controladas

Cada anomalia deve ser ativada por cenário e seed, ter início/duração conhecidos e produzir uma
expectativa verificável. Nenhuma anomalia pode depender de sorte não registrada.

| Cenário | Comportamento | Resultado esperado |
|---|---|---|
| Porta de câmara aberta | `door_open=true`, temperatura sobe com inércia | alerta de temperatura/duração e posterior resolução |
| Rolamento degradando | vibração e temperatura crescem em rampa | rate-of-change/faixa esperada identificam degradação |
| Bomba cavitando | fluxo cai, vibração sobe e corrente oscila | métricas correlacionadas visíveis no Explorer |
| Sensor travado | valor repete exatamente além da janela | diagnóstico `stuck` sem tratar estabilidade legítima como falha |
| Device sem dados | conexão interrompida por período conhecido | stale após confirmação e recuperação na primeira leitura nova |
| Rede instável | desconexões, reconexões e atraso controlados | gaps/backlog/recovery observáveis |
| Duplicidade | mesma sequence reenviada | ingestão idempotente, sem ponto/evento duplicado |
| Fora de ordem | leitura antiga chega após leitura nova | histórico persiste; estado corrente do alerta não retrocede |
| Payload inválido | tipo incompatível ou campo obrigatório ausente | rejeição classificada, sem contaminar telemetria válida |
| Tentativa cross-workspace | credencial publica em tópico alheio | broker nega pela ACL |
| Pico de consumo | HVAC/chiller e linha elevam potência conjuntamente | energia e operação permanecem coerentes |
| Falha do CPD | temperatura/UPS degradam em sequência | área administrativa participa dos alertas operacionais |

O cenário normal deve ocupar a maior parte da execução. Injetar falha contínua em todos os
devices produziria carga artificial e reduziria a utilidade do piloto.

## 6. Evolução dos scripts

### 6.1 Manifesto declarativo

Criar um manifesto versionado, sem credenciais, contendo:

- tenant e workspaces;
- 100 devices com identificador, nome, categoria, perfil e seed;
- métricas, unidades e intervalo esperado;
- calendário e parâmetros-base;
- timeline de anomalias;
- versão do cenário.

Sugestão de localização:
`scripts/scenarios/food-beverage-pilot-100/scenario.json` e documentação próxima. Credenciais
geradas devem ficar em arquivo separado, ignorado pelo Git.

### 6.2 Provisionamento idempotente

Evoluir `scripts/provision-simulated-devices.py` ou criar um orquestrador específico para:

- criar/reutilizar o tenant de teste e os 10 workspaces;
- criar exatamente 10 devices em cada workspace;
- retomar execução interrompida sem duplicar entidades;
- emitir manifesto de runtime com IDs, tópicos e credenciais;
- mascarar segredos em stdout;
- falhar se a topologia real divergir da declarada, salvo opção explícita de reconciliação.

Não implementar exclusão automática ampla. Limpeza deve exigir alvo exato e confirmação explícita.

### 6.3 Motor stateful

Evoluir `scripts/mqtt-device-simulator.py` preservando o modo atual de carga simples. O novo modo
de cenário deve:

- manter estado independente por device;
- gerar sinais correlacionados e determinísticos por seed;
- suportar ritmos diferentes sem criar uma thread por device;
- usar credencial e tópico provisionados, TLS e QoS do piloto;
- preservar sequence durante reconexão dentro da execução;
- permitir pause/resume e encerramento gracioso;
- registrar eventos planejados e realmente publicados.

O modo de cenário com 100 devices pode usar `asyncio`. Essa evolução não autoriza aumentar para
1000 conexões nem executar o E4.

### 6.4 Relatório

Gerar resumo JSON e Markdown com:

- cenário, versão, seed, início/fim e ambiente;
- workspaces/devices previstos e conectados;
- mensagens previstas, publicadas, confirmadas quando aplicável, erros e reconexões;
- taxa por perfil/workspace e latência disponível;
- anomalias injetadas e expectativas;
- contagens consultadas na API/banco, rejeições, gaps e duplicatas;
- alertas e health events esperados versus observados;
- limitações e resultado aprovado/reprovado por gate.

Relatórios reais podem conter identificadores e detalhes operacionais; revisar antes de versionar e
nunca incluir credenciais.

## 7. Etapas de implementação

1. **Contrato do cenário:** schema do manifesto, perfis, validação e exemplo completo.
2. **Provisionamento:** 10 workspaces × 10 devices, idempotência e runtime manifest seguro.
3. **Geradores stateful:** sinais contínuos, calendário e correlações por perfil.
4. **Scheduler:** intervalos mistos, seed, relógio e ciclo de vida dos devices.
5. **Falhas controladas:** anomalias da matriz com expectativas registradas.
6. **Integração:** TLS, QoS, credenciais individuais e ACL negativa.
7. **Oráculo de validação:** confrontar publicados, persistidos, rejeitados, alertas e health.
8. **Ensaio:** smoke curto, 1 h e 24 h, com relatório e atualização do checklist.

Cada etapa deve ser uma PR revisável. Não misturar os 100 perfis, provisionamento, motor e relatório
num único diff.

## 8. Critérios de aceite

- existem exatamente 10 workspaces e 10 devices por workspace;
- pelo menos dois workspaces representam áreas administrativas/suporte, não chão de fábrica;
- todos os 100 devices usam identidade, credencial e tópico próprios;
- o mesmo seed e relógio controlado reproduzem os mesmos valores e anomalias;
- sinais respeitam limites, inércia, correlação e acumuladores definidos;
- operação normal, falhas e recuperação aparecem no Explorer e nos diagnósticos esperados;
- isolamento entre workspaces e ACL negativa possuem teste;
- nenhuma credencial é versionada ou exposta no relatório;
- execução de 24 h não apresenta perda silenciosa; diferenças são explicadas por duplicidade,
  rejeição ou falha contabilizada;
- o relatório permite repetir o ensaio e comparar execuções.

## 9. Fora de escopo

- 1000 devices, dimensionamento de capacidade e HA;
- modelo 3D, representação gráfica de ativos ou protocolo de digital twin padronizado;
- gêmeo bidirecional com desired/reported state;
- simulação física de alta fidelidade ou validação metrológica;
- regras de negócio de alimentos codificadas no backend;
- ML, geração sintética por LLM ou anomalias não explicáveis.

