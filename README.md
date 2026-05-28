# Fluxo

Plataforma IoT para cadastro, provisionamento e monitoramento de dispositivos conectados.

Este repositorio foi estruturado como portfolio tecnico, com foco em:
- arquitetura limpa para backend .NET;
- pipeline de ingestao MQTT com rastreabilidade;
- seguranca aplicada ao MVP;
- operacao local reprodutivel via Docker.

## Estado atual

- MVP funcional com API, Worker e Portal Web.
- Suporte a autenticacao de usuario no portal e isolamento por workspace.
- Provisionamento de device com credencial MQTT e topico dedicado.
- Ingestao de telemetria com trilha de rejeicao e idempotencia por sequence.
- Guia de piloto controlado e roadmap para escala.

## Arquitetura em alto nivel

- Backend: ASP.NET Core + Clean Architecture
- Banco: PostgreSQL
- Broker: Mosquitto (MQTT)
- Ingestao: Worker .NET consumindo MQTT
- Frontend: React + Vite + TypeScript

Fluxo de dados (resumo):

```text
Device -> MQTT Broker (Mosquitto) -> Worker Ingestion -> PostgreSQL
                                        |
                                        v
                                   Telemetry Rejections

Portal Web -> API -> PostgreSQL
```

Camadas do backend:

```text
src/
  Fluxo.Api            # Transporte HTTP, middleware, configuracao
  Fluxo.Application    # Use cases, regras de aplicacao, DTOs
  Fluxo.Domain         # Entidades e regras de dominio
  Fluxo.Infrastructure # EF Core, repositorios, migrations
  Fluxo.Worker.Ingestion
```

## Como navegar neste portfolio

Se voce tem 5 minutos:
1. Leia este README.
2. Abra o [indice de documentacao](docs/README.md).
3. Veja [Piloto real controlado](docs/piloto-real-controlado.md) e [Roadmap 1000 devices](docs/roadmap-production-1000-devices.md).

Se voce tem 15 minutos:
1. Suba o ambiente local.
2. Rode os testes.
3. Execute o fluxo de provisionamento + ingestao MQTT.

## Inicio rapido (local)

1. Copie variaveis:

```powershell
Copy-Item .env.example .env
```

2. (Opcional, recomendado) Gere certificados TLS locais para MQTT:

```powershell
powershell -ExecutionPolicy Bypass -File docker/mosquitto/scripts/generate-local-certs.ps1
```

3. Prepare credenciais do Mosquitto (ACL/password):

```powershell
Copy-Item docker/mosquitto/credentials.template.json docker/mosquitto/credentials.local.json
powershell -ExecutionPolicy Bypass -File docker/mosquitto/scripts/generate-auth-files.ps1 -Overwrite
```

4. Suba stack Docker:

```powershell
docker compose build
docker compose up -d
```

5. Aplique migration:

```powershell
dotnet ef database update `
  --project src/Fluxo.Infrastructure/Fluxo.Infrastructure.csproj `
  --startup-project src/Fluxo.Api/Fluxo.Api.csproj
```

Referencias detalhadas:
- [Deploy local seguro](docs/deploy-local-seguro.md)
- [Piloto real controlado](docs/piloto-real-controlado.md)

## Endpoints locais

- API: `http://localhost:5000`
- Health API: `http://localhost:5000/health`
- Status API: `http://localhost:5000/api/status`
- Portal web: `http://localhost:8080`
- MQTT dev interno: `localhost:1883`
- MQTT TLS local: `localhost:8883`

## Qualidade e testes

```powershell
dotnet build Fluxo.slnx
dotnet test Fluxo.slnx
```

Frontend (local sem Docker):

```powershell
cd portal-web
npm install
npm run dev
```

## Documentacao

Veja o indice central em [docs/README.md](docs/README.md).

## Seguranca

- Nao versione `.env`, credenciais MQTT, chaves privadas ou segredos locais.
- Configure `FLUXO_AUTH_SIGNING_KEY` com valor forte (>= 32 chars) antes de subir a API.
- Em producao publica, desabilite `1883` e use TLS obrigatorio com certificados validos.
- Referencia tecnica: [Seguranca OWASP](docs/seguranca-owasp.md).

## Roadmap

- [Roadmap tecnico para producao com 1000 dispositivos](docs/roadmap-production-1000-devices.md)

## Contribuicao

Fluxo de contribuicao e padrao de PR em [CONTRIBUTING.md](CONTRIBUTING.md).
