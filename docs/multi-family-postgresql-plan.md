# Plano tecnico - PostgreSQL e multiplas familias

## Status

Plano para revisao. Nenhuma etapa de PostgreSQL, autenticacao, importacao ou multi-tenancy esta implementada.

Baseline protegido: branch `master`, tag `v1.0.4`, commit `e06d30e`.

## Objetivo e principios

Evoluir a instancia privada atual para a familia existente e duas familias piloto, preservando todos os dados. O primeiro schema PostgreSQL ja deve ser multi-tenant. O backend deriva a familia da identidade autenticada; nenhum `FamilyId` enviado pelo frontend e autoritativo.

Principios:

- mudancas pequenas, aditivas e reversiveis quando possivel;
- JSON original imutavel durante importacao e validacao;
- database, owner e credenciais dedicados;
- tenant aplicado em toda leitura e mutacao privada;
- retorno `404` para IDs pertencentes a outra familia;
- secrets fora do Git, respostas e logs;
- deploy somente apos backup e restore ensaiados.

## PostgreSQL operacional

### Opcao recomendada

Usar o PostgreSQL geral existente com um database dedicado ao Agendador, owner dedicado e privilegios minimos. Para tres familias, reduz consumo e operacao sem misturar tabelas ou credenciais com outras aplicacoes.

Requisitos:

- database exclusivo;
- role de ownership sem login para objetos, se o padrao operacional permitir;
- role de runtime com apenas conexao e DML necessario;
- role separada para migrations;
- backup e restore por database;
- limites de conexao e monitorizacao;
- connection string somente em secret de producao.

### Container PostgreSQL exclusivo

Oferece isolamento de processo, ciclo de upgrade e restore mais independentes, mas cria outro servico stateful, maior consumo e mais manutencao. Passa a ser preferivel se o PostgreSQL geral nao oferecer restore isolado, versionamento compativel, limites de recursos ou janela operacional adequada.

Decisao final antes da implementacao: confirmar capacidade de backup/restore por database e politica de upgrades do PostgreSQL geral. Se forem satisfatorias, usar database dedicado no servidor geral.

## Modelo relacional proposto

### Family

- `Id uuid`;
- `Name`;
- `CreatedAtUtc`;
- `IsActive`;
- `DeactivatedAtUtc` opcional.

### AppUser

- chave compativel com ASP.NET Core Identity;
- email/username normalizados e unicos;
- password hash gerenciado pelo Identity;
- confirmacao e recovery tokens;
- estado ativo e timestamps.

### FamilyUser

- `FamilyId`;
- `UserId`;
- `Role`: `Owner`, `Admin`, `Member`;
- `IsActive`;
- `JoinedAtUtc`;
- chave unica `(FamilyId, UserId)`.

No piloto, exigir uma unica familia ativa por usuario na camada de aplicacao. A tabela associativa preserva a possibilidade futura de participacao em mais de uma familia sem remodelar o banco.

### FamilySettings

- `FamilyId` como PK/FK;
- `CurrencyCode` principal para apresentacao, sem conversao;
- `TimeZoneId` IANA;
- `ReminderHour` e `ReminderMinute`;
- timestamps de alteracao.

As contas continuam mantendo sua moeda original. A moeda familiar nao autoriza somar moedas distintas.

### TelegramSettings

- `FamilyId` como PK/FK;
- `IsEnabled`;
- `ChatId` protegido;
- referencia ao token em secret store ou token cifrado com chave externa ao banco;
- timestamps e ultimo estado de envio sem corpo sensivel.

### Conta

- preservar `Id` UUID atual;
- adicionar `FamilyId` obrigatorio;
- manter nome, valor, vencimento, inicio, duracao, ativa, observacoes, pais e moeda;
- indice `(FamilyId, Id)` e indices de consultas por vencimento/estado;
- validacoes equivalentes ao dominio atual.

### Pagamento

- `Id` novo ou chave natural controlada;
- `FamilyId`;
- `ContaId`;
- ano, mes e `PagoEmUtc`;
- unicidade `(FamilyId, ContaId, Ano, Mes)`;
- FK composta garantindo que conta e pagamento pertençam a mesma familia.

### LembreteEnviado

- `Id`;
- `FamilyId`;
- data local do lembrete;
- canal/destino logico;
- `SentAtUtc`;
- unicidade `(FamilyId, LocalDate, Channel)` para idempotencia.

## Autenticacao e autorizacao

Usar ASP.NET Core Identity com cookie seguro:

- hash de senha pelo PasswordHasher/Identity, com parametros suportados pelo framework;
- `Secure`, `HttpOnly`, `SameSite=Lax` ou mais restritivo conforme fluxo;
- expiracao, sliding expiration e revogacao documentadas;
- antiforgery em mutacoes autenticadas por cookie;
- rate limiting particionado por IP/identidade;
- email confirmado antes de recuperar senha;
- tokens de convite e recovery de uso unico, expiraveis e armazenados de forma segura;
- respostas de login/recovery sem enumeracao de usuarios.

Convites devem conter familia e role decididas no servidor. Somente `Owner` e, conforme politica, `Admin` convidam membros. O primeiro usuario importado sera `Owner` da familia existente.

## CurrentFamilyContext e isolamento

Depois de autenticar:

1. resolver o `UserId` da identidade;
2. carregar `FamilyUser` ativo;
3. carregar a `Family` e exigir `IsActive=true`;
4. criar `CurrentFamilyContext` scoped e imutavel;
5. aplicar `FamilyId` em toda query e comando.

Regras:

- DTOs de criacao/edicao nao aceitam `FamilyId`, ou rejeitam o campo;
- busca por ID sempre usa `(Id, CurrentFamilyId)`;
- cross-tenant retorna `404`;
- query filters do EF Core podem ser defesa adicional, nunca a unica barreira;
- raw SQL, jobs, exports, backups logicos e administracao exigem filtro explicito;
- roles sao politicas do backend, nao controles visuais;
- familia inativa bloqueia login funcional, APIs e workers, sem apagar dados.

## Importador JSON para PostgreSQL

O importador deve ser ferramenta separada, executada primeiro em copia controlada:

1. abrir o JSON somente para leitura;
2. calcular hash SHA-256 e registrar tamanho/timestamp sem dados pessoais;
3. validar schema, IDs, referencias, enums, datas, valores e duplicatas;
4. criar a familia existente com UUID gerado e nome configurado;
5. criar o Owner por fluxo seguro, sem senha embutida;
6. importar contas preservando UUIDs;
7. importar pagamentos e relaciona-los a conta/familia;
8. importar lembretes enviados e configuracao de `settings.json`;
9. criar FamilySettings com `Europe/London` e defaults documentados;
10. migrar Telegram separadamente, sem registrar token em logs;
11. executar tudo em transacao ou lotes retomaveis com marcador de importacao;
12. gravar uma chave unica de importacao baseada em origem/hash para impedir duplicacao.

Validacoes de reconciliacao:

- quantidade de contas, ativas/inativas e pagamentos;
- conjunto exato de IDs;
- soma de valores por moeda, nunca soma entre moedas;
- pagamentos por ano/mes;
- contas orfas e duplicatas iguais a zero;
- settings e historico de lembretes;
- repeticao do importador produz zero duplicatas;
- JSON original e backups permanecem inalterados.

O corte somente ocorre depois de leitura comparativa e aceite. O JSON permanece arquivado em modo somente leitura durante a janela de rollback.

## Telegram e workers

O worker enumera familias ativas com notificacao habilitada, calcula o horario no timezone familiar, consulta apenas contas daquele tenant e registra idempotencia por familia/data/canal. Falha de uma familia nao bloqueia as demais. Logs incluem identificador tecnico da familia, nunca token, ChatId completo, mensagem pessoal ou response body sensivel.

## Testes obrigatorios

### Isolamento A/B

- A ve, cria e altera somente dados de A;
- B ve, cria e altera somente dados de B;
- A nao le, altera, exclui, ativa ou paga conta de B;
- IDs em URL, query e payload manipulados nao atravessam tenant;
- listagens, CSV, settings, backups logicos e Telegram nao misturam familias.

### Roles e estado

- Owner, Admin e Member respeitam a matriz autorizada;
- convite nao pode elevar role indevidamente;
- usuario removido perde acesso;
- familia inativa bloqueia usuarios e workers sem apagar dados.

### Worker e timezone

- horarios distintos por familia;
- idempotencia em restart/concorrencia;
- falha de Telegram isolada;
- DST em `Europe/London` e outros timezones IANA.

### Importacao e operacao

- importacao completa, invalida e repetida;
- preservacao de IDs e reconciliacao financeira;
- rollback antes e depois do corte;
- migrations `Up` em copia de producao;
- restore PostgreSQL testado;
- compatibilidade do frontend durante a transicao.

## Backup, RPO, RTO e rollback

Proposta inicial para o piloto, sujeita a aprovacao operacional:

- RPO: no maximo 24 horas, preferencialmente backup diario mais WAL/PITR se a infraestrutura ja suportar;
- RTO: ate 4 horas para restaurar database e aplicacao;
- backup logico diario do database dedicado;
- backup fisico/PITR conforme o PostgreSQL geral;
- copia fora do mesmo disco/host;
- teste de restore antes do piloto e periodicamente;
- retencao documentada e cifrada.

Antes do primeiro deploy:

- preservar a imagem atual por ID/digest;
- registrar imagem -> commit `e06d30e`/tag ou documentar divergencia;
- copiar e hashear JSON/settings/backups sem alterar os originais;
- manter Compose e secrets atuais recuperaveis;
- criar plano de retorno para a imagem v1.0.4 e JSON original.

Se o corte falhar: interromper novas gravacoes, restaurar a versao anterior, remontar o volume JSON preservado e validar `/health`, login, contas, pagamentos, Telegram e backups. Nunca converter PostgreSQL de volta para JSON automaticamente.

## Sequencia em etapas pequenas

1. aprovar este plano e matriz de roles;
2. confirmar rotacao de secrets e RPO/RTO;
3. criar checkpoint/tag de trabalho sem alterar producao;
4. adicionar testes de caracterizacao do JSON atual;
5. modelar schema multi-tenant e migrations em ambiente local;
6. preparar database/roles dedicados em ambiente nao produtivo;
7. implementar e testar importador idempotente;
8. importar copia anonimizada/controlada e reconciliar;
9. adicionar Identity, convite e recovery sem liberar pilotos;
10. implementar CurrentFamilyContext e filtros em todos os endpoints;
11. adaptar settings, Telegram, workers e familia inativa;
12. executar testes A/B, seguranca e restore;
13. ensaiar migration completa em copia de producao;
14. realizar corte controlado da familia existente com rollback pronto;
15. observar estabilidade e reconciliar dados;
16. liberar Familia A;
17. observar, corrigir e repetir testes;
18. liberar Familia B.

## Criterios de liberacao

### Familia A

- familia existente estavel no PostgreSQL pelo periodo acordado;
- backup e restore comprovados dentro de RPO/RTO;
- zero divergencias de reconciliacao;
- testes A/B, IDOR, roles, worker e payload manipulado aprovados;
- secrets rotacionados e logs revisados;
- HTTPS/cookies/antiforgery validados;
- suporte e rollback documentados.

### Familia B

- Familia A operou sem incidente de isolamento pelo periodo acordado;
- nenhuma consulta ou notificacao cross-tenant;
- metricas, backups e jobs dentro do esperado;
- convite, recovery e desativacao exercitados;
- nova revisao de seguranca e aceite operacional.
