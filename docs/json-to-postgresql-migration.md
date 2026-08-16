# Migracao controlada JSON para PostgreSQL

## Escopo

Esta fundacao le uma copia controlada de `contas.json` e prepara/importa contas e pagamentos para uma `Family` explicitamente informada. Nao possui endpoint HTTP, nao e chamada no startup e nao altera o runtime legado. O servico so e registrado quando `MultiFamily:Enabled=true`, ainda limitado a Development/Testing.

## Compatibilidade auditada

| JSON legado | PostgreSQL | Tratamento |
| --- | --- | --- |
| `Conta.Id` global no arquivo | PK global e alternate key `(FamilyId, Id)` | GUID deterministico namespaced pela familia |
| campos da conta | mesmos campos em `ContaEntity` | preservados apos validacao dos limites relacionais |
| `Pagamento.ContaId/Ano/Mes/PagoEm` | entidade com `Id`, `FamilyId` e `PagoEmUtc` | conta remapeada; timestamp normalizado para UTC |
| pagamentos repetidos | indice unico por familia/conta/ano/mes | primeira ocorrencia importada; demais geram warning |
| lembretes enviados | exige canal e semantica familiar | fora do escopo |
| `settings.json` | `FamilySettings` | arquivo separado; fora do escopo |
| Telegram | `TelegramSettings` | secrets nao sao importados |

O JSON nunca fornece `FamilyId`. A familia alvo deve existir e e recebida como argumento confiavel da operacao controlada.

## Idempotencia

Nao foi necessario alterar o schema. O ID da conta deriva por SHA-256 de namespace fixo, tipo, `targetFamilyId` e ID legado. O ID do pagamento acrescenta conta legada, ano e mes. Isso permite tres ou mais execucoes sem duplicacao, importa o mesmo JSON para familias diferentes e nunca usa nome/valor como identidade.

## Fluxo, validacao e transacao

`ImportAsync(sourcePath, targetFamilyId, options, cancellationToken)` le, desserializa, confirma a familia, valida todo o conjunto, deduplica pagamentos, calcula o plano e consulta existentes somente no tenant alvo. Dry-run devolve o plano com `DatabaseModified=false`. A execucao efetiva grava contas e pagamentos em uma unica transacao.

Nome vazio/maior que 80, valor nao positivo, vencimento fora de 1-31, duracao negativa, enum desconhecido, observacao maior que 300, ID vazio/duplicado, pagamento orfao, ano ou mes invalido abortam antes da escrita. Duplicidade mensal e warning recuperavel. Falhas de IO, JSON ou banco abortam e fazem rollback. O relatorio usa apenas nome do arquivo, IDs tecnicos, contagens e categorias; nunca inclui caminho completo, connection string ou secret.

## Rollback futuro

Runbook ainda nao executado: definir janela/RPO/RTO; criar backup imutavel do JSON; criar backup PostgreSQL; executar dry-run em copia; reconciliar contagens; importar; validar isolamento e totais; efetuar cutover aprovado sem remover JSON; observar workers/Telegram; e, se criterios falharem, desligar multi-family e retornar ao JSON preservado.

Nao existem comandos automaticos de exclusao, backup, cutover ou rollback nesta implementacao.

## Limitacoes

- lembretes, settings, Telegram, usuarios e memberships nao sao importados;
- ainda nao existe CLI/admin command, apenas servico interno e harness de teste;
- importadores simultaneos podem fazer uma execucao abortar com seguranca pelas constraints;
- antes de dados reais sao obrigatorios revisao, copia anonimizada, restore ensaiado e reconciliacao operacional;
- os dois achados LOW da Fase 2.2 permanecem fora do escopo.
