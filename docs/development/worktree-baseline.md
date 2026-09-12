# Baseline da working tree antes da evolução visual

- **Data da inspeção:** 2026-08-08
- **Repositório:** `D:\Officina404\Fluxo`
- **Branch:** `fix/portal-same-origin-auth`
- **HEAD inspecionado:** `e161400` (`fix(portal): route API requests through same-origin proxy`)
- **Status deste documento:** concluído

## Objetivo

Registrar e classificar integralmente a working tree encontrada antes de
qualquer evolução visual do Fluxo Portal, preservando o ensaio EdgeWarden e
sem alterar o comportamento funcional da plataforma.

## Estado encontrado

O repositório estava funcional, porém com working tree suja:

- 1 arquivo rastreado modificado;
- nenhum arquivo staged;
- 53 arquivos não rastreados, totalizando 4.146.400 bytes;
- 21 desses arquivos (3.910.590 bytes) são evidências de QA da Fase 2;
- containers `fluxo-api`, `fluxo-worker-ingestion`, `fluxo-portal-web` e
  `fluxo-postgres` estavam saudáveis durante a inspeção;
- `fluxo-mosquitto` também estava em execução;
- relatórios locais de drills do harness foram encontrados em diretórios já
  ignorados pelo `.gitignore` específico do harness.

O branch atual não possui upstream configurado. Em relação a `origin/main`, a
história observada possui 11 commits exclusivos no branch e 1 commit exclusivo
no remoto; o merge-base é `4b05084393d593a778788f7b78a165b40154020c`.
Nenhuma operação de merge, rebase, push ou sincronização remota faz parte desta
higienização.

## Alteração rastreada

| Arquivo | Classificação | Avaliação |
|---|---|---|
| `docs/project-status.md` | Documentação legítima do piloto | Atualiza o snapshot para 31/07, marca o Gateway Pi como piloto, registra MQTT/TLS, spool/replay, pendências de 24h, riscos de cartão SD e referência ao ADR-0004. Deve ser versionado com os documentos da Fase 5. |

O diff contém somente documentação: 20 adições e 5 remoções. Não havia
alteração staged.

## Arquivos não rastreados

### Fonte arquitetural na raiz

| Conjunto | Arquivos | Classificação | Ação |
|---|---:|---|---|
| `Fluxo_Documento_Confronto_Arquitetural_MVP.docx` | 1 | Documento legítimo e fonte citada por ADR-0001, ADR-0002 e escopo do MVP | Versionar como fonte arquitetural. SHA-256: `FC4E08DA389038950195D815FA302849217C0D60D03568B7DDE21D0407CD8A85`. |

O metadado interno identifica o título “Fluxo - Documento de Confronto
Arquitetural e Diretriz de MVP Comercial” e autoria de Junior Godoi / Officina
404. Não foi editado nesta atividade.

### Evidências geradas da Fase 2

| Conjunto | Arquivos | Classificação | Ação |
|---|---:|---|---|
| `artifacts/phase2-qa/` | 21 | Artefatos gerados, porém evidência histórica finita e citada no relatório da Fase 2 | Versionar o `report.json` e as 20 capturas. Não adicionar a pasta ao `.gitignore`. |

O `report.json` registra cinco rotas em quatro larguras (360, 768, 1024 e
1440 px), sem erros e sem overflow horizontal. As capturas totalizam cerca de
3,9 MB e não são saídas mutáveis do ensaio atual.

### Gateway Pi de referência

| Conjunto | Arquivos | Classificação | Ação |
|---|---:|---|---|
| `devices/pi-gateway-reference-node/` | 15 | Código, configuração de exemplo, documentação e units legítimos da Fase 5 | Versionar sem modificar comportamento. |

Esse conjunto contém o flow Node-RED, spool persistente, sequence, publicação
MQTT QoS 1/TLS, scripts de deploy/diagnóstico/monitoramento e documentação da
validação física. Ele está implantado ou serve de referência direta para o
Gateway `edgewarden`; portanto, qualquer mudança funcional está fora do escopo
desta higienização.

### Harness do ensaio EdgeWarden

| Conjunto | Arquivos | Classificação | Ação |
|---|---:|---|---|
| `devices/edgewarden-test-harness/` | 13 rastreáveis | Código e documentação legítimos do ensaio de resiliência | Versionar exatamente como encontrado. |
| `data/`, `logs/`, `reports/`, bancos SQLite e `environment` | saídas locais mutáveis | Artefatos gerados/estado de runtime | Manter ignorados; preservar no disco; não versionar nem limpar durante o ensaio. |

O `.gitignore` interno já separa corretamente código de estado mutável. Foram
observados três diretórios de relatório ignorados, produzidos em 08/08/2026.
Os documentos os descrevem como drills e registram que o ensaio oficial de 24h
depende de decisão explícita. Enquanto houver atividade operacional, todo o
conjunto será tratado como ensaio protegido.

### Documentação da Fase 5

| Conjunto | Arquivos | Classificação | Ação |
|---|---:|---|---|
| `docs/adr/0004-pi-gateway-store-and-forward.md` | 1 | Decisão arquitetural legítima | Versionar com a documentação do piloto. |
| `docs/handoff/fase-5-piloto-fisico-gateway-pi-codex.md` | 1 | Handoff e contexto operacional legítimos | Versionar com a documentação do piloto. |
| `docs/handoff/relatorio-fase-5-piloto-fisico-gateway-pi-2026-07-31.md` | 1 | Relatório técnico legítimo | Versionar com a documentação do piloto. |

## Arquivos gerados e política de ignore

O `.gitignore` raiz já cobre builds .NET, `node_modules`, build do frontend,
artefatos ESP-IDF, segredos, certificados, dados/logs Mosquitto, resultados de
testes, cobertura, backups e caches Python. Não foi encontrada justificativa
para alterá-lo.

O `.gitignore` do harness deve ser versionado junto com o código. Ele preserva
os marcadores `.gitkeep` e ignora corretamente:

- bancos SQLite, WAL e SHM;
- `data/`, `logs/` e `reports/` mutáveis;
- logs e arquivo local `environment`.

As capturas de `artifacts/phase2-qa` são geradas, mas não devem ser ignoradas
nesta normalização: são evidência concluída e explicitamente referenciada pelo
relatório de handoff da Fase 2.

## Verificação de segredos

- Nenhum marcador de chave privada foi encontrado nos 53 arquivos não
  rastreados.
- Valores da `.env` encontrados por comparação literal nos novos arquivos
  correspondiam somente a identificadores não secretos já documentados, como
  audience e nome do banco.
- `environment.example` contém o contrato de configuração; o arquivo real
  `environment` permanece ignorado.

Essa verificação reduz risco de commit acidental, mas não substitui gestão de
segredos nem auditoria especializada.

## Riscos identificados

1. **Ensaio em curso ou preparação ativa:** containers e evidências recentes
   impedem reinício, teardown, limpeza de volumes ou testes de integração que
   alterem estado.
2. **Janela de perda já documentada:** o drill encontrou mensagens aceitas por
   PUBACK que podem não chegar à persistência quando o Mosquitto reinicia. Não
   corrigir durante a higienização; preservar a evidência para decisão própria.
3. **Código implantado ainda não versionado:** Gateway Pi e harness poderiam se
   perder ou divergir do dispositivo físico enquanto permanecessem untracked.
4. **História divergente do remoto:** sincronização indiscriminada pode misturar
   o commit squash de `origin/main` com a linha local de 11 commits.
5. **Credencial Gateway órfã:** documentação registra uma credencial/device a
   revogar, mas essa ação é operacional e não deve ocorrer durante o ensaio.
6. **Spool em cartão SD:** escrita, retenção e replay longo continuam sem
   evidência de 24h concluída.
7. **Portal:** bundle acima de 500 kB, sessão em memória e vulnerabilidades
   moderadas do React Router permanecem limitações conhecidas, fora deste
   escopo.

## Itens que não devem ser tocados durante o ensaio

- containers, redes, volumes e health state do stack Fluxo;
- broker Mosquitto, ACL, credenciais, certificados, dados e logs;
- banco PostgreSQL e suas migrations/dados;
- API, worker de ingestão, contratos Schema V2 e endpoints;
- `flow.json`, `gateway-spool.js`, Node-RED e services no Raspberry Pi;
- software, firmware, configuração, credenciais e estado do EdgeWarden;
- diretórios ignorados `data/`, `logs/` e `reports/` do harness;
- sessões SQLite/WAL e relatórios já produzidos pelos drills;
- revogação de devices/credenciais ou simulações de falha;
- testes que publiquem MQTT, reiniciem serviços ou escrevam no banco em uso.

## Ações recomendadas

1. Versionar documentação e fonte arquitetural em commit próprio.
2. Versionar as evidências históricas de QA da Fase 2 separadamente.
3. Versionar o Gateway Pi exatamente como encontrado.
4. Versionar o harness e sua política local de ignore exatamente como
   encontrados.
5. Executar somente validações estáticas/isoladas enquanto o ensaio estiver
   protegido: `dotnet build --no-restore`, testes unitários sem dependência
   externa, testes do frontend e build do frontend.
6. Não executar testes de integração/componente nem `docker compose up/down`.
7. Criar `frontend-baseline.md` com o commit final, comandos, resultados e
   limitações.
8. Confirmar working tree limpa sem stash, exclusão ou movimentação dos dados
   ignorados do ensaio.

## Plano de commits

- `docs: preservar baseline arquitetural e registros do piloto`
- `test(portal): versionar evidências de QA da fase 2`
- `feat(edge): versionar gateway Raspberry Pi de referência`
- `test(edge): versionar harness do ensaio EdgeWarden`
- `docs: registrar baseline seguro antes da evolução visual`

Os nomes representam a natureza histórica do conteúdo encontrado. Esta tarefa
não afirma que código novo foi desenvolvido durante a higienização.

## Resultado da normalização

As ações recomendadas foram executadas sem apagar, mover, editar ou esconder
os artefatos preexistentes:

| Commit | Conteúdo preservado |
|---|---|
| `9432cf5` | fonte arquitetural, ADR-0004, handoffs da Fase 5 e atualização histórica de `project-status.md` |
| `9b4f3d8` | `report.json` e 20 capturas de QA da Fase 2 |
| `2b9edb9` | Gateway Raspberry Pi de referência exatamente como encontrado |
| `dc08a8b` | harness EdgeWarden e política de ignore exatamente como encontrados |

O `.gitignore` raiz não foi alterado. Os três diretórios locais de relatório
do harness permanecem presentes, ignorados e fora dos commits. Nenhum stash,
reset, clean, exclusão ou operação remota foi utilizado.

Na validação de links, a única referência inválida encontrada apontava para um
`implementation-plan.md` declarado como futuro, mas ausente. A referência foi
substituída pelo `test-plan.md` efetivamente versionado, sem mudar o plano ou o
comportamento do harness.
