# Fluxo — Plano de ação E1, E2, E3 e E5

Estado do plano: aprovado para execução incremental.

Este documento orienta a próxima sequência de trabalho no Claude Code. O objetivo é concluir a
experiência de alertas, comprová-la de ponta a ponta, fechar o piloto controlado e então entregar
inteligência operacional determinística. Os épicos E4 (simulação de 1000 devices) e E6 (escala e
HA) ficam deliberadamente em stand by até os gates da seção 8 serem cumpridos.

## 1. Resultado esperado

Ao final deste plano, o Fluxo deve:

- permitir criar, editar, ativar, consultar e acompanhar alertas pelo portal;
- comprovar por testes o caminho telemetria → avaliação → evento → entrega → reconhecimento;
- possuir evidência operacional de TLS, credenciais, backup/restore, retenção, health e ensaio de
  24 horas no piloto;
- detectar e explicar stale/no-data, gaps de sequence, rejection rate, sensor travado, faixa
  esperada e rate-of-change sem ML no hot path;
- manter APIs, ingestão, isolamento por workspace e decisões arquiteturais já aceitas.

## 2. Regras de execução para o Claude Code

Antes de cada entrega:

1. ler `docs/project-status.md` e os documentos normativos citados na etapa;
2. confirmar branch, HEAD e working tree; preservar qualquer mudança preexistente;
3. criar uma branch de escopo único a partir de `main`;
4. implementar apenas uma entrega vertical revisável por PR;
5. executar os gates proporcionais ao risco e anexar os resultados à PR;
6. atualizar `docs/project-status.md` e o checklist/relatório correspondente somente com evidência
   realmente executada;
7. não declarar uma fase concluída enquanto seus critérios de saída permanecerem abertos.

Não fazer durante este plano:

- iniciar simulador de 1000 devices, benchmark de escala ou dimensionamento para essa meta;
- implementar HA, cluster de broker/banco, escalabilidade horizontal ou arquitetura distribuída;
- introduzir ML/LLM na ingestão, avaliação de alertas ou diagnóstico operacional;
- redesenhar ADR-0002/ADR-0005 durante a implementação;
- implementar `NoData` como operador do `AlertEvaluationWorker`;
- adicionar webhook sem fechar previamente sua política de SSRF e transporte.

Os testes existentes com 10–100 devices continuam autorizados como regressão e evidência do
piloto. Isso não constitui execução do E4.

## 3. Sequência e dependências

```text
Baseline completa
├── E1 — Portal de alertas ──> E2 — Validação ponta a ponta ──┐
└── E3 — Piloto controlado ───────────────────────────────────┤
                                                             v
                                                gate de consolidação
                                                             |
                                                             v
                                            E5 — Inteligência operacional
```

E3 pode avançar em paralelo lógico com E1/E2, desde que cada execução operacional use ambiente e
evidência identificados. E5 começa depois que o modelo de alertas estiver estável e o baseline
relacional estiver verde.

## 4. Marco 0 — Baseline obrigatória

### M0.1 — Revalidar o repositório

Entregáveis:

- build .NET aprovado;
- testes unitários e de integração aprovados com PostgreSQL descartável real e
  `FLUXO_TESTS_REQUIRE_POSTGRES=1`;
- testes e build do portal aprovados;
- E2E atual aprovado;
- `docker compose config` aprovado nos perfis dev e controlled-prod;
- números e eventuais skips registrados em `docs/project-status.md`.

Gate: nenhum teste relacional pode ser contado como aprovado se foi ignorado por falta de banco.
Falhas encontradas na baseline devem gerar correções separadas antes do E1.

## 5. E1 — Portal de alertas

Documentos obrigatórios: `docs/adr/0002-alert-evaluation-state-and-delivery.md`,
`docs/adr/0005-alertas-canais-historico-isolamento-proposta.md`,
`docs/product/alertas-especificacao.md`, `docs/frontend/design-system.md` e
`docs/frontend/ui-release-checklist.md`.

### E1.1 — Contratos e camada de serviço

Escopo:

- mapear os contratos reais de `AlertsController` para tipos TypeScript;
- criar serviço HTTP para regras, revisões, eventos, histórico, reconhecimento e diagnóstico;
- normalizar paginação, erros seguros e cancelamento de requests;
- adicionar testes de contrato/serviço com payloads equivalentes ao JSON do navegador.

Aceite: nenhum `any`, nenhum enum inferido apenas pelo texto da UI e nenhum segredo ou detalhe
interno exposto em mensagem de erro.

### E1.2 — Lista e estado das regras

Escopo:

- adicionar rota e navegação contextual de Alertas;
- listar regras paginadas com nome, métrica, device/escopo, condição, severidade e estado;
- representar loading, vazio, erro e retry seguro;
- permitir ativar/desativar somente se o contrato existente suportar a operação sem ambiguidade.

Aceite: isolamento por workspace preservado, rota direta funciona após reload e a informação não
depende apenas de cor.

### E1.3 — Criar e editar regra

Escopo:

- formulário guiado pelo `ValueType` da métrica;
- operadores compatíveis com Numeric, Boolean e Text;
- device específico ou todos os devices compatíveis do workspace;
- duração, gap/default esperado, histerese, cooldown e severidade quando aplicáveis;
- validação no cliente como apoio, mantendo o servidor autoritativo;
- confirmação clara para mudanças que alteram uma regra ativa.

Aceite: combinações inválidas não são enviadas; editar cria/preserva revisão conforme o backend;
regras globais não misturam o estado de devices distintos.

### E1.4 — Eventos, histórico e reconhecimento

Escopo:

- listar eventos ativos e resolvidos;
- filtrar por estado/severidade sem inventar paginação client-side;
- exibir device, regra, métrica, valor, ocorrência, ingestão e freshness;
- abrir histórico de transições;
- reconhecer evento com feedback idempotente e autoria visível quando disponível.

Aceite: timestamps e timezone são explícitos; refresh não perde o contexto; reconhecimento
duplicado não cria transição duplicada.

### E1.5 — Diagnóstico de entrega e hardening visual

Escopo:

- expor estado da fila/entregas usando apenas os dados autorizados pelo endpoint existente;
- diferenciar pendente, em processamento, entregue, retry, falha permanente e dead letter;
- revisar teclado, foco, zoom, 1600/1024/768/375 px e alternativas textuais;
- acrescentar smoke test da nova rota.

Gate de saída do E1:

- todas as operações previstas podem ser realizadas no portal;
- testes semânticos dos componentes e build estão verdes;
- nenhuma alteração regressiva em autenticação, ingestão ou Telemetry Explorer;
- documentação do portal e `project-status.md` refletem o estado real.

## 6. E2 — Alertas ponta a ponta

### E2.1 — Harness determinístico

Criar utilitários de teste que provisionem workspace/device, publiquem Schema V2 com sequence e
tempo controlados, consultem o resultado pela API e aguardem processamento com timeout limitado.
Cada execução deve ter identificador próprio e limpeza segura.

### E2.2 — Golden path

Cobrir, no mínimo:

1. usuário cria regra no portal;
2. device publica leituras que satisfazem condição e duração;
3. worker abre um único evento;
4. evento e histórico aparecem no portal;
5. intenção/entrega chega ao estado esperado para o canal configurado;
6. usuário reconhece o evento;
7. leitura de resolução fecha o evento sem criar duplicata.

### E2.3 — Confiabilidade e isolamento

Casos obrigatórios:

- histerese, cooldown, gap e telemetria fora de ordem;
- work item reprocessado sem evento duplicado;
- falha de uma regra não bloqueia outras;
- retry, lease expirado e dead letter;
- regra global mantém estado por device;
- usuário de outro workspace não lê nem altera regra, evento, histórico ou entrega;
- destinatário/relação removida antes do envio não recebe entrega, quando aplicável ao canal.

### E2.4 — Canal de entrega

Se o backend já possuir adaptador utilizável, testar o canal com um sink local controlado. Se a
escolha/configuração do provedor ainda estiver aberta, registrar o gate e não inventar credenciais
ou fornecedor. O núcleo avaliação/evento/reconhecimento pode ficar verde, mas a Fase 3 só é
declarada totalmente concluída quando o canal assumido pelo escopo possuir evidência real.

Gate de saída do E2:

- golden path executa no CI sem depender de serviço externo instável;
- testes negativos comprovam autorização e isolamento;
- comportamento de retry/idempotência possui evidência relacional;
- `project-status.md` distingue claramente núcleo comprovado e qualquer canal ainda pendente.

## 7. E3 — Piloto controlado

Fonte autoritativa: `docs/checklist-producao-controlada.md`. Cada checkbox só pode ser marcado com
data, ambiente, comando/procedimento, resultado e referência à evidência sem segredos.

### E3.1 — Segurança e rede

- comprovar TLS HTTP no proxy/terminador e TLS MQTT em 8883;
- comprovar que 1883 não está publicamente exposta;
- validar hostname, cadeia e validade dos certificados;
- validar issuer, audience, assinatura JWT e HSTS no contexto correto;
- fazer varredura de segredos versionados;
- testar rotação de credencial MQTT, bloqueio da antiga e continuidade com a nova.

### E3.2 — Backup, restore e retenção

- executar backup pelo script existente;
- restaurar em banco descartável separado e validar contagens/amostras/integridade;
- medir duração, tamanho e RPO/RTO observado;
- definir retenção inicial para telemetria, rejeições, alertas e trabalho concluído;
- documentar arquivamento/exclusão sem implementar downsampling ou HA prematuramente.

### E3.3 — Operação observável

- confirmar health checks de API, Worker, Mosquitto e PostgreSQL;
- confirmar logs úteis e ausência de tokens, senhas e payload sensível;
- registrar consumo de CPU, memória, disco, backlog e rejeições durante o piloto;
- testar restart controlado de cada componente e recuperação esperada;
- validar procedimento de encerramento sem remover volumes acidentalmente.

### E3.4 — Devices e ensaio sustentado

- evoluir e revalidar o piloto simulado conforme
  `docs/simulacao-piloto-industria-alimentos-100-devices.md`: um tenant, 10 workspaces e 10
  devices por workspace, incluindo produção, qualidade, logística, utilidades e áreas
  administrativas;
- gerar dados stateful, correlacionados e reproduzíveis, com calendário industrial, intervalos
  por perfil e anomalias controladas; preservar também o modo uniforme de carga simples;
- executar smoke progressivo com 10, 50 e 100 devices conforme necessidade de regressão;
- executar Gateway Pi por 24h no perfil controlled-prod;
- executar ESP32 por 24h quando o hardware estiver disponível;
- revisar reconexões, gaps, duplicatas, spool, desgaste/volume de escrita e telemetria aceita;
- revogar o provisionamento Gateway antigo registrado como sem uso;
- integrar sensor físico quando houver hardware identificado, sem bloquear as demais evidências.

### E3.5 — Fechamento documental

Revisar o guia do piloto, backup/restore e simulador. Produzir relatório datado com ambiente,
versões, limitações e todos os itens ainda abertos. Ausência de hardware deve permanecer explícita;
não converter “não executado” em aprovação documental.

Gate de saída do E3: todos os itens obrigatórios para o ambiente efetivamente exposto estão
fechados; exceções possuem responsável, risco aceito e prazo. O piloto não declara capacidade de
1000 devices.

## 8. Gate de consolidação e stand-by de E4/E6

E4 e E6 somente podem ser replanejados quando todos os itens abaixo forem verdadeiros:

- E1 e E2 concluídos, incluindo canal definido para o escopo do MVP;
- baseline completa verde com PostgreSQL real e E2E;
- E3 encerrado com backup/restore e ensaio sustentado documentados;
- política inicial de retenção aprovada;
- métricas e logs permitem localizar o primeiro gargalo;
- contratos de API, Schema V2 e alertas não estão passando por mudança estrutural frequente;
- riscos e SLOs da próxima prova estão explicitamente definidos.

“Aplicação consolidada” significa esses gates observáveis, não apenas ausência recente de bugs.
Ao liberar o stand-by, planejar primeiro E4 como prova progressiva e usar suas medições para
desenhar E6. Não escolher HA antes de conhecer o gargalo e o SLO.

## 9. E5 — Inteligência operacional determinística

Documentos obrigatórios: seção “Stale/no-data — Fase 4” do ADR-0002 e
`docs/product/mvp-scope.md`. Implementar cada detector como uma entrega vertical com estado,
explicação, API, portal, teste e observabilidade.

### E5.1 — Modelo comum de saúde e eventos

- definir `DeviceHealthEvent` e estados/transições sem reutilizar `AlertEvent` indevidamente;
- garantir índice/constraint que impeça dois eventos stale ativos para o mesmo device;
- definir reason codes estáveis, evidências usadas no cálculo e timestamps;
- manter isolamento por workspace e autorização em repositório, aplicação e API.

### E5.2 — Stale/no-data

- criar `DeviceStaleEvaluationWorker` periódico, separado do worker de alertas;
- usar `Device.LastContactAtUtc` e a fórmula normativa;
- usar default compartilhado de intervalo esperado;
- exigir ciclos de confirmação para stale e recuperar online na primeira leitura nova;
- comprovar restart e concorrência entre instâncias sem eventos duplicados.

### E5.3 — Qualidade de comunicação

- calcular gaps de sequence, duplicatas e rejection rate em janela definida;
- não transformar ausência de evidência em zero saudável;
- apresentar contadores, janela e razão do estado no portal;
- validar sequences reiniciadas/rotacionadas conforme o contrato real do device.

### E5.4 — Qualidade do sinal

- sensor travado: janela, mínimo de amostras e tolerância explícitos;
- faixa esperada: limites configurados por métrica/device, sem regra vertical implícita;
- rate-of-change: unidade temporal, gap máximo e tratamento de outliers explícitos;
- estatística robusta somente determinística e explicável nesta fase.

### E5.5 — Health score explicável

- combinar sinais com pesos/regras documentados e versionados;
- retornar score, classificação, freshness e lista de fatores contribuintes;
- nunca esconder um estado crítico atrás de uma média;
- mostrar no dashboard e detalhe do device com alternativa textual completa.

### E5.6 — Retenção, testes e operação

- aplicar a política aprovada a eventos derivados;
- testar bordas temporais com relógio controlado;
- testar isolamento, idempotência, restart e dados atrasados;
- medir custo dos workers no volume do piloto, não no perfil de 1000 devices;
- criar runbook para falso positivo, reprocessamento e diagnóstico.

Gate de saída do E5:

- cada diagnóstico é explicável por dados e parâmetros visíveis;
- nenhuma detecção depende de ML/LLM;
- nenhuma ausência é mascarada como saúde normal;
- workers sobrevivem a restart e concorrência sem duplicação;
- API, portal, testes e retenção estão alinhados.

## 10. Ordem sugerida de PRs

1. `chore/baseline-pos-alertas`
2. `feat/portal-alert-contracts`
3. `feat/portal-alert-rules`
4. `feat/portal-alert-rule-editor`
5. `feat/portal-alert-events`
6. `feat/portal-alert-delivery-diagnostics`
7. `test/alerts-e2e-harness`
8. `test/alerts-e2e-reliability`
9. `docs/pilot-security-evidence`
10. `docs/pilot-backup-retention-evidence`
11. `feat/pilot-scenario-manifest`
12. `feat/pilot-multi-workspace-provisioning`
13. `feat/pilot-stateful-device-profiles`
14. `feat/pilot-controlled-anomalies`
15. `test/pilot-scenario-validation`
16. `docs/pilot-24h-evidence`
17. `feat/device-health-events`
18. `feat/device-stale-evaluation`
19. `feat/device-communication-health`
20. `feat/device-signal-health`
21. `feat/device-health-score`

Itens operacionais do E3 podem exigir pequenos fixes de código/infraestrutura. Nesses casos,
abrir PR `fix/...` própria antes de registrar a evidência; não misturar correção e relatório de
execução numa alteração ampla.

## 11. Gate comum de qualidade

Para toda PR de código, executar o subconjunto relevante e, antes de fechar cada épico, a suíte
completa:

```powershell
dotnet build Fluxo.slnx
dotnet test Fluxo.slnx

Set-Location portal-web
npm test
npm run build

Set-Location ../e2e
npm test
```

Testes relacionais devem usar o PostgreSQL descartável documentado. Alterações no portal seguem
também `docs/frontend/ui-release-checklist.md`. Resultados devem informar aprovados, falhos e
ignorados; “verde” sem contagem ou com dependência ausente não é evidência suficiente.
