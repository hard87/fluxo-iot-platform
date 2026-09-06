# Alertas — Etapa 1: baseline confiável e isolado

Data: 2026-09-06 (revisado no mesmo dia após revisão do operador)
Status: baseline revisado após validação dos arquivos; correções de isolamento e CI aplicadas.
Nenhum código de alertas foi escrito. As contagens originais são históricas; a revalidação
está no adendo final.

## 1. Estado do repositório

| Item | Valor |
| --- | --- |
| Branch | `fix/portal-same-origin-auth` |
| HEAD | `7ea75de5dd5adaa34680574eadddcfa8259cf0dc` (inalterado) |
| SDK / runtime | .NET SDK 10.0.202, `Microsoft.AspNetCore.App` 10.0.6 |

Mudanças documentais preexistentes, **preservadas intactas**:

- `docs/README.md` (modificado)
- `docs/adr/0005-alertas-canais-historico-isolamento-proposta.md` (não rastreado)
- `docs/product/alertas-especificacao.md` (não rastreado)

Nenhum stash, reset, descarte, commit, push ou deploy. Nada em `src/` foi alterado.

## 2. Correção do diagnóstico — as 13 falhas não reproduzem

O briefing partia de uma execução anterior com **27 aprovados e 13 falhas**, atribuídas a
Data Protection/DPAPI e ao Windows Event Log. **Isso não se reproduziu.**

Baseline capturado antes de qualquer alteração:

```
dotnet build Fluxo.slnx --no-restore --configuration Release   -> êxito, 0 avisos
dotnet test tests/Fluxo.UnitTests        --no-build -c Release -> 71/71 aprovados
dotnet test tests/Fluxo.IntegrationTests --no-build -c Release -> 40/40 aprovados, 0 falhas
```

Evidência de por que não reproduz nesta máquina e nesta conta:

- O key ring DPAPI existe e está íntegro: `%LOCALAPPDATA%\ASP.NET\DataProtection-Keys` contém
  4 chaves (a mais recente de 30/08/2026), criadas pela mesma conta (`PINKY\junior`, sessão
  **não** elevada) que executa os testes.
- O provider de Event Log usa a fonte `.NET Runtime`, que **existe** na máquina.
  `EventLog.SourceExists` só lança `SecurityException` para fontes inexistentes (verificado:
  lança para `Fluxo.Api` e `ASP.NET Core`; retorna `True` para `.NET Runtime` e `Application`).

Hipótese para a execução original, **não confirmada**: outro contexto de execução — outra
conta Windows, perfil de usuário não carregado (serviço/agente de CI), ou antes de o key ring
atual existir.

**A cadeia de dependência existe e foi confirmada estaticamente**, apenas não está falhando
aqui:

1. Não há **nenhuma** configuração de Data Protection, Serilog ou EventLog no repositório
   (0 ocorrências em código e configuração). Tudo vem de defaults do framework.
2. `Program.cs:38` → `AddFluxoSecurity` → `AddAuthentication(...)`, que internamente chama
   `AddDataProtection()`, embora a API use apenas JWT bearer.
3. `AddDataProtection()` registra o `DataProtectionHostedService` — confirmado por inspeção
   binária de `Microsoft.AspNetCore.DataProtection.dll` 10.0.6; o antigo
   `DataProtectionStartupFilter` não existe mais em .NET 10. Ele inicializa o key ring
   avidamente no start do host, e `WebApplicationFactory` inicia hosted services.
4. O key ring default no Windows fica em `%LOCALAPPDATA%\ASP.NET\DataProtection-Keys`, cifrado
   com `DpapiXmlEncryptor`.
5. `WebApplication.CreateBuilder` adiciona o `EventLogLoggerProvider` por padrão no Windows.

A suíte dependia da conta Windows que a executa. O isolamento implementado remove essa
dependência de forma verificável, mas é **blindagem de hermeticidade**, não correção de falha
observada agora.

Descartado como causa: `MqttDynamicSecurity:Enabled` é `false` em `appsettings.json`, logo o
`MqttIngestionWorkerAccessBootstrapper` se auto-suprime nos testes.

## 3. O problema real: 26 testes aprovavam sem executar

Reproduzido de forma decisiva. O baseline de 40/40 acima foi obtido com o **daemon do Docker
parado e nenhum PostgreSQL acessível**. Ainda assim a suíte relatou verde, porque os três
helpers de banco faziam `return` puro quando `FLUXO_TESTS_POSTGRES_ADMIN_CONNECTION` estava
ausente:

```csharp
var admin = Environment.GetEnvironmentVariable("FLUXO_TESTS_POSTGRES_ADMIN_CONNECTION");
if (string.IsNullOrWhiteSpace(admin)) return;   // reportado como "aprovado"
```

Isso também valia para o CI anterior, que não tinha PostgreSQL nem definia a variável.
O workflow foi corrigido nesta revisão (§11).

## 4. Alterações e justificativas

Nada em `src/`. A configuração de segurança fora de `Testing` permanece intacta — não havia o
que enfraquecer, já que a aplicação não configura Data Protection nem logging.

| Arquivo | Mudança | Motivo |
| --- | --- | --- |
| `tests/.../Infrastructure/TestHostIsolation.cs` (novo) | Data Protection em diretório temporário por processo + `NullXmlEncryptor`; remoção cirúrgica do `EventLogLoggerProvider` | Elimina dependência do key ring DPAPI do usuário e do Event Log |
| `tests/.../Infrastructure/DisposableTestDatabase.cs` (novo) | Consolida os 3 helpers duplicados; guardas; skip explícito; escopo por execução | Deduplicação, guardas, fim do falso verde, isolamento entre execuções |
| `tests/.../Api/FluxoWebApplicationFactory.cs` | Aplica o isolamento; env vars em construtor estático; connection string `Port=1` | Ver nota abaixo |
| `tests/.../Api/TestHostIsolationTests.cs` (novo) | 3 testes que asseguram o isolamento | Sem eles, uma regressão só apareceria em outra conta Windows |
| `tests/.../Infrastructure/DisposableServerLeaseTests.cs` (novo) | 3 testes negativos do lease e da propriedade de banco | Provam que teardown é recusado enquanto outra execução usa o servidor |
| `tests/.../Fluxo.IntegrationTests.csproj` | `Xunit.SkippableFact` 1.5.23 | Estado "Ignorado" não existe em xunit v2 puro |
| `tests/.../Ingestion/TelemetryIngestionPostgreSqlTests.cs` | `[SkippableFact]`, helper comum | — |
| `tests/.../Ingestion/TelemetrySchemaV2ConcurrencyTests.cs` | `[SkippableFact]`/`[SkippableTheory]`, helper comum | — |
| `tests/.../Telemetry/TelemetryQueryPostgreSqlTests.cs` | Idem + correção da data-base (§6) | — |
| `docker/test/docker-compose.tests.yml` (novo) | PostgreSQL descartável — perfil **transacional** | — |
| `docker/test/docker-compose.durability.yml` (novo) | PostgreSQL descartável — perfil **durabilidade** | §7 |
| `docker/test/init/00-marker.sql` (novo) | Tabela marcadora do ambiente descartável | Guarda que não pode ser burlada por connection string plausível |
| `scripts/tests/start-test-postgres.ps1` / `stop-test-postgres.ps1` (novos) | Sobe/derruba apenas o projeto compose do perfil selecionado | — |

### Nota: as variáveis de ambiente do processo são load-bearing

Mover `ConnectionStrings__DefaultConnection` e `Authentication__Jwt__SigningKey` para a
configuração in-memory do host **quebrou os 16 testes de host**. Causa: `Program.cs` chama
`AddInfrastructure`/`AddFluxoSecurity` durante `DeferredHostBuilder.Build()`, ou seja **antes**
de o `ConfigureAppConfiguration` da factory ser aplicado; ambos lançam se os valores faltarem.

Restauradas em construtor estático (uma vez por processo) e com valor **inutilizável**:
`Host=localhost;Port=1;Database=fluxo_tests_unused`. O valor anterior era
`Host=localhost;Port=5432;...`, que poderia alcançar o que estivesse escutando em 5432.

## 5. Isolamento — evidência

### Host de teste

Assegurado por `TestHostIsolationTests`, que passam:

- `DataProtection_Should_Not_Use_The_Windows_User_Key_Ring` — o `XmlRepository` resolvido é um
  `FileSystemXmlRepository` no diretório temporário do processo, e não é subdiretório de
  `%LOCALAPPDATA%\ASP.NET\DataProtection-Keys`.
- `DataProtection_Should_Not_Encrypt_Keys_With_Dpapi` — o `XmlEncryptor` é `NullXmlEncryptor`.
- `Logging_Should_Not_Write_To_The_Windows_Event_Log` — nenhum `ILoggerProvider` é
  `EventLogLoggerProvider`.

`UseEphemeralDataProtectionProvider()` foi deliberadamente **evitado**: substitui apenas
`IDataProtectionProvider` e deixaria o `IKeyRingProvider` — que o hosted service resolve —
ainda apontando para o repositório DPAPI.

### Banco descartável — nada compartilhado com o piloto

| | Piloto | Testes (transacional) | Testes (durabilidade) |
| --- | --- | --- | --- |
| Container | `fluxo-postgres` | `fluxo-tests-postgres` | `fluxo-tests-postgres-durability` |
| Projeto compose | `fluxo` | `fluxo-tests` | `fluxo-tests-durability` |
| Porta host | `127.0.0.1:5433` | `127.0.0.1:55432` | `127.0.0.1:55433` |
| Persistência | volume `fluxo_postgres_data` | `tmpfs` (RAM) | volume `fluxo-tests-durability_postgres_data` |
| Rede | `fluxo_default` | `fluxo-tests_default` | `fluxo-tests-durability_default` |
| Banco / usuário | `fluxo_db` / `fluxo` | `fluxo_test_admin` / `fluxo_test` | idem |
| `restart` | `unless-stopped` | `"no"` | `"no"` |

Credenciais dos containers de teste são locais, só de teste, ligadas a loopback. Nenhum
segredo do piloto foi lido ou reutilizado. `.env` não foi tocado.

### Guardas — provados negativamente

Cada um recusou **antes** de qualquer DDL:

| Guarda | Alvo | Resultado |
| --- | --- | --- |
| Host não-local | `Host=db.exemplo-remoto.invalid` | `Refusing to run disposable-database tests against host ... Only localhost, 127.0.0.1, ::1 are allowed.` |
| Banco proibido | `Database=fluxo_db` | `Refusing to use 'fluxo_db' as the admin database for disposable tests.` |
| Sem marcador | servidor de teste com a tabela marcadora removida | `Refusing to create or drop databases on this server: table public.fluxo_disposable_test_marker was not found...` |

Após a recusa por falta de marcador, a contagem de bancos `fluxo\_%` permaneceu **1** (apenas
`fluxo_test_admin`) — nenhum `fluxo_it_*` chegou a ser criado.

### Proteção entre execuções concorrentes

O servidor descartável pode ser compartilhado por mais de uma execução simultânea. São duas
camadas, porque protegem coisas diferentes.

**Camada 1 — propriedade dos bancos (RunId).** Cada processo gera um **RunId** (8 hex)
incluído em todo nome de banco: `fluxo_{it|query|v2}_{runId}_{guid}`, visível em
`pg_database`. `GuardDisposableDatabaseName` recusa nome cujo RunId não seja o desta execução;
`GuardOwnership` recusa derrubar banco que esta execução não criou, e o registro de
propriedade só ocorre **após** o `CREATE DATABASE` ter êxito, de modo que um create falho
nunca autoriza um drop.

**Camada 2 — ciclo de vida do servidor (lease).** RunId escopa bancos, não o servidor: um
`docker compose down` de uma execução ainda destruiria o servidor que outra está usando. Isso
foi fechado com exclusividade verificável:

- Toda execução ativa adquire o advisory lock `hashtext('fluxo:tests:server-lease')` em modo
  **compartilhado**, numa conexão dedicada e **não-pooled** (`Pooling=false`) mantida aberta
  por todo o processo, com `application_name = fluxo-tests-run-{RunId}`.
- Execuções concorrentes coexistem (modo compartilhado).
- `stop-test-postgres.ps1` mantém uma sessão `psql` aberta com a **mesma chave em modo exclusivo**
  até o PostgreSQL parar. Resposta diferente de `t`, timeout ou falha de inspeção recusam o
  teardown. `-Force` é um bypass explícito reservado à recuperação manual.
- O helper mantém um lease por connection string, verifica a sessão antes de reutilizá-la
  e readquire o lock com nova validação do marcador após perda da conexão. Testes que
  reiniciem o servidor precisam chamar `EnsureRunLeaseAsync` antes de retomar operações;
  não há garantia de proteção ininterrupta durante um crash deliberado.
- O lease vive na sessão: se o processo de teste morrer, o PostgreSQL o libera sozinho.
  **Não existe lease órfão** bloqueando teardown para sempre.

Os dois perfis usam projetos compose distintos, então `stop` de um nunca alcança o outro.

#### Testes negativos

Três testes automatizados (`DisposableServerLeaseTests`), mais uma prova no nível do script:

| Prova | Resultado |
| --- | --- |
| `ActiveRun_Prevents_Exclusive_Lease_Acquisition` | com a execução ativa, `pg_try_advisory_lock` — a exata precondição do teardown — retorna `false` |
| `ActiveRun_Is_Attributable_To_Its_RunId` | o detentor aparece em `pg_stat_activity` como `fluxo-tests-run-{RunId}`, para que a recusa seja acionável |
| `Foreign_RunId_Database_Name_Is_Refused` | nome bem formado de outra execução é recusado no drop, com mensagem "belongs to another test run" |
| Script, com outra execução ativa | `Refusing to tear down 'fluxo-tests-postgres': another test run is still using it (fluxo-tests-run-deadbeef).` — container permaneceu `Up (healthy)` |
| Script, após liberar o lease | teardown normal: container, rede e volume removidos |

**Limitação remanescente, honesta:** o lease protege o caminho sancionado
(`stop-test-postgres.ps1`). Um `docker kill`/`docker rm` manual, ou o desligamento do Docker
Desktop, continua podendo destruir o servidor sob uso — nenhum mecanismo dentro do banco pode
impedir isso. Bancos órfãos de uma execução abortada permanecem até o próximo `down`, quando
desaparecem com o container; não há varredura de órfãos, porque limpeza restrita aos recursos
da própria execução era requisito explícito.

### Piloto

O daemon do Docker estava parado no início. O operador iniciou o Docker Desktop e parou os
containers manualmente. Estado verificado antes, durante e depois: **todos os containers do
piloto em `Exited (0)`**, nunca executados por este trabalho. Após o teardown,
`fluxo_postgres_data`, `fluxo_mosquitto_data`, `fluxo_mosquitto_log` e a rede `fluxo_default`
permanecem intactos, e nenhum recurso `fluxo-tests*` restou. Nenhum comando global de limpeza
do Docker foi usado.

## 6. Defeito latente encontrado e corrigido — e o que ele NÃO significa

Com os 26 testes finalmente executando, **10 falharam de verdade** em
`TelemetryQueryPostgreSqlTests`:

```
Npgsql.PostgresException : 23514: no partition of relation "telemetry_points" found for row
```

### Cronologia (corrigida)

`telemetry_points` é particionada por RANGE em `OccurredAtUtc`. A migration
`20260711172401_AddTelemetrySchemaV2` cria, **em banco novo**, partições de mês corrente − 1
até + 6. O teste fixava `Base = 2026-07-12`.

| Momento | Janela em banco novo | `2026-07-12` cabe? |
| --- | --- | --- |
| Escrita do teste (jul/2026) | 2026-06 … 2027-01 | sim — o teste estava correto |
| Ago/2026 | 2026-07 … 2027-02 | sim |
| **A partir de 01/09/2026** | 2026-08 … 2027-03 | **não — passa a falhar** |

Portanto o defeito ficou ativo por **5 dias** até ser detectado (06/09/2026), e não por dois
meses. Os helpers antigos e o workflow comprovam que execuções sem a variável de PostgreSQL
não exerciam cobertura relacional. Não há evidência suficiente para afirmar que nenhuma
execução relacional ocorreu nos dois meses anteriores; essa generalização foi retirada.

O sintoma foi observado em **banco recém-migrado**, exatamente o caso de CI e de testes;
qualquer banco sem a partição correspondente também pode apresentá-lo.

Correção: `Base` passou a ser ancorada no primeiro dia do mês corrente às 12:00 UTC, o que
mantém os pontos semeados dentro da janela indefinidamente e preserva a semântica (o maior
intervalo usado é de ~33 minutos).

### Dados atrasados — duas camadas independentes

A versão anterior deste relatório afirmava que "telemetria mais antiga que ~1 mês é rejeitada
na escrita", atribuindo isso às partições. **Estava errado em duas frentes** e fica retratado:
a conclusão confundia dois mecanismos distintos e ignorava a validação que de fato governa a
ingestão.

#### Camada A — janela de aceitação da aplicação (é ela que vincula)

`TelemetryIngestionProcessor.cs:171` rejeita a mensagem antes de qualquer escrita:

```csharp
if (occurredAtUtc != default &&
    (occurredAtUtc > receivedAtUtc.AddMinutes(5) ||
     occurredAtUtc < receivedAtUtc.AddDays(-_options.MaxPastDays)))
    rejectionReasons.Add("TimestampOutsideWindow.");
```

| Borda | Valor | Origem |
| --- | --- | --- |
| Passado | **`MaxPastDays` = 30 dias** (configurável) | `MqttIngestionOptions.cs:42`, seção `MqttIngestion` |
| Futuro | **5 minutos** (fixo no código, não configurável) | `TelemetryIngestionProcessor.cs:171` |

Características que importam para alertas:

- A borda é medida contra `receivedAtUtc`, não contra o relógio da avaliação.
- É uma **rejeição de domínio modelada**, não um erro de banco: vira
  `TelemetryIngestionRejectionRecord` com tipo `Validation` e motivo `TimestampOutsideWindow`, e entra
  no caminho de `RejectionReprocessing`. É observável e auditável.
- A assimetria passado-configurável / futuro-fixo é intencional? Não há registro. Vale decidir
  explicitamente antes da Etapa 2, já que 5 minutos é a tolerância a relógio dessincronizado
  dos dispositivos.

#### Camada B — disponibilidade de partição (infraestrutural, separada)

`telemetry_points` é particionada por RANGE em `OccurredAtUtc`. A migration cria, em banco
novo, mês corrente − 1 até + 6. `TelemetryPartitionMaintenanceService.MaintainAsync` (laço
`0..6`) cria apenas de mês corrente para frente e **nunca remove nenhuma**, de modo que em
banco de longa vida as partições antigas se acumulam desde a migration.

Se um ponto escapar a essa cobertura, o resultado é um `PostgresException 23514` — **exceção
de infraestrutura, não rejeição modelada**. Foi exatamente isso que os 10 testes acusaram, e
só chegaram lá porque escrevem direto via EF/binary copy, **sem passar pelo processador de
ingestão** e portanto sem a Camada A.

#### Como as duas se relacionam

As janelas não são equivalentes: 30 dias corridos podem ultrapassar o início do mês
anterior. Em banco criado em **01/03/2026 00:00 UTC**, a migration começa as partições em
**01/02/2026**, mas a aplicação admite pontos desde **30/01/2026**. Um ponto nessa lacuna
pode passar na validação e falhar com `23514`, mesmo na ingestão normal.

A cobertura também depende de `MaxPastDays`, da continuidade da manutenção e da retenção.
Aumentar a janela, deixar a manutenção falhar por período prolongado ou remover partições
pode criar outras lacunas. Backfill, testes e reprocessamentos exigem análise própria.

Esta revisão corrige o diagnóstico; não altera migrations nem a política de particionamento
do produto. Alinhar partições à janela de aceitação permanece uma pendência de infraestrutura,
com teste da borda de fevereiro e de `MaxPastDays`, antes de garantir persistência de todo
dado aceito pela validação.

**Implicação para a Etapa 2.** O requisito "dados atrasados permanecem no histórico, mas não
retroagem o estado corrente" opera dentro da janela de **`MaxPastDays` (default 30 dias) para
trás e cinco minutos para frente**, relativa ao recebimento, e depende de partições disponíveis.
Fora da janela de ingestão, o dado não chega ao histórico de pontos: é rejeitado e
registrado como `TimestampOutsideWindow`. A especificação de alertas precisa dizer isso
explicitamente e decidir se a avaliação de alertas trata rejeições `TimestampOutsideWindow`
como evento observável ou como ausência de dado.

## 7. Perfis de banco: transacional vs. durabilidade

A versão anterior oferecia um único perfil com `fsync=off`, `synchronous_commit=off`,
`full_page_writes=off` e dados em `tmpfs`. Isso é adequado para transações, locks e
constraints, mas **invalida qualquer teste de durabilidade** — e a Etapa 2 exige exatamente
"recuperação após crash" e "crash entre persistência e envio". Os perfis foram separados.

| | Transacional (default) | Durabilidade |
| --- | --- | --- |
| Arquivo | `docker-compose.tests.yml` | `docker-compose.durability.yml` |
| `fsync` / `synchronous_commit` / `full_page_writes` | off | **on** |
| `POSTGRES_INITDB_ARGS` | `--nosync` | (padrão, durável) |
| Dados | `tmpfs` (RAM) | volume nomeado do projeto |
| Válido para | transações, locks, `SKIP LOCKED`, constraints, isolamento, concorrência, migrations | recuperação após crash, sobrevivência a reinício, WAL |
| **Inválido para** | **durabilidade** | — (apenas mais lento) |

Uso: `.\scripts\tests\start-test-postgres.ps1 -Profile durability`.

### Evidência empírica de que não são intercambiáveis

Em ambos os perfis foi criada uma tabela e feito commit de uma linha; em seguida, parada suja
(`docker kill`) e novo start:

| Perfil | Antes do crash | Depois do crash |
| --- | --- | --- |
| Transacional | 1 linha | **`ERROR: relation "crash_probe" does not exist`** — cluster reinicializado, dado committed perdido |
| Durabilidade | 1 linha | **1 linha** — sobreviveu |

Um teste de recuperação após crash rodando no perfil transacional passaria ou falharia por
motivos sem relação com o código sob teste. Confirmado também que, após a reinicialização do
`tmpfs`, o script de init roda de novo e a tabela marcadora é recriada, de modo que os guardas
continuam válidos.

## 8. Comandos de reprodução

```powershell
# 1. Baseline sem infraestrutura (testes relacionais devem aparecer como Ignorado)
dotnet build Fluxo.slnx --no-restore --configuration Release
dotnet test tests/Fluxo.UnitTests        --no-build --configuration Release
dotnet test tests/Fluxo.IntegrationTests --no-build --configuration Release

# 2. Subir o PostgreSQL descartável (define a variável nesta shell)
.\scripts\tests\start-test-postgres.ps1                       # transacional
.\scripts\tests\start-test-postgres.ps1 -Profile durability   # durabilidade

# 3. Baseline oficial: infraestrutura obrigatória, "não executado" vira falha
$env:FLUXO_TESTS_REQUIRE_POSTGRES = '1'
dotnet test tests/Fluxo.IntegrationTests --no-build --configuration Release

# 4. Derrubar (remove apenas o projeto compose do perfil)
.\scripts\tests\stop-test-postgres.ps1
.\scripts\tests\stop-test-postgres.ps1 -Profile durability

# 5. Portal
cd portal-web
npm ci
npx tsc --noEmit
npm test -- --run
npm run build
npm audit --omit=dev
```

## 9. Resultados originais por suíte (antes da revisão adicional)

### Antes das alterações

| Suíte | Aprovados | Falhos | Não executados |
| --- | --- | --- | --- |
| Unitários | 71 | 0 | 0 |
| Integração | 40 | 0 | **26 relatados como aprovados** |

### Depois — sem infraestrutura PostgreSQL

| Suíte | Aprovados | Falhos | Ignorados |
| --- | --- | --- | --- |
| Unitários | 71 | 0 | 0 |
| Integração | 17 | 0 | **29 (visíveis)** — 26 relacionais + 3 do lease |

Com `FLUXO_TESTS_REQUIRE_POSTGRES=1` e sem infraestrutura, a suíte **falha** com mensagem
explícita, em vez de pular.

### Depois — com o PostgreSQL descartável (baseline oficial)

| Suíte | Perfil | Aprovados | Falhos | Ignorados | Duração |
| --- | --- | --- | --- | --- | --- |
| Unitários | — | 71 | 0 | 0 | ~2 s |
| Integração | transacional | **46** | 0 | **0** | ~22 s |
| Integração | durabilidade | **46** | 0 | **0** | ~50 s |

Os 46 incluem os 26 relacionais executando de verdade, os 3 testes de isolamento do host e os
3 testes negativos do lease. A diferença de duração entre perfis é consistente com `fsync`
ligado. Build: êxito, 0 avisos. Nenhum banco residual em nenhuma execução.

### Portal

| Comando | Resultado |
| --- | --- |
| `npm ci` | êxito |
| `npx tsc --noEmit` | exit 0 |
| `npm test -- --run` | **8 arquivos, 30 testes aprovados** |
| `npm run build` | exit 0; bundle principal `600,03 kB` (gzip 174,42 kB) |
| `npm audit --omit=dev` | 2 vulnerabilidades moderadas, exit 1 — limitação já documentada |
| lint | **não existe** — sem script `lint` e sem ESLint/Prettier/Biome |

Divergências com a documentação anterior: `frontend-baseline.md` e `project-status.md`
registram **5** testes de portal; são **30**. O bundle cresceu de `575,35 kB` para `600,03 kB`.

## 10. Testes não executados / limitações

- **Nenhum teste ficou por executar** no baseline oficial. O estado "Ignorado" agora aparece
  apenas quando a infraestrutura está ausente, e nunca como aprovação.
- A cadeia DPAPI/Event Log do briefing **não foi reproduzida**. O isolamento é preventivo e
  verificado por testes, mas não há evidência local da falha original.
- Nenhum teste de concorrência ou de recuperação após crash **novo** foi escrito nesta etapa. O
  perfil de durabilidade está pronto e validado como ambiente, mas ainda não há teste de
  produto que o exercite — isso é trabalho da Etapa 2.
- Não há varredura de bancos órfãos de execuções abortadas (§5).
- Nada foi executado contra a stack do piloto.

## 11. Problemas remanescentes

### Retratação: `.env` **não** está versionado

A versão anterior deste relatório afirmava que `.env` estava versionado com a chave JWT e a
senha MQTT do piloto. **A afirmação era incorreta e fica retratada.** Verificado:

| Verificação | Resultado |
| --- | --- |
| `git ls-files --error-unmatch .env` | não rastreado |
| `git log --all -- .env` | vazio — ausente no histórico alcançável consultado |
| `.gitignore` linha 38 | `.env` (e linha 39, `.env.local`) |

O arquivo rastreado é `.env.example`, que traz apenas nomes de chave. Existe um `.env` local,
não rastreado, consumido pelo docker compose — o arranjo prescrito pelo `README.md`.

**Reconciliação de uma declaração inconsistente.** A versão anterior afirmava que "nenhum valor
dele foi lido". Isso contradizia o próprio relatório, que descrevia o conteúdo do arquivo.
O que de fato ocorreu: **o `.env` foi lido durante a exploração inicial** deste trabalho, e foi
dessa leitura que veio a caracterização — depois usada, indevidamente, para afirmar que havia
segredo versionado. Nenhum valor foi usado para configurar containers, testes ou scripts; o
Postgres descartável usa credenciais próprias definidas em `docker/test/`. Nenhum valor é
reproduzido neste relatório, nos scripts, nos logs ou na saída dos testes.

**Escopo da conclusão.** Limita-se ao que foi efetivamente verificado por comando: o arquivo
não está sob controle de versão e não aparece no histórico alcançável (`--all`). Este
relatório **não** afirma nada além disso — em particular, não classifica a sensibilidade do
conteúdo, não afirma que os valores são de produção, e não constitui auditoria de exposição do
arquivo por outros meios (backups, cópias, histórico de shell, artefatos de build).

Origem do erro: confundi "presente na working tree e legível" com "versionado".

Ação recomendada, sem caráter de incidente: manter o `.env` local sob a regra já documentada no
README. Se houver dúvida sobre exposição por outras vias, isso demanda verificação própria, não
coberta aqui.

### Outros

1. **CI corrigido nesta revisão.** O workflow provisiona PostgreSQL 16 descartável, inicializa
   o marcador e define `FLUXO_TESTS_REQUIRE_POSTGRES=1`. Também executa os testes do portal.
   A execução no GitHub depende de commit/push e não foi observada nesta revisão. Antes
   desta correção eram 29 ignorados (26 relacionais + 3 do lease), não 26.
2. **Documentação de baseline atualizada nesta revisão** quanto à contagem de testes do
   portal, integrações e tamanho do bundle. Os resultados originais da §9 são históricos.

## 12. Gates e sequência das próximas etapas

Correção em relação à versão anterior deste relatório: verificação de e-mail e escolha do
transporte **não** são gates da Etapa 2. Elas pertencem à Etapa 3, onde os canais de entrega
são construídos.

### Etapa 2 — motor de avaliação (regras, revisões, ocorrências, coordenação)

Gates antes de começar:

- Aceitação formal do ADR-0005 (hoje ainda `Proposto`), que supersede pontos do ADR-0002.
- Decisão registrada sobre a semântica de dados atrasados à luz de §6, incluindo os casos 1–3
  e a interação com a futura política de retenção de partições.
- Mapeamento do modelo real de autorização (`GetAuthorizedWorkspaceUseCase`,
  `WorkspaceMembership` e seus papéis, `PlatformUser` ativo) para as operações de regra,
  ocorrência e transição.

Habilitadores já prontos:

- `DisposableTestDatabase.WithDatabaseAsync` é o ponto de extensão para testes relacionais.
  Novos testes de alertas devem reutilizar um prefixo existente ou acrescentar um ao padrão em
  `DisposableNamePattern` (que hoje aceita `it`, `query`, `v2`).
- Testes novos devem ser `[SkippableFact]`/`[SkippableTheory]` e chamar
  `DisposableTestDatabase.SkipUnlessAvailable()`, para nunca voltarem a aprovar sem executar.
- Testes de lock, `SKIP LOCKED`, fencing e constraints usam o perfil **transacional**; testes
  de recuperação após crash e "crash entre persistência e envio" **precisam** do perfil
  **durabilidade** (§7).
- Todo teste relacional novo deve passar por `WithDatabaseAsync`, que adquire o lease do
  servidor. Testes que abram conexões próprias fora dele não registram uso e ficam sujeitos a
  teardown por outra execução (§5).

### Etapa 3 — canais e entrega (portal + e-mail)

Gates antes de começar, **não** antes da Etapa 2:

- Investigar se `PlatformUser` possui estado explícito de verificação de e-mail e, se ausente,
  desenhar o fluxo mínimo seguro (token de uso único, expiração, proteção contra abuso, sem
  registrar o token em log).
- Selecionar e configurar o transporte de e-mail, com adaptador atrás de interface e sem
  contratar serviço nem usar credenciais reais sem autorização.

## Gate

Correções do baseline autorizadas e aplicadas. O ADR-0005 continua **Proposto**; esta revisão
não equivale à aceitação arquitetural nem inicia o motor de alertas da Etapa 2. Sem commit,
push ou deploy; sem alterações na stack do piloto.

## 13. Revalidação e correções adicionais — 06/09/2026

Revisão autorizada pelo operador após confrontar o relatório com os arquivos. Corrigidas:

- **Corrida no teardown:** a consulta anterior liberava o lock exclusivo antes do `down`.
  Agora a sessão permanece aberta até `docker stop` concluir; só então ocorre a remoção.
  Erros de Docker, timeout e respostas SQL diferentes de `t` impedem o teardown.
  A seleção do container exige nome exato, sem confundir os dois perfis.
- **Lease após perda de sessão:** `DisposableServerLease` verifica a conexão antes de
  reutilizar, readquire o lock e valida o marcador na nova sessão. Leases são separados
  por connection string; removido o cache permanente de presença do marcador.
- **Dois testes de regressão:** encerram apenas a sessão criada pelo próprio teste e
  comprovam readquisição; o segundo remove o marcador de um banco próprio e comprova recusa.
- **Inicialização:** foi reproduzida uma falha do healthcheck por socket durante a troca do
  servidor temporário do initdb pelo definitivo. Healthchecks dos perfis e CI usam TCP local,
  evitando considerar o servidor temporário pronto. Inicialização limpa revalidada.
- **CI:** PostgreSQL 16, marcador, infraestrutura obrigatória e testes do portal adicionados.
- **Documentação:** corrigidas a generalização sobre 30 dias/partições, a classificação da
  rejeição (`Validation` com motivo `TimestampOutsideWindow`), as contagens e os gates de e-mail.
  ADR-0005 permanece proposto. A política de partições do produto não foi alterada.

| Verificação executada nesta revisão | Resultado |
| --- | --- |
| Build Release | 0 erros, 0 avisos |
| Unitários | 71 aprovados |
| Integração transacional | 48 aprovados, 0 falhas, 0 ignorados; ~25 s |
| Integração durabilidade | 48 aprovados, 0 falhas, 0 ignorados; ~1 min 44 s |
| Integração sem variável de banco | 17 aprovados, 31 ignorados |
| Teste filtrado com banco obrigatório e variável ausente | 1 falha esperada, confirmando recusa |
| Teardown durante suíte ativa | Recusado; container permaneceu running/healthy |
| Teardown com conexão SQL indisponível | Recusado; container permaneceu running; nome do banco restaurado |
| Durabilidade: commit, kill/start | Linha persistida sobreviveu |
| Transacional: commit, kill/start | Tabela de prova desapareceu; marcador foi recriado |
| Limpeza final | Ambos os projetos de teste removidos; nenhum recurso `fluxo-tests*` restante |
| Portal | 8 arquivos, 30 testes aprovados; TypeScript e build aprovados |
| Bundle principal | 600,03 kB; gzip 174,42 kB; warning de tamanho permanece |
| Audit de dependências de produção | 2 vulnerabilidades moderadas, 0 altas/críticas; pendência preexistente |

Os testes de perda de sessão simulam a perda do backend mantendo o processo de teste;
o ensaio kill/start valida o ambiente, não a recuperação do futuro motor de alertas.
Operações após crash deliberado devem readquirir o lease antes de continuar. Interrupções
externas da sessão/servidor não têm proteção ininterrupta garantida.

O workflow foi alterado e suas suítes verificadas localmente; não houve execução remota
do GitHub Actions, commit, push ou deploy. A lacuna entre partições e janela de ingestão
permanece explicitamente registrada na §6 para correção própria do produto.
Os containers do piloto permaneceram parados; volumes `fluxo_postgres_data`,
`fluxo_mosquitto_data`, `fluxo_mosquitto_log` e rede `fluxo_default` permanecem presentes.

## 14. Serialização externa de start/stop — 06/09/2026

Baseline funcional aprovado pelo operador. Esta revisão final altera somente os scripts
de ciclo de vida, acrescenta sua regressão determinística e registra as evidências pertinentes.
As contagens e os resultados funcionais da §13 não foram refeitos nesta revisão.

`start-test-postgres.ps1` e `stop-test-postgres.ps1` adquirem um mutex nomeado do sistema
operacional, externo ao PostgreSQL, antes da primeira inspeção. A posse abrange toda a
operação, incluindo a janela entre `docker stop` e `docker compose down`, e é liberada
em `finally`, inclusive em falhas. O helper é `test-postgres-lifecycle-lock.ps1`.

O nome `Global\Fluxo.TestPostgres.Lifecycle.<projeto-compose>` é estável entre checkouts e
sessões Windows. Os perfis usam nomes distintos. Se já houver uma operação em andamento,
a segunda é recusada imediatamente, antes de inspecionar ou alterar recursos, com
`Lifecycle operation already in progress`. Erros de acesso ao mutex também impedem a operação;
um mutex abandonado por processo encerrado pode ser adquirido pela próxima execução.

O advisory lock continua protegendo as suítes ativas: o mutex serializa os scripts,
enquanto o lease compartilhado no PostgreSQL impede o stop de um servidor em uso.
Nenhuma chamada automatizada desta validação usou `-Force`. Comandos Docker externos aos
scripts continuam fora dessa coordenação; o mutex é local ao sistema operacional.

### Regressão determinística reproduzível

```powershell
.\scripts\tests\test-postgres-lifecycle.ps1
.\scripts\tests\test-postgres-lifecycle.ps1 -Profile durability
```

O teste exige ausência do container selecionado. Executa Docker real e intercepta apenas
o retorno de `docker stop`: antes de deixar o script seguir para `compose down`, inicia
outro processo PowerShell executando o start real. A asserção exige recusa pelo mutex externo
e verifica a sequência exata, sem usar sleeps para tentar acertar a corrida:

`docker-stop-completed → concurrent-start-refused → compose-down`

| Prova | Transacional | Durabilidade |
| --- | --- | --- |
| Stop com lease compartilhado de suíte ativa | Recusado; container continuou executando | Recusado; container continuou executando |
| Start concorrente entre stop e down | Recusado pelo mutex externo | Recusado pelo mutex externo |
| Ordem exata dos três eventos | Confirmada | Confirmada |
| Novo start após término do teardown | Sucesso; healthcheck e marcador válidos | Sucesso; healthcheck e marcador válidos |
| Teardown final sem bypass | Sucesso | Sucesso |

Ambas as execuções terminaram com exit code 0. O parser PowerShell aceitou os quatro scripts.
Recursos descartáveis removidos ao final; piloto permaneceu parado. Sem commit, push ou deploy.

**Etapa 1 encerrada após a correção solicitada. Aguardando aprovação explícita para a Etapa 2.**
Nenhum código de alertas foi implementado e o ADR-0005 permanece Proposto.

## 15. Aprovação do operador e registro da Etapa 1 — 06/09/2026

O operador aprovou explicitamente o encerramento da Etapa 1 e autorizou iniciar a Etapa 2,
limitada ao núcleo backend. Autorizou também um commit específico da Etapa 1 para registrar
o baseline e iniciar a implementação com a working tree limpa. Essa exceção não autoriza
commit das alterações da Etapa 2, push ou deploy. As declarações anteriores de ausência de
commit descrevem o estado histórico antes desta autorização.

A Etapa 2 consolidará as decisões aplicáveis do ADR-0005 preservando sua proposta original,
manterá rejeições fora da fila de avaliação e registrará a pendência de partições sem alterar
essa política. E-mail, portal e stack do piloto permanecem fora do escopo. O resultado da
Etapa 2 será submetido à aprovação antes da Etapa 3.
