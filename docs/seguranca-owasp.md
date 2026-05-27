# Seguranca e OWASP Top 10 (Fluxo)

Resumo das medidas implementadas nesta fase para backend/API, worker e portal web.

## A01 - Broken Access Control

- Endpoints de portal protegidos por JWT (`[Authorize]`).
- Isolamento por workspace via membership (`workspace_memberships`).
- Acesso a device/provisionamento/telemetria com verificacao de usuario + workspace.
- Quando usuario nao pertence ao workspace, retorno `404` para reduzir enumeracao.

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
- CORS limitado a origens configuradas.
- Ambiente local documentado com riscos explicitos de `1883` sem TLS.

## A06 - Vulnerable and Outdated Components

- Dependencias fixadas em arquivos de projeto.
- Recomendado executar varredura de CVE no pipeline (pendente).

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
- Endurecer headers HTTP (HSTS/CSP/Frame-Options) com politica formal.
- Incluir SAST/DAST e scan de dependencia automatizados no pipeline.
