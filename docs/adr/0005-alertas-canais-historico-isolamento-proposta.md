# ADR-0005 — Complemento ao ADR-0002: canais, histórico e isolamento

Data: 2026-09-05. Status: Proposto. Complementa o ADR-0002; não o substitui integralmente.

## Contexto

O responsável pelo produto definiu portal e e-mail como canais nativos configuráveis. O ADR-0002 descreve entrega específica por webhook e contém pontos que precisam ser fechados antes da implementação. A [especificação consolidada](../product/alertas-especificacao.md) define o comportamento proposto.

Esta é uma revisão documental, não auditoria de código ou validação do ambiente. A implementação do motor não foi encontrada na busca inicial em src; isso não substitui o baseline obrigatório.

## Decisões preservadas

Preservar Schema V2, avaliação independente de entrega, trabalho transacional por ingestão aceita, estado por regra/dispositivo, duração/gap/histerese, exclusão de igualdade numérica exata e política de monotonicidade temporal. NoData permanece na Fase 4 com worker próprio. ML/LLM não entra no caminho operacional. Segurança de webhooks continua requisito quando esse canal for implementado.

## Alterações propostas e precedência após aceitação

| ADR-0002 | Complemento |
|---|---|
| WebhookDelivery como entrega inicial | NotificationDelivery por canal; portal/e-mail primeiro, webhook adiado |
| AlertEvent mutável Firing/Resolved | Preservar ocorrência e acrescentar transições imutáveis e Closed administrativo |
| Regra sem versão explícita | Revisões imutáveis e snapshot da regra na ocorrência |
| Claim de work item apenas | Coordenação adicional por dispositivo, progresso por regra e fencing de lease |
| Falha individual apenas logada | Unidade de avaliação rastreada, com retry/dead letter por regra |
| >= em timestamp sem desempate | Timestamp com sequence/identificador estável para ordenar empates |

Nenhum ADR aceito é editado retroativamente para aparentar que essas decisões já estavam aprovadas. Após aceitação, atualizar referências de fase e o ADR-0002 com ligação explícita a este complemento.

## Modelo lógico adicional

Nomes são conceituais; migrations devem seguir convenções reais do repositório.

- AlertRuleRevision: regra, versão, escopo, parâmetros tipados, unidade/tipo capturados, ativação e autor; conteúdo imutável.
- AlertEvaluationAttempt: work item, regra/revisão, estado Pending/Claimed/Completed/Failed/DeadLetter/Skipped, tentativas, próxima tentativa, lease/token e motivo; unicidade por work item/regra/revisão.
- AlertEvent: ocorrência, workspace, device, regra/revisão e estado atual; no máximo uma ocorrência Firing por regra/device.
- AlertEventTransition: TransitionId, EventId, ordinal, tipo, horários, evidência mínima, versão do avaliador e motivo. Único por evento/ordinal; imutável.
- AlertAcknowledgement e AlertFeedback: autor autorizado, horário e histórico de revisões, sem sobrescrever a evidência original.
- NotificationSubscription: workspace, regra, membro, canal, tipos de transição e habilitação.
- NotificationDelivery: TransitionId, assinatura/destinatário, canal, estado, DeliveryId estável, tentativas, próximo envio, prazo, lease e referência protegida ao destino. Unicidade por transição/canal/assinatura.
- PortalNotification: transição e membro, data de leitura individual; unicidade por transição/membro.

Todas as referências devem garantir pertencimento consistente ao workspace no servidor e no banco por chaves compostas quando aplicável. Tenant derivado do workspace autorizado; nunca confiar em TenantId ou WorkspaceId fornecido no corpo como prova de autorização. Caches e buscas incluem escopo. Detalhar constraints na revisão das migrations.

## Trabalho, falhas e concorrência

1. Manter criação atômica de AlertEvaluationWorkItem com ingestão aceita, sem trabalho para duplicatas/rejeições.
2. Materializar de forma idempotente as unidades por regra aplicável; versão escolhida é fixada nessa unidade. Serializar ativação/edição e avaliação por um mecanismo transacional consistente. Unidade de versão desativada termina Skipped, com motivo, sem aplicar configuração nova a trabalho antigo.
3. Serializar processamento por (WorkspaceId, DeviceId), em ordem estável de chegada, usando lock transacional de uma linha dedicada por dispositivo. SKIP LOCKED continua útil para distribuir dispositivos/trabalho, mas sozinho não garante ordem de estado. Proibir aquisição circular de locks.
4. Para uma mesma regra/device, uma unidade em retry bloqueia avanço das seguintes dessa regra até sucesso ou DeadLetter; regras independentes podem seguir. DeadLetter rompe continuidade de duração e gera diagnóstico operacional explícito.
5. Cada unidade confirma estado, ocorrência, transição, intenções de entrega e progresso na mesma transação. Work item pai só é concluído quando todas as unidades têm resultado terminal; distinguir conclusão com falhas.
6. Claim inclui Pending elegível, Failed com NextAttemptAtUtc vencido e Claimed com lease vencido, excluindo itens terminais e expirados. Backoff deve realmente participar do predicado de claim, corrigindo a divergência entre texto e exemplo SQL do ADR-0002.
7. Reclaim muda token de lease. Commit exige token atual; worker antigo não pode confirmar estado após perder posse. Renovação e timeout precisam impedir execução externa sem limite.
8. Aplicar monotonicidade usando (OccurredAtUtc, Sequence, IngestionRecordId) para desempate estável. Dados anteriores ao estado não o alteram; reprocessamento da mesma unidade não cria outra transição.

Trade-off: serialização por dispositivo reduz paralelismo dentro dele, mas simplifica correção. Benchmark deve avaliar custo da fila e fan-out por regra; não presumir capacidade para mil dispositivos. Evitar manter transação de banco aberta durante chamadas externas.

## Canais e autorização

Transição e intenções são atômicas; workers externos não participam da avaliação. Portal pode materializar notificações pela mesma outbox com deduplicação. E-mail usa adaptador de transporte configurado pelo operador; nenhuma escolha de fornecedor está tomada neste ADR.

Validar assinatura, associação ao workspace e endereço verificado antes de cada tentativa. Guardar identidade do destinatário e versão do endereço; mudança cancela entrega antiga. Remoção de acesso cancela pendências. A fronteira inevitável é um envio já submetido ao transporte, que não pode ser revogado. Não prometer ausência absoluta de corrida entre revogação e serviço externo; minimizar a janela e registrar a decisão de autorização.

E-mail tem semântica at-least-once em falhas ambíguas; DeliveryId e chave de idempotência do provedor, se houver, reduzem repetição sem prometer exactly-once. Portal deduplica por transição/destinatário. Reenvio manual gera nova tentativa lógica auditada vinculada à mesma transição.

Cinco tentativas, backoff com jitter e prazo de 24 horas são defaults propostos. Falha permanente não é repetida. Tentativas do SDK e da fila precisam de orçamento único para evitar multiplicação de retries. Estado Accepted significa aceito pelo transporte; não DeliveredToInbox ou Read. Conteúdo e controles seguem a especificação.

## Histórico e futura análise

TransitionId é a chave de deduplicação de mudança: EventId sozinho não distingue disparo e resolução da mesma ocorrência. Futuro webhook deve publicar ambos, com versão de payload nova explicitamente documentada.

Snapshots mínimos preservam parâmetros, unidades, evidência e versão do avaliador sem copiar o stream. Guardar tempos de ocorrência e conhecimento permite análise temporal sem confundir chegada tardia com observação disponível naquele instante. Feedback separado pode apoiar conjuntos futuros, com proveniência e autorização próprias.

Não criar data lake, feature store, embeddings ou integração LLM agora. Exportações futuras exigem contrato versionado, política de retenção/exclusão e autorização por workspace. Os períodos propostos na especificação precisam ser confirmados frente ao volume do piloto antes de ativar expurgo.

## Lacunas preexistentes registradas

- Exemplo de claim do ADR-0002 omite Failed/NextAttemptAtUtc apesar de prever retries.
- Claim exclusivo de item não serializa dois itens que alteram o mesmo estado.
- Log de erro por regra não define recuperação nem comprova avaliação completa.
- Índice de ocorrência Firing não deduplica sozinho transições, resoluções e entregas.

São achados de desenho; não são incidentes de produção comprovados.

## Consequências e gates

Custos: novas entidades, fan-out de avaliações, serialização por dispositivo e suporte operacional de e-mail. Benefícios: falhas recuperáveis, isolamento verificável, histórico explicável e extensão futura de canais sem acoplar análise ao transporte.

Antes da Etapa 2 (motor): aceitar este ADR, mapear o modelo real de acesso, registrar a semântica de dados atrasados e estabelecer baseline isolado. Antes da Etapa 3 (canais): verificar o estado de verificação de e-mail e definir seu fluxo, além de selecionar/configurar o transporte. Verificação de e-mail não bloqueia o desenvolvimento isolado do motor. Antes de liberar: cumprir a matriz de aceite da especificação, testar migração/rollback, falha e concorrência e validar quotas/retenção. Não executar ensaios na stack ativa do piloto.

## Referências

- [ADR-0002](0002-alert-evaluation-state-and-delivery.md)
- [ADR-0001](0001-telemetry-schema-v2.md)
- [Especificação consolidada](../product/alertas-especificacao.md)
- [Escopo do MVP](../product/mvp-scope.md)
- [Estado do projeto](../project-status.md)
