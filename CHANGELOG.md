# CHANGELOG


## 2026-08-19

- Adicionado fluxo operacional multi-family local com bootstrap administrativo, login Identity, selecao de familia e UI tenant-aware.
- Adicionados members e FamilySettings com matriz Owner/Admin/Member e protecao do ultimo Owner.
- Preparados Telegram por referencia de secret e worker relacional isolado por familia.
- Mantido integralmente o runtime legado `ContaStore + JSON` quando a feature flag esta desligada.

## 2026-08-16

- Adicionada foundation controlada de migracao JSON para PostgreSQL, sem endpoint ou execucao automatica.
- Implementados dry-run, relatorio seguro, validacao previa, IDs deterministas por familia, deduplicacao e rollback transacional.
- Adicionados testes de importacao, tres reexecucoes, isolamento familiar, dry-run, invalidos e falha atomica.
- Nenhum dado real foi importado; `ContaStore + JSON` permanece o runtime padrao.

## 2026-08-14

- Adicionados `ContaRepository` e `PagamentoRepository` scoped, com tenant resolvido exclusivamente por `ICurrentFamilyContext`.
- Criados endpoints experimentais tenant-aware para contas e pagamentos sob `/api/multi-family`.
- Aplicada matriz Owner/Admin/Member, com 404 para recursos cross-family e 403 para role insuficiente no tenant atual.
- Mutacoes permanecem protegidas por antiforgery; DTOs nao aceitam `FamilyId` como autoridade.
- Adicionados cenarios PostgreSQL descartaveis para duas familias, troca de tenant e parametros adulterados.
- Runtime de producao permanece `ContaStore + JSON`; nenhuma migration, importacao ou ativacao real foi realizada.

## 2026-08-13

- Preparada Fase 2.1 com Identity individual, `CurrentUserContext` e `CurrentFamilyContext` apenas em modo controlado.
- Adicionados cookie seguro, antiforgery, lockout, rate limiting por IP e selecao familiar em sessao revalidada.
- Runtime JSON permanece padrao; APIs legadas ficam bloqueadas quando o modo experimental multi-family esta ativo.
- Adicionada fundacao local EF Core/PostgreSQL multi-tenant, ainda desconectada do runtime JSON.
- Criados modelo relacional, Identity, migration inicial e testes SQLite descartaveis de integridade familiar.
- Nenhuma migration foi aplicada em producao, nenhum JSON real foi importado e nenhum deploy foi realizado.
- Encerrado o baseline pos-producao da tag `v1.0.4`, commit `e06d30e`.
- Registrado o estado operacional validado no servidor HP Linux.
- Criado checkpoint formal pos-producao.
- Atualizada documentacao de arquitetura, deploy, continuidade e pendencias.
- Documentado o plano tecnico JSON -> PostgreSQL -> autenticacao individual -> Family/Tenant, sem implementacao.

## 2026-07-30

- Adicionado versionamento dos arquivos visuais para evitar CSS antigo em cache no Safari.
- Simplificada a tela do celular ocultando as secoes administrativas de contas e backups.
- Corrigido estouro horizontal do painel em tablets e ampliados os filtros para toque.
- Backups automaticos identicos agora sao ignorados, com copia obrigatoria apos sete dias.
- Persistidas as chaves ASP.NET Data Protection no volume de dados do deploy Docker.
- Evitada a invalidacao desnecessaria das sessoes de login ao recriar o container.
- Atualizados Compose, exemplos de ambiente, seguranca e checklists operacionais.
- Adicionado HSTS em producao e teste automatizado para evitar regressao.

## 2026-07-18

- Corrigida a protecao de acesso para permitir o carregamento anonimo de `login.js` e `login.css` na tela de login.
- Adicionados testes para garantir que somente os recursos da tela de login sejam publicos e que `app.js` continue protegido.

## 2026-07-16

- Adicionada configuracao do horario do lembrete diario pela interface.
- Criado `ReminderSettingsStore` para persistir o horario em `settings.json`, ao lado de `contas.json`.
- Criadas rotas protegidas `/api/settings/reminder` para consultar e atualizar hora/minuto do envio diario.
- Atualizado `DailyReminderService` para ler o horario configurado dinamicamente sem reiniciar a aplicacao.
- Adicionados testes automatizados para defaults, persistencia e validacao do horario do lembrete.
- Adicionados `Dockerfile`, `.dockerignore`, `deploy/docker-compose.hp.yml` e `deploy/agendador-contas.docker.env.example` para deploy Docker seguro no HP Pavilion Ubuntu Server 24.04 LTS.
- Atualizado `docs/deployment-hp-linux.md` para tornar Docker Compose o metodo recomendado no HP, preservando `systemd` como alternativa.
- Configurado compose com container previsivel, `restart: unless-stopped`, volume persistente em `/srv/data/apps/agendador`, rede externa `proxy` e porta `5005` limitada a `127.0.0.1`.
- Criado guia `docs/deployment-hp-linux.md` para deploy em servidor HP Linux x64.
- Atualizados README, checklist final, roadmap, TODO, decisions e notas de IA para registrar o servidor HP como alvo imediato de homologacao.
- Mantido deploy Raspberry Pi como caminho futuro separado, usando `linux-arm64`.

## 2026-07-09

- Adicionado rate limiting ao endpoint `/api/auth/login`.
- Externalizados CSS e JavaScript da tela de login.
- Removido `unsafe-inline` da Content Security Policy.
- Removidos handlers `onclick` inline da tela principal.
- Centralizada acoes da interface principal por delegacao de eventos em `wwwroot/app.js`.
- Adicionados cabeçalhos HTTP básicos de segurança.
- Configurada CSP compatível com a interface atual, ainda permitindo inline enquanto a tela de login nao for externalizada.
- Adicionado indicador de ultima atualizacao no cabecalho da interface.
- Ampliados testes automatizados para protecao de acesso e validacao de senha obrigatoria.
- Criado checklist final em `docs/final-checklist.md`.
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
