# Deploy Local Seguro (Fluxo)

Guia para subir o ambiente completo local com foco em seguranca para laboratorio.

## 1. Pre-requisitos

- Docker + Docker Compose
- .NET SDK 10
- Node.js 20+ (se for rodar portal fora do Docker)
- OpenSSL (opcional, para gerar cert TLS MQTT)

## 2. Preparar variaveis de ambiente

```powershell
Copy-Item .env.example .env
```

Edite o `.env` com valores locais (principalmente `FLUXO_AUTH_SIGNING_KEY`).
Observacao: a API nao inicializa com a chave JWT default de placeholder.

## 3. Gerar certificados TLS locais do MQTT (recomendado)

```powershell
powershell -ExecutionPolicy Bypass -File docker/mosquitto/scripts/generate-local-certs.ps1
```

## 4. Gerar certificado HTTPS local da API (opcional)

```powershell
powershell -ExecutionPolicy Bypass -File scripts/generate-local-api-cert.ps1
```

## 5. Gerar arquivos de autenticacao do Mosquitto

1. Copie o template:

```powershell
Copy-Item docker/mosquitto/credentials.template.json docker/mosquitto/credentials.local.json
```

2. Preencha com `credentialUsername`, `provisioningSecret` e `mqttPublishTopic`.

3. Gere `passwords` e `acl`:

```powershell
powershell -ExecutionPolicy Bypass -File docker/mosquitto/scripts/generate-auth-files.ps1 -Overwrite
```

## 6. Build e subida do ambiente

```powershell
docker compose build
docker compose up -d
```

Servicos principais:
- PostgreSQL
- Mosquitto
- API
- Worker de ingestao
- Frontend do portal

## 7. Aplicar migration local

Com API/DB disponiveis, execute:

```powershell
dotnet ef database update `
  --project src/Fluxo.Infrastructure/Fluxo.Infrastructure.csproj `
  --startup-project src/Fluxo.Api/Fluxo.Api.csproj
```

## 8. Comandos uteis

- Logs agregados:

```powershell
docker compose logs -f
```

- Logs de um servico:

```powershell
docker compose logs -f api
docker compose logs -f worker
```

- Parar ambiente:

```powershell
docker compose down
```

- Reset completo (containers + volumes):

```powershell
docker compose down -v
```

## 9. Endpoints e UIs

- Portal web: `http://localhost:8080`
- API: `http://localhost:5000`
- Health API: `http://localhost:5000/health`
- Status JSON: `http://localhost:5000/api/status`
- MQTT sem TLS (dev): `localhost:1883`
- MQTT TLS local: `localhost:8883`

## 10. Diferenca por ambiente

- Local (laboratorio): pode manter `1883` habilitado para debug interno.
- Piloto controlado: preferir `8883` TLS e rotacao frequente de credenciais.
- Producao publica: desabilitar `1883`, usar certificados validos, segredo externo (vault), observabilidade completa.

## 11. Nunca versionar

- `.env`
- `docker/mosquitto/passwords`
- `docker/mosquitto/acl`
- `docker/mosquitto/credentials.local.json`
- `docker/mosquitto/certs/*.key`
