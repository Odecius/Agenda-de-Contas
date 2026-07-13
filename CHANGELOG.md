# CHANGELOG

## 2026-07-09

- Adicionado endpoint `/health` para verificacao operacional simples.
- Liberado `/health` sem login por nao retornar dados sensiveis.
- Adicionado backup automatico configuravel por `Backup__*`.
- Adicionada retencao segura que remove apenas backups automaticos antigos.
- Atualizado nome de backup para incluir milissegundos e evitar colisao no mesmo segundo.
- Adicionada exportacao CSV dos vencimentos do mes selecionado.
- O CSV inclui data, conta, pais, moeda, valor, valor formatado, status e observacoes.
- Adicionado resumo por pais e moeda na interface, sem conversao cambial.
- Adicionados filtros de contas por pais e moeda.
- Mantida exibicao de totais separados por moeda para evitar soma indevida entre GBP, EUR e BRL.
- Criado projeto `tests/AgendadorContas.Tests` com test runner automatizado.
- Adicionados testes para defaults de pais/moeda, vencimento, pagamento, backup/restauracao e lembrete por moeda.
- Adicionados backups manuais do arquivo de dados local.
- Criada listagem de backups e restauracao com confirmacao.
- A restauracao cria backup `pre-restore` antes de substituir os dados atuais.
- Adicionada protecao opcional de acesso por cookie.
- Criada tela de login e endpoint de logout.
- Documentadas variaveis `AccessProtection__*` para producao/Raspberry Pi.
- Preparado deploy Raspberry Pi com guia detalhado em `docs/deployment.md`.
- Adicionados modelos `deploy/agendador-contas.service` e `deploy/agendador-contas.env.example`.
- Documentados caminhos sugeridos para app, dados, segredos, logs, atualizacao e backup manual.
- Adicionado suporte inicial a pais e moeda por conta.
- Incluidos enums para `AccountCountry` e `AccountCurrency`.
- Configurada serializacao de enums como texto na API e no arquivo JSON local.
- Atualizada a interface para cadastro, edicao e exibicao de pais/moeda.
- Totais financeiros passaram a ser exibidos agrupados por moeda, sem conversao cambial.
- Atualizado Telegram para mostrar valores na moeda original e totais agrupados por moeda.
- Padronizada a documentação do projeto em português.
- Criados `CHANGELOG.md`, `TODO.md`, `ROADMAP.md`, `DECISIONS.md`, `AI_NOTES.md`, `AI_GUIDELINES.md`, `SECURITY.md` e documentação em `docs/`.
- Consolidado o estado atual registrado no README e em `notas.txt`.
- Registrado risco de segredo Telegram presente em `notas.txt`.

## 2026-07-09 - Evolução funcional registrada nas notas

- Consolidada aplicação .NET 8 para contas e vencimentos.
- Implementadas notificações Telegram com Options Pattern, validação e `HttpClientFactory`.
- Criada rota `/test-telegram` restrita a desenvolvimento.
- Configurado uso de User Secrets.
- Criada interface web responsiva.
- Adicionado rodapé com marca ABC Solutions.
- Enviado commit `28908d0 feat: improve responsive layout` conforme notas locais.

## Histórico anterior

- Criado cadastro/listagem de contas.
- Criado armazenamento local em JSON.
- Criado serviço diário de lembrete.
