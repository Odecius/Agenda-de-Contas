# TODO

## Alta prioridade

- Remover token/chat id de `notas.txt` e limpar histórico Git se o segredo já tiver sido versionado.
- Validar deploy real em Raspberry Pi quando o hardware estiver disponivel.
- Validar login em Raspberry Pi real antes de expor o sistema em rede.

## Média prioridade

- Avaliar conversao cambial futura com API externa.
- Melhorar relatorios por moeda e pais.
- Expandir testes automatizados para endpoints completos com servidor em memoria.

## Baixa prioridade

- Avaliar banco leve, como SQLite, se o JSON deixar de ser suficiente.
- Criar painel mensal com métricas.
- Avaliar novos canais de notificação.

## Concluído

- Cadastro e listagem de contas.
- Marcação/desmarcação de pagamentos.
- Pausar, reativar, editar e excluir contas.
- Lembretes via Telegram.
- Rota de teste em desenvolvimento.
- Interface responsiva.
- Melhorias de UX no cadastro, edicao, exclusao, filtros e feedback visual.
- Painel de resumo mensal.
- Suporte inicial a pais e moeda por conta, sem conversao cambial.
- Dashboard por pais e moeda, sem conversao cambial.
- Exportacao CSV mensal de vencimentos.
- Backup automatico configuravel e retencao segura de backups automaticos.
- Endpoint `/health` para verificacao operacional.
- Checklist final em `docs/final-checklist.md`.
- Testes automatizados basicos para protecao de acesso.
- Preparacao de deploy Raspberry Pi com systemd, ambiente, logs e checklist.
- Protecao opcional de acesso por cookie.
- Backup manual e restauração com backup `pre-restore`.
- Documentação padronizada em 2026-07-09.
