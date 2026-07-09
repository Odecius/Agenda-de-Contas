# CHANGELOG

## 2026-07-09

- Adicionado suporte inicial a pais e moeda por conta.
- Incluidos enums para `AccountCountry` e `AccountCurrency`.
- Configurada serializacao de enums como texto na API e no arquivo JSON local.
- Atualizada a interface para cadastro, edicao e exibicao de pais/moeda.
- Totais financeiros passaram a ser exibidos agrupados por moeda, sem conversao cambial.
- Atualizado Telegram para mostrar valores na moeda original e totais agrupados por moeda.
- Padronizada a documentaÃ§Ã£o do projeto em portuguÃªs.
- Criados `CHANGELOG.md`, `TODO.md`, `ROADMAP.md`, `DECISIONS.md`, `AI_NOTES.md`, `AI_GUIDELINES.md`, `SECURITY.md` e documentaÃ§Ã£o em `docs/`.
- Consolidado o estado atual registrado no README e em `notas.txt`.
- Registrado risco de segredo Telegram presente em `notas.txt`.

## 2026-07-09 - EvoluÃ§Ã£o funcional registrada nas notas

- Consolidada aplicaÃ§Ã£o .NET 8 para contas e vencimentos.
- Implementadas notificaÃ§Ãµes Telegram com Options Pattern, validaÃ§Ã£o e `HttpClientFactory`.
- Criada rota `/test-telegram` restrita a desenvolvimento.
- Configurado uso de User Secrets.
- Criada interface web responsiva.
- Adicionado rodapÃ© com marca ABC Solutions.
- Enviado commit `28908d0 feat: improve responsive layout` conforme notas locais.

## HistÃ³rico anterior

- Criado cadastro/listagem de contas.
- Criado armazenamento local em JSON.
- Criado serviÃ§o diÃ¡rio de lembrete.
