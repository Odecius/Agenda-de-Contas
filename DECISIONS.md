# DECISIONS

## 2026-08-14 - Repositories resolvem o tenant server-side

**Decisao:** `ContaRepository` e `PagamentoRepository` recebem apenas IDs de recursos; o `FamilyId` vem exclusivamente de `ICurrentFamilyContext`. DTOs de escrita nao possuem campos de tenant e propriedades JSON extras sao ignoradas sem se tornarem autoridade.

**Motivo:** Impedir leitura ou escrita cross-family mesmo quando body, query, headers ou IDs validos de outro tenant forem adulterados.

**Impacto:** Recurso inexistente ou de outra familia retorna 404. Role insuficiente sobre recurso do tenant atual retorna 403. Delete fisico de conta ou pagamento fica restrito a Owner; Admin cria e edita contas e registra pagamentos; Member somente consulta e registra pagamentos.

## 2026-08-13 - Contexto familiar selecionado em sessao e revalidado

**Decisao:** Usar sessao protegida para guardar a familia escolhida, revalidando usuario, membership e familia no PostgreSQL a cada resolucao do `CurrentFamilyContext`.

**Motivo:** O cliente nao deve ser autoridade sobre `UserId` ou `FamilyId`; a sessao e a opcao mais simples para o incremento controlado atual.

**Impacto:** Uma familia valida pode ser selecionada automaticamente. Multiplas familias exigem escolha autorizada. Sessao distribuida fica pendente antes de multiplas replicas.

## 2026-08-13 - Identity permanece atras de feature flag

**Decisao:** Ativar Identity apenas com `MultiFamily:Enabled=true` em Development/Testing e bloquear APIs JSON legadas nesse modo.

**Motivo:** Evitar qualquer mudanca na producao e impedir acesso nao isolado ao JSON compartilhado durante os testes multi-family.

**Impacto:** AccessProtection e `ContaStore` continuam exatamente como runtime padrao com a flag desligada.

## 2026-08-13 - Manter modelo JSON separado durante a transicao

**Decisao:** Criar entidades relacionais em `Data/Entities` sem adicionar `FamilyId` aos modelos JSON ativos nesta etapa.

**Motivo:** Evitar alterar contratos e arquivos reais antes de existir importador e `CurrentFamilyContext`.

**Impacto:** `ContaStore` continua sendo o runtime. O schema PostgreSQL nasce multi-tenant e preservara os UUIDs na importacao.

## 2026-08-13 - FamilyId em pagamentos

**Decisao:** Persistir `FamilyId` em pagamentos e usar FK composta `(FamilyId, ContaId)` para `Conta`.

**Motivo:** Impedir associacao cross-family no banco.

**Impacto:** Importador e repositorios futuros devem preencher o tenant a partir do contexto server-side.

## 2026-08-13 - Baseline pos-producao

**Decisao:** Registrar a tag `v1.0.4`, commit `e06d30e`, como baseline documental pos-producao do Agendador.

**Motivo:** A aplicacao esta funcional em um servidor Linux via Docker Compose, superando o baseline de pre-producao de 2026-07-23.

**Impacto:** Este repositorio passa a ser a fonte de verdade para novos planejamentos. Snapshots simplificados existentes em outros diretorios nao devem ser usados. O proximo ciclo deve preservar a imagem e os dados atuais antes de qualquer evolucao.

## 2026-08-13 - PostgreSQL deve nascer preparado para Family/Tenant

**Decisao:** Planejar o primeiro schema PostgreSQL com `Family`, usuarios, associacoes e isolamento familiar, sem criar uma etapa relacional mono-tenant intermediaria.

**Motivo:** O objetivo aprovado para planejamento e atender a familia atual e duas familias piloto. Um schema mono-tenant exigiria uma segunda migracao estrutural e aumentaria o risco sobre os dados.

**Impacto:** A importacao do JSON criara a familia existente como primeiro tenant. PostgreSQL, autenticacao e multi-tenancy continuam apenas planejados ate aprovacao explicita de implementacao.

## 2026-08-13 - Baseline pos-producao

**Decisao:** Registrar a tag `v1.0.4`, commit `e06d30e`, como baseline documental pos-producao do Agendador.

**Motivo:** A aplicacao esta funcional em um servidor Linux via Docker Compose, superando o baseline de pre-producao de 2026-07-23.

**Impacto:** Este repositorio passa a ser a fonte de verdade para novos planejamentos. Snapshots simplificados existentes em outros diretorios nao devem ser usados. O proximo ciclo deve preservar a imagem e os dados atuais antes de qualquer evolucao.

## 2026-08-13 - PostgreSQL deve nascer preparado para Family/Tenant

**Decisao:** Planejar o primeiro schema PostgreSQL com `Family`, usuarios, associacoes e isolamento familiar, sem criar uma etapa relacional mono-tenant intermediaria.

**Motivo:** O objetivo aprovado para planejamento e atender a familia atual e duas familias piloto. Um schema mono-tenant exigiria uma segunda migracao estrutural e aumentaria o risco sobre os dados.

**Impacto:** A importacao do JSON criara a familia existente como primeiro tenant. PostgreSQL, autenticacao e multi-tenancy continuam apenas planejados ate aprovacao explicita de implementacao.

## 2026-07-15 - Branch principal remota

**Decisao:** Manter temporariamente `master` porque o repositorio remoto ainda publica `origin/master`.

**Motivo:** O Standard recomenda `main`, mas a troca exige coordenar a branch remota e a branch padrao no GitHub. Renomear apenas localmente quebraria o fluxo atual.

## 2026-07-16 - Servidor Linux como homologacao

**Decisao:** Preparar o deploy imediato para um servidor Linux x64 usando o mesmo modelo de `systemd`, configuracao externa e armazenamento persistente.

**Motivo:** O Raspberry Pi ainda nao esta disponivel, mas o servidor Linux permite validar operacao 24/7, login, Telegram, backups, logs e reinicio automatico antes da migracao futura.

**Impacto:** O publish do servidor usa runtime `linux-x64`. O publish Raspberry permanece documentado como `linux-arm64`.

## 2026-07-16 - Docker Compose recomendado no servidor Linux

**Decisao:** Usar Docker Compose como metodo recomendado para o servidor Linux, mantendo `systemd` como alternativa documentada.

**Motivo:** O ambiente ja possui Docker, Docker Compose e integracao com reverse proxy. Docker reduz diferencas de ambiente, evita instalar runtime .NET diretamente no host e facilita rebuild/rollback.

**Impacto:** Codigo, configuracao e dados persistentes ficam separados. Configuracao sensivel permanece externa ao Git.

## 2026-07-16 - Horario do lembrete configuravel pela interface

**Decisao:** Persistir o horario do lembrete diario em `settings.json`, na mesma pasta persistente de `contas.json`, e expor rotas protegidas para leitura e atualizacao pela interface.

**Motivo:** O usuario precisa alterar o horario sem editar arquivos de configuracao, reiniciar a aplicacao ou tocar em segredos. Manter esse dado no volume persistente tambem preserva o comportamento no Docker.

**Impacto:** `Reminder:Hour`, `Reminder:Minute` e `Reminder:TimeZoneId` continuam sendo defaults iniciais. Depois que o horario e salvo pela interface, o Hosted Service passa a ler a configuracao persistida.

## 2026-08-19 - Fluxo operacional permanece atras da feature flag

**Descricao:** Bootstrap, Identity, UI, members, settings, Telegram familiar e worker relacional existem somente no bloco `MultiFamily:Enabled=true`, ainda restrito a Development/Testing.

**Motivo:** Permitir validacao ponta a ponta sem alterar o runtime publicado.

**Impacto:** `MultiFamily=false` nao registra DbContext nem worker relacional e continua usando JSON.

## 2026-08-19 - Administracao de memberships restrita ao Owner

**Descricao:** Admin pode listar members, mas apenas Owner adiciona, altera ou desativa memberships. Novas roles sao Admin/Member e o ultimo Owner e protegido.

**Motivo:** Reduzir risco de escalacao de privilegio na primeira versao operacional.

**Impacto:** Convites, criacao de novos Owners e administracao delegada ficam para fase futura.

## 2026-08-19 - Worker usa FamilyId explicito

**Descricao:** Jobs de background nao simulam request nem usam `ICurrentFamilyContext`; processam cada familia ativa por ID com queries explicitamente filtradas.

**Motivo:** Background nao possui usuario autenticado e precisa isolar falhas por tenant.

**Impacto:** Falha de uma familia nao impede as seguintes e envio so e registrado apos sucesso.

## 2026-08-16 - Idempotencia sem alterar o schema

**Descricao:** Contas e pagamentos importados recebem GUIDs deterministas derivados de `FamilyId` e chaves legadas. Duplicados mensais conservam a primeira ocorrencia e geram warning.

**Motivo:** O schema atual ja expressa unicidade e isolamento. O namespace familiar permite importar o mesmo arquivo para tenants distintos e repetir sem duplicar.

**Alternativas consideradas:** Preservar GUID legado, adicionar `LegacyId`, tabela de tracking e nome/valor.

**Impacto:** Nenhuma migration nova. Invalidos abortam antes da escrita e falhas de persistencia fazem rollback completo.

## 2026-08-16 - Importador interno sem endpoint

**Descricao:** A migracao e servico interno apenas no modo experimental multi-family, sem startup hook.

**Motivo:** E uma operacao administrativa excepcional e nao deve ampliar a superficie HTTP nem acontecer acidentalmente.

**Impacto:** A interface operacional devera ser aprovada separadamente antes do cutover.

## 2026-07-09 - Usar .NET 8 com Minimal API

**Descrição:** O backend usa ASP.NET Core Minimal API em `Program.cs`.

**Motivo:** O escopo é pequeno e as rotas são diretas.

**Alternativas consideradas:** MVC, Razor Pages, SPA com backend separado.

**Impacto:** Manter rotas simples e extrair serviços quando a regra crescer.

## 2026-07-09 - Armazenar dados localmente em JSON

**Descrição:** `ContaStore` mantém os dados em arquivo JSON local.

**Motivo:** O projeto ainda é de uso pessoal/local e não exige banco externo.

**Alternativas consideradas:** SQLite, PostgreSQL, LiteDB.

**Impacto:** Simplicidade maior, mas exige atenção a backup, concorrência e migração futura.

## 2026-07-09 - Enviar notificações via Telegram

**Descrição:** `TelegramNotificationService` implementa `INotificationService`.

**Motivo:** Telegram é simples para alertas pessoais e funciona bem em automação.

**Alternativas consideradas:** Email, WhatsApp, push notification.

**Impacto:** Segredos devem ficar fora do código e a interface permite novos canais no futuro.

## 2026-07-09 - Usar User Secrets no desenvolvimento

**Descrição:** Tokens e chat id devem ser configurados por User Secrets em desenvolvimento.

**Motivo:** Evitar segredos no Git.

**Alternativas consideradas:** Gravar no `appsettings.json` ou `.env`.

**Impacto:** Desenvolvedores precisam configurar segredos localmente.

## 2026-07-09 - Restringir `/test-telegram` a Development

**Descrição:** A rota de teste existe somente em ambiente de desenvolvimento.

**Motivo:** Evitar endpoint operacional exposto em produção.

**Alternativas consideradas:** Remover rota ou proteger por autenticação.

**Impacto:** Testes de produção devem usar logs e fluxo real.

## 2026-07-09 - Preparar suporte a pais e moeda por conta

**Descricao:** Cada conta passa a ter `Country` e `Currency`, usando enums para os paises e moedas inicialmente suportados.

**Motivo:** O projeto pode evoluir para uso em multiplos paises, dashboards por pais e conversao cambial futura.

**Alternativas consideradas:** Manter moeda fixa em EUR/GBP ou usar strings livres.

**Impacto:** Valores devem ser sempre exibidos com a moeda da conta. Totais com moedas diferentes devem ser agrupados por moeda enquanto nao houver servico de conversao cambial.

## 2026-07-09 - Protecao simples por cookie

**Descricao:** A aplicacao usa uma protecao opcional por cookie, ativada por configuracao `AccessProtection`.

**Motivo:** Antes de expor o sistema na rede local, e necessario impedir acesso direto a interface e APIs.

**Alternativas consideradas:** Sem autenticacao, Basic Auth, Identity completo.

**Impacto:** Credenciais devem ser configuradas por User Secrets ou variaveis de ambiente. Para exposicao fora da rede local, ainda sera necessario avaliar HTTPS, reverse proxy e autenticacao mais robusta.

## 2026-07-09 - Dashboard por pais e moeda sem conversao

**Descricao:** A interface agrupa os vencimentos do mes selecionado por pais e moeda.

**Motivo:** O usuario precisa enxergar a distribuicao das contas por pais sem misturar moedas diferentes.

**Alternativas consideradas:** Somar todos os valores em uma moeda principal ou criar conversao cambial nesta sprint.

**Impacto:** O dashboard melhora a leitura operacional agora e preserva um ponto claro para futura integracao com servico de cambio.

## 2026-07-09 - Exportar relatorio mensal no navegador

**Descricao:** A interface gera um CSV dos vencimentos do mes selecionado usando os dados ja carregados no navegador.

**Motivo:** O usuario ganha um relatorio simples para Excel/Sheets sem aumentar a complexidade da API.

**Alternativas consideradas:** Criar endpoint backend de exportacao ou gerar PDF.

**Impacto:** A exportacao fica rapida e simples. Relatorios oficiais ou PDFs podem ser adicionados depois se houver necessidade.

## 2026-07-09 - Retencao remove somente backups automaticos

**Descricao:** A limpeza automatica remove apenas arquivos `contas.auto.*.json`.

**Motivo:** Backups manuais e `pre-restore` representam decisoes explicitas ou protecoes antes de restauracao e nao devem ser apagados automaticamente.

**Alternativas consideradas:** Aplicar retencao a todos os backups ou deixar limpeza totalmente manual.

**Impacto:** A pasta de backups fica controlada em producao sem risco de apagar pontos de recuperacao escolhidos pelo usuario.

## 2026-07-09 - Health check anonimo e minimo

**Descricao:** `/health` retorna apenas status operacional basico.

**Motivo:** Em Raspberry Pi e systemd, e util ter um endpoint simples para confirmar que a aplicacao esta respondendo.

**Alternativas consideradas:** Expor diagnostico detalhado ou manter apenas logs.

**Impacto:** Facilita verificacao operacional sem expor contas, caminhos locais ou segredos.

## 2026-07-09 - CSP compativel antes de CSP estrita

**Descricao:** A aplicacao aplica cabeçalhos HTTP de seguranca e uma CSP compativel com a interface atual.

**Motivo:** A tela de login ainda tem script/estilo inline e a interface principal ainda usa handlers inline gerados por JavaScript.

**Alternativas consideradas:** Forcar CSP estrita imediatamente ou deixar todos os cabeçalhos para o reverse proxy.

**Impacto:** O app ganha protecoes basicas agora sem quebrar a interface. Uma sprint futura deve remover inline para permitir CSP sem `unsafe-inline`.
