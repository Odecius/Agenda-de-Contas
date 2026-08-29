# Convites multi-family

## Escopo

O fluxo permite que um Owner convide uma identidade como Admin ou Member no tenant atual. Ele permanece atras de `MultiFamily:Enabled`, restrito ao modo experimental, e nao altera o runtime `ContaStore + JSON`.

## Trust boundary

O request informa somente email e role. A familia e o usuario criador vem de `ICurrentFamilyContext`; propriedades extras, query strings e headers nao podem escolher o tenant. Convites de outra familia retornam 404 em listagem/revogacao e nunca revelam sua existencia.

O antigo cadastro direto de membership por email foi removido. Toda nova associacao de Admin ou Member passa pelo aceite do convite; alteracao de role e desativacao de memberships existentes continuam nos endpoints administrativos proprios.

## Ciclo de vida

1. O Owner cria o convite.
2. O servidor gera um token criptograficamente aleatorio e persiste apenas SHA-256.
3. Um novo convite para o mesmo email revoga convites pendentes anteriores daquela familia.
4. O link usa fragmento de URL; o navegador remove o fragmento antes do request de aceite.
5. Uma identidade nova define senha; uma existente confirma a senha atual e respeita o lockout Identity.
6. No PostgreSQL, o aceite relê e bloqueia a linha do convite com `FOR UPDATE` dentro da transação antes de criar a identidade; isso serializa requests concorrentes do mesmo token entre múltiplas instâncias.
7. A membership e o consumo condicional do token usam a mesma transação. Falha após criar a identidade, consumir o convite ou preparar a membership reverte todas essas alterações.
8. Token expirado, revogado, adulterado ou reutilizado falha com resposta generica.

## Autorizacao e constraints

- apenas Owner cria, lista e revoga;
- role limitada a Admin ou Member;
- `FamilyId`, `CreatedByUserId` e `AcceptedByUserId` possuem FKs;
- hash do token e unico;
- expiracao deve ser posterior a criacao;
- convite nao pode estar aceito e revogado simultaneamente;
- aceite e usuario aceitante devem ser ambos nulos ou ambos preenchidos;
- membership continua protegida pela PK composta `(FamilyId, UserId)`.

## Endpoints

- `GET /api/multi-family/invitations`
- `POST /api/multi-family/invitations`
- `DELETE /api/multi-family/invitations/{id}`
- `POST /api/multi-family/invitations/accept`

Mutacoes exigem antiforgery. O aceite possui rate limiting adicional e nunca retorna detalhes que permitam distinguir email, token, expiracao ou senha invalidos.

## Limites

- entrega automatizada do link ainda nao existe;
- password recovery ainda nao existe;
- convites nao criam Owner;
- a funcionalidade nao autoriza ativacao do modo multi-family em producao;
- a migration foi gerada, revisada e validada em PostgreSQL 16 descartavel, mas nao deve ser aplicada fora de ambiente descartavel sem um gate de cutover separado.
- o harness `tests/run-postgresql16-gate.ps1` concluiu com 59/59 testes: dois aceites concorrentes do mesmo token produziram um unico vencedor, nenhuma membership duplicada foi criada, o rollback preservou estado consistente, constraints e isolamento passaram e o cleanup terminou sem recursos residuais.
