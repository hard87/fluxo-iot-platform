# Portal Web MVP do Fluxo

Este documento descreve a base do portal web desta sprint.

## Stack e estrutura

- React + Vite + TypeScript
- Arquitetura de frontend:
  - `src/services/api`: chamadas HTTP e tratamento unificado de erro
  - `src/hooks`: estado de sessao e selecao de workspace
  - `src/components`: layout e componentes reutilizaveis
  - `src/pages`: telas do MVP
  - `src/types`: contratos tipados com API
  - `src/utils`: sanitizacao e validacoes

## Telas implementadas

- Login (`/login`)
- Cadastro inicial de usuario (`/register`)
- Criacao/listagem/selecao de workspace (`/workspaces`)
- Dashboard do workspace (`/workspaces/{workspaceId}/dashboard`)
- Listagem de dispositivos (`/workspaces/{workspaceId}/devices`)
- Cadastro de novo dispositivo (`/workspaces/{workspaceId}/devices/new`)
- Detalhes do dispositivo com ultima telemetria e status (`/workspaces/{workspaceId}/devices/{deviceId}`)
- Exibicao de credencial apenas no momento de criacao/rotacao
- Pagina de saude da plataforma (`/status`)

## Seguranca aplicada no frontend

- Token nao e persistido em `localStorage`.
- Segredos de provisionamento sao exibidos apenas em memoria na tela atual.
- Sanitizacao basica de entradas textuais antes de envio.
- Validacoes de formulario (email, senha forte, campos obrigatorios).
- Tratamento padrao de erros da API (`ProblemDetails`) com mensagens claras.
- Sem uso de `dangerouslySetInnerHTML`.

## Variavel de ambiente

Arquivo `portal-web/.env.example`:

```env
VITE_API_BASE_URL=http://localhost:5000
```

## Execucao local (sem Docker)

```powershell
cd portal-web
npm install
npm run dev
```

A aplicacao sobe em `http://localhost:5173`.

## Fluxo funcional esperado

1. Criar conta inicial.
2. Fazer login.
3. Criar ou selecionar workspace.
4. Cadastrar dispositivo.
5. Provisionar dispositivo e copiar credencial mostrada uma unica vez.
6. Acompanhar status/ultima telemetria e rotacionar credencial quando necessario.

## Limitacoes atuais do MVP

- Sessao nao persiste apos refresh (token mantido em memoria para reduzir exposicao local).
- Sem tema multi-idioma.
- Sem fluxo de recuperacao de senha.
