# Contribuindo com o Fluxo

Obrigado por contribuir com o Fluxo.

## Objetivo deste repositorio

Este projeto e um portfolio tecnico com foco em:
- engenharia de software em .NET com Clean Architecture;
- pipeline IoT com MQTT + Worker + PostgreSQL;
- seguranca aplicada ao MVP;
- evolucao para piloto e escala.

## Fluxo de trabalho recomendado

1. Crie uma branch a partir de `main`.
2. Faça alteracoes pequenas e com escopo claro.
3. Rode build e testes localmente.
4. Abra PR com evidencias tecnicas.

Exemplo de nomes de branch:
- `feat/...`
- `fix/...`
- `chore/...`
- `docs/...`

## Padrao de qualidade minimo

Antes da PR:

```powershell
dotnet build Fluxo.slnx
dotnet test Fluxo.slnx
```

Se alterar `portal-web`:

```powershell
cd portal-web
npm install
npm run build
```

## Escopo e estilo

- Prefira PRs pequenas e objetivas.
- Atualize documentacao quando alterar comportamento.
- Nao versione segredos locais (`.env`, credenciais MQTT, chaves).
- Mantenha consistencia com os guias em `docs/`.

## Pull Request

Use o template de PR em `.github/PULL_REQUEST_TEMPLATE.md`.

Campos esperados:
- contexto e objetivo;
- o que mudou;
- como validar;
- riscos e limitacoes;
- checklist tecnico.
