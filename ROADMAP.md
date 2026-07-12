# ROADMAP

## Sprint 10 - Experiência de cadastro e edição

- Melhorar validações visuais.
- Exibir mensagens claras.
- Confirmar exclusões.
- Melhorar edição.
- Adicionar filtros simples.

## Sprint 11 - Histórico e pagamentos

- Refinar histórico de pagamentos.
- Melhorar visualização de meses pagos e pendentes.
- Criar testes das regras de pagamento.

## Sprint 12 - Resumo mensal

- Criar painel de resumo mensal.
- Exibir total previsto, pago e pendente.
- Destacar contas vencendo hoje.

## Sprint 13 - Pais e moeda

- Adicionar pais e moeda por conta.
- Exibir valores com GBP, EUR ou BRL.
- Agrupar totais por moeda sem conversao cambial.
- Preparar caminho para conversao futura.

## Sprint 14 - Deploy Raspberry Pi

- Preparar modelo `systemd`.
- Preparar exemplo de variaveis de ambiente de producao.
- Documentar instalacao, atualizacao, logs e backup manual.
- Deixar validacao real pendente ate o Raspberry estar disponivel.

## Sprint 15 - Proteção de acesso

- Adicionar autenticação simples.
- Revisar autorização das rotas.
- Avaliar HTTPS/reverse proxy se exposto fora da rede local.

## Sprint 16 - Backup e recuperação

- Criar backup manual dos dados locais.
- Listar backups disponíveis.
- Restaurar backup com confirmação.
- Criar backup `pre-restore` antes de substituir dados atuais.

## Sprint 17 - Testes automatizados

- Criar testes para vencimentos, pagamentos, Telegram, autenticação e backups.
