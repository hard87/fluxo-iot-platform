# Backup e Restore PostgreSQL

Procedimento minimo para ambiente local e producao controlada. Os scripts usam o container `postgres` do Docker Compose e nao possuem credenciais fixas.

## Backup

Perfil dev:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/postgres-backup.ps1
```

Perfil controlled-prod:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/postgres-backup.ps1 `
  -ComposeFile docker-compose.controlled-prod.yml
```

Por padrao, o arquivo e salvo em `backups/postgres/fluxo-postgres-<timestamp>.sql`.

## Restore

Valide o alvo antes de restaurar. O restore sobrescreve objetos conforme o conteudo do dump SQL.

```powershell
powershell -ExecutionPolicy Bypass -File scripts/postgres-restore.ps1 `
  -InputPath backups/postgres/fluxo-postgres-20260616-120000.sql
```

Para controlled-prod:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/postgres-restore.ps1 `
  -ComposeFile docker-compose.controlled-prod.yml `
  -InputPath backups/postgres/fluxo-postgres-20260616-120000.sql
```

## Variaveis

- `FLUXO_DB_NAME`
- `FLUXO_DB_USER`

O password fica no ambiente do container PostgreSQL criado pelo Compose. Nao grave senhas nos scripts.

## Cuidados

- Datasets reais nao devem ser versionados.
- `backups/`, `dumps/`, `*.dump`, `*.backup`, `*.bak` e `*.sql.gz` ficam ignorados no Git.
- Antes do piloto, execute pelo menos um restore em ambiente descartavel.
- Guarde backups fora da maquina do broker/API quando o piloto sair do laboratorio.
