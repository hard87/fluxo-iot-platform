# Arquivamento de regras de alerta

Implementação na branch `codex/arquivar-alertas-persistente`.

## Comportamento

Em **Alertas → Em uso**, a ação **Arquivar** pede confirmação. A regra deixa de avaliar novas medições e sai da lista corrente. Eventos `Firing` são encerrados como `Closed`, com motivo `RuleArchived`; isso é encerramento administrativo, sem afirmar recuperação do dispositivo.

**Arquivadas** permite consultar a configuração final, a data e o autor do arquivamento. **Ver revisões** consulta o histórico paginado. Eventos, reconhecimentos, notificações existentes e intenções de entrega permanecem disponíveis. A resolução dos nomes na lista de eventos inclui regras arquivadas.

Arquivamento é definitivo no contrato atual. Edição e ativação de regras arquivadas retornam conflito; para voltar a usar uma condição, crie outra regra. Regras apenas desativadas continuam editáveis e reativáveis.

## API e persistência

- `POST /api/workspaces/{workspaceId}/alerts/rules/{ruleId}/archive`, corpo `{ "expectedVersion": 2 }`: retorna a nova revisão desativada, com `archivedAtUtc` e `authorId` do usuário que arquivou.
- `GET .../rules?page=1&status=current|archived|all`: padrão `current`; paginação fixa de 100. Status inválido retorna 400.
- `GET .../rules/{ruleId}/revisions`: mantém todas as revisões, inclusive a revisão de arquivamento.
- Owner/Admin podem arquivar; Viewer pode consultar. Usuário ativo e autorização no workspace continuam obrigatórios.
- Versão divergente antes do arquivamento retorna 409. Repetir uma ação já concluída retorna a mesma revisão, sem alterar data/autor ou produzir novas transições.

A nova revisão imutável mantém a condição original e registra `ArchivedAtUtc`. `AuthorId` é o autor dessa revisão e, portanto, do arquivamento. A constraint `CK_alert_revision_archive` impede revisões arquivadas habilitadas.

O arquivamento adquire o mesmo lock exclusivo do catálogo utilizado pela edição. Encerramento de eventos, invalidação dos leases e das tentativas Pending/Claimed/Failed, limpeza do estado derivado, conclusão dos work items elegíveis e troca da revisão corrente ocorrem na mesma transação. Ingestão e avaliação usam o lado compartilhado desse lock. Trabalho já capturado perde seu lease; ingestões futuras não enfileiram avaliações para a revisão desativada. Telemetria e evidências históricas não são removidas.

Destinatários existentes são preservados. Como no comportamento de edição/desativação, transições administrativas `Closed` geram intenção de entrega, mas o adaptador de portal atual só notifica `Firing`/`Resolved`.

## Migration e atualização local

Migration: `AddAlertRuleArchiving`, coluna nullable em `alert_rule_revisions` e constraint de estado. Regras existentes recebem NULL e conservam o comportamento.

Aplique a migration antes de iniciar a API/worker compilados com o novo modelo:

```powershell
dotnet ef database update --project src/Fluxo.Infrastructure --startup-project src/Fluxo.Api
docker compose up -d --build --no-deps api worker frontend
```

Use a conexão local configurada conforme o guia de deploy. O rollback da migration elimina o marcador de arquivamento; as revisões permanecem desativadas, mas passam a ser tratadas como regras desativadas pelo código anterior. Para preservar a distinção de arquivamento, mantenha a migration aplicada ao reverter apenas os binários.

## Verificação

- Suíte .NET com PostgreSQL descartável real: 97 testes unitários e 87 de integração aprovados, zero ignorados.
- Suíte do portal: 157 testes aprovados; build aprovado.
- Integração específica: histórico preservado, eventos encerrados, trabalho capturado invalidado, ausência de novas avaliações, concorrência/idempotência, conflito de versão, acesso Viewer/cross-workspace, arquivamento após desativar device e migration down/up.
- QA do navegador: `scripts/qa/validate-alert-archiving.cjs`, preview local do portal com API simulada. Persistência é validada separadamente pelos testes PostgreSQL. Resultados em `artifacts/alert-archive-qa.json`.
