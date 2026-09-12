# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

Fluxo is an IoT platform for device registration, provisioning, and telemetry monitoring
(accounts → workspaces → devices → telemetry), built as a technical portfolio piece with
production-grade practices: Clean Architecture on .NET, an MQTT ingestion pipeline with
rejection tracking, applied security (JWT auth, per-device MQTT credentials, rate limiting),
and reproducible local operation via Docker.

Data flow:

```text
Device -> MQTT Broker (Mosquitto, dynamic-security) -> Worker Ingestion -> PostgreSQL
                                                            |
                                                            v
                                                     Telemetry Rejections (auto-retried)

Portal Web (React) -> API (ASP.NET Core) -> PostgreSQL
```

## Commands

### Backend (.NET, solution file is `Fluxo.slnx`)

```powershell
dotnet build Fluxo.slnx
dotnet test Fluxo.slnx
```

Run a single test (xunit `--filter`, works for both test projects):

```powershell
dotnet test Fluxo.slnx --filter "FullyQualifiedName~TelemetryIngestionProcessorTests"
dotnet test Fluxo.slnx --filter "FullyQualifiedName~TelemetryIngestionProcessorTests.SomeSpecificFact"
```

Apply EF Core migrations:

```powershell
dotnet ef database update `
  --project src/Fluxo.Infrastructure/Fluxo.Infrastructure.csproj `
  --startup-project src/Fluxo.Api/Fluxo.Api.csproj
```

### Frontend (`portal-web/`, React + Vite + TypeScript, Vitest)

```powershell
cd portal-web
npm install
npm run dev      # dev server
npm run build    # tsc --noEmit && vite build (type-checks before building)
npm run test     # vitest run
```

Run a single frontend test:

```powershell
npx vitest run src/pages/DashboardPage.test.tsx
```

### Local stack (Docker)

```powershell
Copy-Item .env.example .env
docker compose build
docker compose up -d
```

MQTT auth/ACL per device is provisioned automatically by the API via the Mosquitto
`dynamic-security` plugin — there is no manual `password_file`/ACL step. For the minimal
controlled-production profile, use `docker-compose.controlled-prod.yml` instead (don't expose
port `1883`; use TLS on `8883`).

## Architecture

### Backend layers (`src/`)

Standard Clean Architecture, dependencies point inward; **no MediatR** — use cases are plain
classes under `Fluxo.Application/UseCases/*`, wired via DI and called directly from
controllers/workers.

- `Fluxo.Domain` — entities, enums, domain exceptions. No dependencies on other layers.
- `Fluxo.Application` — use cases, DTOs, options, interfaces implemented by Infrastructure.
- `Fluxo.Infrastructure` — EF Core (`Data/`, `Migrations/`), repositories, MQTT dynamic-security
  client (`Mqtt/`), DI registration (`DependencyInjection/`).
- `Fluxo.Api` — ASP.NET Core host: controllers, JWT auth, rate limiting, CORS, middleware
  (correlation ID, exception handling).
- `Fluxo.Worker.Ingestion` — standalone background service (own `Program.cs`) that subscribes
  to MQTT telemetry topics and writes to Postgres; also hosts `AlertEvaluationWorker` and
  `RejectionReprocessingWorker`.

### Multi-tenancy and MQTT topic shape

Devices publish to `fluxo/tenants/{tenant}/workspaces/{workspace}/devices/{device}/telemetry`.
The worker's topic filter and the dynamic-security ACL provisioned per device both derive from
this pattern — changing it touches provisioning, the worker's `MqttTopicParser`, and the broker
ACL setup together.

### Telemetry ingestion reliability

Ingestion failures (transient or DB-level) are written to a `telemetry_ingestion_rejections`
trail instead of being dropped, and `RejectionReprocessingWorker` retries eligible rejections
automatically on an interval (`RejectionReprocessing__*` env vars / `appsettings.json`
`RejectionReprocessing:ErrorTypes`). Idempotency is enforced by sequence number.

### Alerts

`AlertEvaluationWorker` (in the ingestion worker) evaluates alert rules against incoming
telemetry; see `docs/adr/0002-alert-evaluation-state-and-delivery.md` and the newer
`docs/adr/0005-alertas-canais-historico-isolamento-proposta.md` (proposed extension covering
channels/history/isolation) before changing alert behavior.

### Testing strategy — two tiers of integration tests

`Fluxo.IntegrationTests` mixes two very different setups; know which one a test uses before
touching it:

1. **Default (most tests):** `FluxoWebApplicationFactory` swaps EF Core to an in-memory
   provider and stubs out Data Protection / Windows Event Log (see
   `Infrastructure/TestHostIsolation.cs`) so the suite never touches a developer's machine
   state. Runs with plain `dotnet test`, no external services needed.
2. **Relational tests** (schema/concurrency/migrations-sensitive — e.g.
   `TelemetryIngestionPostgreSqlTests`, `TelemetrySchemaV2ConcurrencyTests`,
   `TelemetryQueryPostgreSqlTests`): use `DisposableTestDatabase` to create/drop a real,
   throwaway Postgres database per test. These **skip themselves** (via `Xunit.SkippableFact`)
   unless `FLUXO_TESTS_POSTGRES_ADMIN_CONNECTION` is set — start the disposable server first:

   ```powershell
   .\scripts\tests\start-test-postgres.ps1
   # exports FLUXO_TESTS_POSTGRES_ADMIN_CONNECTION; run dotnet test in the same shell
   .\scripts\tests\stop-test-postgres.ps1
   ```

   Set `FLUXO_TESTS_REQUIRE_POSTGRES=1` to turn a missing server into a hard failure instead of
   a skip (used for the official/CI baseline run). `DisposableTestDatabase` refuses to run
   against anything that isn't provably a disposable server (localhost only, marker table
   check, generated-name pattern, per-run ownership) — never point
   `FLUXO_TESTS_POSTGRES_ADMIN_CONNECTION` at the pilot/prod database.

   Two Postgres profiles exist for different guarantees: `transactional` (default, tmpfs,
   fsync off — for locking/concurrency tests) and `durability` (fsync on, named volume — for
   crash-recovery tests). Using the wrong one makes durability assertions meaningless.

### Security-relevant conventions

- `FLUXO_AUTH_SIGNING_KEY` must be ≥32 chars in any non-local environment.
- Never commit `.env`, MQTT credentials, private keys, or `docker/mosquitto/data|log/`.
- Controlled-production deploys use `docker-compose.controlled-prod.yml`, not the dev compose
  file. Full checklist: `docs/checklist-producao-controlada.md`.
- Details: `docs/seguranca-owasp.md`, `docs/mqtt-tls-e-credenciais.md`.

## Documentation map

`docs/README.md` is the curated index (product/portal, security, pilot operation, scale
roadmap, ADRs). Check `docs/adr/` before changing telemetry schema, alert evaluation, the
telemetry query API, or gateway store-and-forward behavior — each has an accepted ADR.
