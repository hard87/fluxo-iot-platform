# Alertas — especificação consolidada

Data: 2026-09-05. Status: proposta para revisão, sem implementação.

## Objetivo e autoridade

Permitir configurar condições sobre telemetria, acompanhar ocorrências no portal e receber e-mail com isolamento por workspace. Portal e e-mail são canais nativos disponíveis no código, mas exigem seleção e configuração na plataforma; nenhuma regra ou remessa externa é ativada automaticamente.

Esta proposta consolida a direção acordada com o responsável pelo produto. As escolhas técnicas novas estão no [ADR-0005 proposto](../adr/0005-alertas-canais-historico-isolamento-proposta.md). Até sua aceitação, o [ADR-0002](../adr/0002-alert-evaluation-state-and-delivery.md) permanece aceito; divergências estão explicitadas no complemento. Não declara a Fase 3 implementada nem o sistema validado em produção.

Referências: [estado atual](../project-status.md), [escopo do MVP](mvp-scope.md), [Schema V2](../adr/0001-telemetry-schema-v2.md), [Query API](../adr/0003-telemetry-query-api.md).

## 1. Escopo

| Capacidade | Primeira entrega |
|---|---|
| Numéricas | Maior, maior ou igual, menor, menor ou igual, dentro e fora de faixa |
| Booleanas | Verdadeiro ou falso |
| Texto | Consultável no Explorer; regras textuais adiadas |
| Modelos | Limite superior, limite inferior, faixa e estado booleano; usuário informa os parâmetros |
| Aplicação | Um dispositivo ou todos os dispositivos compatíveis do workspace, com estado separado por dispositivo |
| Canais | Notificação no portal e e-mail, ambos selecionáveis |
| Histórico | Ocorrências e transições sempre persistidas, independentemente do canal |
| Ausência de dados | Fase 4, worker periódico separado, conforme ADR-0002 |
| Sensor travado, variação e anomalias | Fase 4; nenhuma detecção implícita nesta entrega |
| Webhooks, SMS, mensageria e ML/LLM | Fora da primeira entrega |

Os modelos não sugerem limites de segurança universais. Métricas precisam estar habilitadas para alertas, ter tipo compatível e pertencer ao mesmo workspace. Regras abrangentes passam a incluir dispositivos compatíveis futuros; a interface informa esse comportamento explicitamente.

## 2. Configuração e experiência

1. Selecionar workspace e criar regra inicialmente desabilitada.
2. Selecionar dispositivo ou abrangência, métrica, operador e limites.
3. Definir duração mínima, histerese numérica, severidade e cooldown.
4. Selecionar pelo menos um canal e seus destinatários autorizados.
5. Revisar resumo em linguagem natural, condições de resolução e abrangência.
6. Ativar após validação no servidor. Disponibilizar teste de canal explicitamente identificado como teste, sem criar ocorrência real.

Proposta de defaults: severidade Warning; duração e cooldown zero; histerese zero. Limites não possuem default. Valores temporais negativos, números não finitos, faixas invertidas e histerese incompatível são rejeitados. Critical, Warning e Info são classificações, não mudanças automáticas de comportamento. Quotas de regras, destinatários e testes devem ser configuradas por ambiente antes do piloto.

Regra ativa precisa de ao menos um destinatário elegível em cada canal selecionado. O e-mail só pode ser habilitado com transporte operacional configurado pelo administrador e endereço verificado. Falha posterior do transporte não desativa a avaliação: a interface mostra a falha de entrega.

O usuário consulta ocorrências, filtros por dispositivo/severidade/período/estado, transições, regra aplicada e situação de entrega. Leitura da notificação é individual. Reconhecimento registra pessoa e horário, sem resolver a condição; resolução automática depende da telemetria. Não há resolução manual simulando retorno ao normal.

## 3. Semântica de avaliação

- Preservar fila transacional por ingestão aceita, isolamento por dispositivo e política de dados atrasados do ADR-0002.
- A ingestão limita o passado por `MqttIngestion:MaxPastDays` (default 30 dias) e o futuro a cinco minutos, relativos ao recebimento. Fora da janela registra rejeição de tipo `Validation`, motivo `TimestampOutsideWindow`; não há ponto aceito nem trabalho de avaliação. Partições devem cobrir a janela: mês anterior não garante 30 dias em todas as datas. O tratamento operacional dessas rejeições e a retenção precisam de decisão registrada antes da Etapa 2.
- Duração é confirmada por amostras: não pressupõe que a condição permaneceu verdadeira entre leituras indefinidamente. Quebra de continuidade segue o gap definido no ADR-0002.
- Comparações de faixa incluem as bordas. Fora de faixa significa estritamente abaixo do mínimo ou acima do máximo.
- Para limite superior, resolver ao atingir limite menos histerese; para inferior, limite mais histerese. Para fora de faixa, resolver dentro da faixa reduzida pela histerese; para dentro de faixa, resolver fora da faixa expandida. Rejeitar faixa reduzida vazia. Sem histerese, resolver pela negação do predicado de disparo.
- Condição booleana resolve quando seu oposto é observado. Não usa histerese numérica.
- Cooldown limita a abertura de uma nova ocorrência após o último disparo; não suprime resolução nem entregas já criadas. Dentro dele, a avaliação continua; nova abertura exige amostra confirmadora após o prazo, sem tarefa temporal implícita.
- Uma ocorrência possui Firing, Resolved ou Closed. Closed significa encerramento administrativo por desativação/edição/remoção do escopo, com motivo; nunca significa recuperação observada.
- Alterar parâmetros cria versão imutável, encerra ocorrências anteriores como Closed e reinicia a avaliação sem replay histórico. Mudança apenas de nome não precisa reiniciar o estado; versão e auditoria ainda são preservadas.
- Ativação não reprocessa dados anteriores. Na primeira entrega, avaliar apenas leituras ocorridas após a ativação da versão. Dados anteriores continuam históricos e têm motivo de não aplicação registrado.
- Sem notificações periódicas repetidas, escalonamento, janelas de manutenção ou expressões arbitrárias nesta entrega.

## 4. Canais e isolamento

Administradores autorizados do workspace gerenciam regras e destinatários; membros com permissão de leitura consultam ocorrências; reconhecimento exige permissão específica. Mapear essas capacidades ao modelo real de permissões antes de implementar, sem inventar autorização a partir de identificadores enviados pelo navegador.

Destinatários iniciais são membros ativos do mesmo workspace. E-mail requer verificação de endereço; grupos externos e endereços livres ficam fora do escopo. A verificação de e-mail é uma dependência a confirmar no código, não uma capacidade presumida.

Revalidar acesso ao consultar, criar, editar, selecionar destinatários, criar entregas e imediatamente antes de cada envio ou retry. Destinatário removido, endereço alterado/não verificado ou assinatura desativada cancela a entrega pendente com motivo. Alterar endereço não redireciona uma entrega antiga automaticamente. Um envio já aceito pelo provedor não pode ser recolhido; documentar essa fronteira.

Enviar mensagens individuais. Assunto genérico, corpo mínimo com tipo/severidade/horário e link autenticado; não incluir payload bruto, valores, nomes livres de dispositivos ou outros destinatários por padrão. O link não concede acesso e toda abertura repete a autorização.

Segredos do transporte ficam no servidor, referenciados por configuração protegida. Usuários do workspace não fornecem host SMTP ou credenciais nesta fase. Logs, erros, filas administrativas e caches respeitam escopo; nenhum token, corpo sensível ou credencial entra em logs.

## 5. Entrega e operação

Disparo, resolução e encerramento administrativo são transições notificáveis conforme seleção explícita; sugestão inicial no formulário: disparo e resolução. Reconhecimento não gera e-mail nesta entrega.

Persistir transição e intenções de entrega atomicamente. Portal oferece deduplicação por transição/destinatário. E-mail é entrega com possível repetição após falha ambígua, nunca exactly-once. Aceitação pelo transporte não comprova leitura ou chegada à caixa postal.

Proposta operacional: cinco tentativas, backoff exponencial com jitter e prazo máximo de 24 horas. Falhas permanentes terminam sem retry; falhas temporárias respeitam próxima tentativa. Após limite ou expiração, registrar DeadLetter/Expired. Reenvio manual exige autorização e auditoria, preserva origem e revalida destinatário. Timeouts e quotas do transporte serão fechados no adaptador escolhido.

Histórico informa tentativas e falhas sem expor detalhes internos do provedor. Observar atraso da fila, idade do item mais antigo, falhas por regra, entregas canceladas e dead letters, sem cardinalidade ilimitada em métricas.

## 6. Dados para análises futuras

Preservar referências de tenant/workspace, dispositivo e métrica; versão da regra e snapshot de parâmetros; unidade e tipo no momento da avaliação; referência à ingestão; horários de ocorrência, recebimento, avaliação e transição; valor que fundamenta a transição; motivo e versão do avaliador.

Manter transições imutáveis e sequenciadas, separadas do estado corrente. Reconhecimento e feedback opcional (útil, falso positivo, inconclusivo, comentário) registram autor, data e revisões. Feedback humano não equivale a rótulo verificado para treinamento.

Não duplicar cada ponto de telemetria em uma tabela analítica. Evidências de transição podem sobreviver à retenção da telemetria por snapshot mínimo; referências expiradas devem ser identificadas como indisponíveis. Não prometer replay completo após expurgo.

Proposta para o piloto, sujeita a capacidade e necessidade: 90 dias para ocorrências/transições/feedback, 30 dias para detalhes de entrega, sete dias para trabalho concluído; falhas não resolvidas não são apagadas silenciosamente. Política de telemetria continua independente. Exclusão de workspace precisa abranger filas, caches, históricos e futuros conjuntos derivados, com cancelamento de envios.

ML futuro usa conjuntos versionados, segregados e com separação temporal para evitar uso de informação do futuro no treinamento. Não compartilhar dados entre clientes implicitamente. LLM permanece fora de ingestão/avaliação/entrega, recebe apenas resumos autorizados e minimizados, com habilitação explícita, quota e rastreamento de versão/custo. Nenhuma chamada ou infraestrutura de ML/LLM será implementada nesta fase.

## 7. Critérios de aceite e baseline

Antes do código: registrar branch/HEAD/working tree, rodar verificações existentes em ambiente de teste isolado, registrar falhas preexistentes e dependências ausentes, sem usar a stack do piloto. Esta revisão documental não executou testes e não atesta saúde do runtime.

| Cenário obrigatório | Resultado esperado |
|---|---|
| IDs de outro workspace em cada operação | Rejeição sem expor existência/conteúdo |
| Destinatário removido durante retry | Cancelamento antes de novo envio |
| Tipo/unidade/limites inválidos | Regra rejeitada antes de ativação |
| Duração, gap, bordas, histerese, cooldown | Transições determinísticas com relógio controlado |
| Duplicata, restart e lease expirado | Sem transição/intenção lógica duplicada |
| Workers concorrentes para o mesmo dispositivo | Estado consistente e ordem determinística |
| Dado atrasado ou pré-ativação | Histórico preservado, sem retroação do estado |
| Regra individual falha | Outras avançam; falha tem retry e destino final visíveis |
| Falha entre commit e envio | Evento preservado; retry auditado |
| Provedor indisponível | Ingestão/avaliação/portal continuam |
| Edição/desativação | Closed com motivo; sem falsa resolução |
| ML/LLM desabilitado | Todos os fluxos desta fase funcionam |
| Migração e retenção | Rollback avaliado, integridade e isolamento preservados |

## 8. Sequência de implementação proposta

1. Aceitar o complemento arquitetural, verificar baseline e permissões e decidir a semântica temporal. Verificação de e-mail e transporte são gates dos canais correspondentes, não do motor isolado.
2. Persistência/versionamento, avaliação e testes de concorrência/isolamento.
3. Configuração de regras, histórico e notificações no portal.
4. Adaptador de e-mail, verificação de destinatário, retries e observabilidade.
5. Ensaio de ponta a ponta, documentação operacional e demonstração.

Escolha de provedor de e-mail, limites operacionais e mapeamento de permissões são gates anteriores à implementação dos componentes correspondentes. Ausência de dados e webhooks requerem fases próprias.
