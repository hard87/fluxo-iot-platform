# Baseline do Fluxo Portal antes da evolução visual

- **Data:** 2026-08-08
- **Branch:** `fix/portal-same-origin-auth`
- **Commit funcional utilizado como baseline:** `dc08a8b`
- **Status:** validado

## Escopo

Este baseline registra o estado funcional do portal e do backend após a
preservação do material preexistente. Nenhuma alteração visual, funcional, de
API, telemetria, MQTT, banco ou EdgeWarden foi realizada.

O commit `dc08a8b` é o último commit que contém artefatos funcionais
preexistentes normalizados. O commit documental posterior apenas registra os
resultados desta higienização.

## Ambiente de validação

| Item | Versão/estado observado |
|---|---|
| .NET SDK | 10.0.202 |
| Node.js | 24.14.1 |
| npm | 11.11.0 |
| Docker | 29.4.0 |
| Docker Compose | 5.1.2 |
| API | container `fluxo-api`, running/healthy |
| Worker | container `fluxo-worker-ingestion`, running/healthy |
| Portal | container `fluxo-portal-web`, running/healthy |
| PostgreSQL | container `fluxo-postgres`, running/healthy |
| Mosquitto | container `fluxo-mosquitto`, running; sem healthcheck próprio |

## Como iniciar o portal

### Stack Docker de desenvolvimento

Na raiz do repositório, após criar a `.env` local a partir de `.env.example` e
preencher os valores exigidos:

```powershell
docker compose build
docker compose up -d
```

Endpoints esperados:

- Portal: `http://localhost:8080`
- API: `http://localhost:5000`
- Health: `http://localhost:5000/health`

Durante o ensaio EdgeWarden de 24h, esses comandos **não devem ser repetidos**
contra o stack em execução, pois podem reconstruir ou reiniciar serviços.

### Portal local para desenvolvimento

Com a API disponível em `http://localhost:5000`:

```powershell
cd portal-web
npm ci
npm run dev
```

O Vite encaminha `/api` para `http://localhost:5000`. Para validar o bundle de
produção sem iniciar serviço:

```powershell
npm run build
```

## Validações executadas

| Área | Comando/verificação | Resultado |
|---|---|---|
| Backend/build | `dotnet build Fluxo.slnx --no-restore --configuration Release` | aprovado; 0 avisos e 0 erros |
| Backend/unitários | `dotnet test tests/Fluxo.UnitTests/Fluxo.UnitTests.csproj --no-build --configuration Release` | 71/71 aprovados |
| Frontend/testes | `npm test -- --run` | 5/5 aprovados |
| Frontend/build | `npm run build` | aprovado; 856 módulos transformados |
| Dependências frontend | `npm audit --omit=dev` | 2 vulnerabilidades moderadas; exit code 1 |
| Gateway/harness JS | `node --check` em 7 arquivos | aprovado |
| Gateway Bash | `bash -n` em 6 arquivos | aprovado |
| Node-RED | parse JSON de `flow.json` | aprovado |
| Portal em execução | `GET http://127.0.0.1:8080/` | HTTP 200, `text/html` |
| API em execução | `GET http://127.0.0.1:5000/health` | HTTP 200, `Healthy` |
| Containers | inspeção somente leitura do estado | API, Worker, Portal e PostgreSQL saudáveis |

Não foram executados testes de integração/componente, migrations, publicação
MQTT, simulações de falha, rebuild/restart do stack ou inicialização limpa.
Essas exclusões são intencionais para não alterar o banco, o broker ou o
ensaio de 24h em andamento/preparação.

## Resultado do build do portal

```text
index.html                   0,40 kB (gzip 0,27 kB)
assets/index-BTOI4j4r.css    4,66 kB (gzip 1,71 kB)
assets/index-BOh75fxY.js   575,35 kB (gzip 166,49 kB)
```

## Limitações conhecidas

1. O chunk JavaScript principal permanece acima do warning de 500 kB do Vite;
   não foi aplicado code splitting nesta higienização.
2. Os testes exibem avisos das future flags `v7_startTransition` e
   `v7_relativeSplatPath` do React Router.
3. `npm audit --omit=dev` confirma duas vulnerabilidades moderadas no
   React Router. A correção proposta instala `react-router-dom@7.18.2`, uma
   mudança breaking; não foi aplicada.
4. A sessão do portal permanece em memória e é perdida no refresh.
5. A fonte Google externa continua bloqueada pela CSP, usando fallback local.
6. O baseline não revalida integração, persistência, MQTT, migrations ou
   inicialização do zero, pois esses testes poderiam afetar o ensaio ativo.
7. O branch local não possui upstream e diverge historicamente de
   `origin/main`; qualquer sincronização deve ser planejada separadamente.

## Gate para evolução visual

A evolução visual pode começar somente em uma nova unidade de trabalho e sem
alterar contratos, ingestão ou comportamento operacional. Antes dela, confirmar
novamente:

```powershell
git status --short
```

O resultado esperado é vazio. Alterações visuais devem manter os 71 testes
unitários, os 5 testes do portal e o build do portal verdes.
