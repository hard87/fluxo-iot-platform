# Seguranca e OWASP Top 10 (Fluxo)

Resumo das medidas implementadas nesta fase para backend/API, worker e portal web.

## A01 - Broken Access Control

- Endpoints de portal e legados protegidos por JWT (`[Authorize]`), incluindo os controllers legados (`/api/devices`, `/api/provisioning`, `/api/telemetry`).
- Isolamento por workspace via membership (`workspace_memberships`).
- Acesso a device/provisionamento/telemetria com verificacao de usuario + workspace.
- Autorizacao por papel (`WorkspaceMembershipRole`): acoes de escrita (criar/provisionar device, rotacionar credencial, registrar telemetria) exigem no minimo `Admin`; leitura (dashboard, listagem, detalhes, telemetria) aceita qualquer papel (`Viewer` incluso). Retorno `403` quando o papel e insuficiente.
- Quando usuario nao pertence ao workspace, retorno `404` para reduzir enumeracao; quando pertence mas o papel e insuficiente, retorno `403`.

## A02 - Cryptographic Failures

- Senha de usuario com PBKDF2-HMAC-SHA256 + salt unico.
- Credencial de device armazenada apenas como hash + salt.
- Segredo de device mostrado somente na criacao/rotacao.
- Preparacao de TLS local para MQTT (`8883`) com script de certificado self-signed.

## A03 - Injection

- Validacoes de entrada em DTOs e use cases.
- JSON de metadata/payload validado no dominio/ingestao.
- EF Core com queries parametrizadas (sem SQL dinamico no fluxo normal).

## A04 - Insecure Design

- Separacao por camadas (Domain/Application/Infrastructure/API).
- Regras de negocio fora de controllers.
- Fluxo de provisionamento com tenant/workspace/device consistente.

## A05 - Security Misconfiguration

- `.env` real ignorado no Git.
- `passwords`, `acl`, chaves e certificados locais ignorados no Git.
- Estado/runtime do Mosquitto (`data/` e `log/`) ignorado no Git.
- CORS limitado a origens configuradas.
- Ambiente local usa bind em loopback por padrao para portas publicadas.
- Nginx/API adicionam headers basicos: `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy` e CSP conservadora.
- HSTS fica restrito ao ambiente nao-development e depende de HTTPS ativo no proxy/terminador.

## A06 - Vulnerable and Outdated Components

- Dependencias fixadas em arquivos de projeto.
- `react-router-dom` atualizado dentro da linha 6.x.
- `npm audit --omit=dev` deve rodar limpo para dependencias de producao do portal.
- Recomendado executar varredura de CVE no pipeline (pendente para automacao).

## A07 - Identification and Authentication Failures

- Fluxo de registro e login com resposta generica para credencial invalida.
- JWT com issuer/audience/signing key configuraveis.
- Expiracao de token configuravel.

## A08 - Software and Data Integrity Failures

- Migration versionada para modelos de seguranca (users/workspaces).
- Recomendada assinatura/controle de imagem em CI/CD (pendente).

## A09 - Security Logging and Monitoring Failures

- Logs estruturados e middleware de correlacao (`X-Correlation-ID`).
- Middleware de excecao padronizado com `ProblemDetails`.
- Sem log de senha/token/segredo de device.

## A10 - SSRF

- Nao ha fluxo de fetch de URL arbitraria por usuario no backend atual.
- Reavaliar ao introduzir webhooks/conectores externos.

## Riscos e pendencias antes de producao publica

- Substituir chave JWT local por segredo de cofre (vault/KMS).
- Adotar refresh token + revogacao de sessao.
- Habilitar obrigatoriamente TLS fim a fim (API e MQTT).
- Refinar CSP para dominios finais do ambiente controlado.
- Incluir SAST/DAST e scan de dependencia automatizados no pipeline.
