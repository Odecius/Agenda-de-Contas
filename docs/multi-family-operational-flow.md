# Fluxo operacional multi-family

## Status e isolamento de runtime

A Fase 4 torna o modo relacional utilizavel somente em Development/Testing. `MultiFamily:Enabled=false` continua default e registra exclusivamente `ContaStore`, AccessProtection, Telegram global, `DailyReminderService` e `AutomaticBackupService`. Nenhum DbContext, Identity, bootstrap ou worker relacional e exigido nesse modo.

Com a flag ativa, PostgreSQL e obrigatorio, APIs JSON sao bloqueadas e o frontend redireciona para `multi-family.html`.

## Bootstrap administrativo

Nao existe cadastro publico ou endpoint de bootstrap. O comando explicito e:

```text
dotnet run -- bootstrap-multi-family
```

Ele somente funciona com a flag ativa em Development/Testing. Email, senha e nome da familia devem vir de `Bootstrap__Email`, `Bootstrap__Password` e `Bootstrap__FamilyName` no ambiente/secret local. A senha nao e impressa nem logada. O comando cria AppUser, Family, membership Owner e FamilySettings somente quando usuario e familia ainda nao existem. Repetir exatamente a mesma identidade e idempotente. Familia ou usuario preexistente sem a mesma membership Owner falha fechado e exige intervencao administrativa explicita; o bootstrap nunca cria um Owner adicional. Uma membership Owner esperada, mas inativa, so e reativada quando nao existe outro Owner ativo.

## Login e familia ativa

`login.js` detecta `/api/multi-family/mode`, busca antiforgery e usa Identity. A senha e limpa antes do redirecionamento e nada sensivel e armazenado em localStorage. A pagina operacional lista familias autorizadas, usa selecao server-side e limpa o estado em memoria antes de recarregar outro tenant.

## Matriz de autorizacao

| Acao | Owner | Admin | Member |
| --- | --- | --- | --- |
| Ver contas/pagamentos/settings | sim | sim | sim |
| Criar/editar/ativar conta | sim | sim | nao |
| Excluir conta/pagamento | sim | nao | nao |
| Registrar pagamento | sim | sim | sim |
| Listar members | sim | sim | nao |
| Mutar memberships | sim | nao | nao |
| Alterar FamilySettings | sim | sim | nao |
| Alterar TelegramSettings | sim | nao | nao |

Mutacoes exigem antiforgery. `FamilyId` de query, headers ou payload nao e autoridade. O ultimo Owner ativo nao pode ser removido nem rebaixado. Nesta fase novos memberships podem ser somente Admin ou Member; nao ha convite, email ou cadastro publico.

## Settings e Telegram

FamilySettings controla moeda default, timezone IANA e horario/minuto. TelegramSettings retorna somente ChatId mascarado e indicador de referencia configurada. O token real e resolvido em runtime por `MultiFamilyTelegramSecrets:{FamilyId}:{reference}`, impedindo reutilizacao cross-family, e nunca e armazenado na tabela ou retornado pela API. Testes usam sender fake e nenhum Telegram real.

## Worker relacional

`MultiFamilyReminderWorker` e registrado somente quando a flag esta ativa. Ele nao usa `ICurrentFamilyContext`: enumera familias ativas e processa cada `FamilyId` explicitamente. Todas as queries de settings, contas, pagamentos e lembretes incluem o tenant. Falha de uma familia gera log tecnico e permite continuar; nenhum Telegram e enviado quando nao ha contas pendentes, e lembrete so e marcado depois de envio bem-sucedido.

## Frontend

O frontend legado foi preservado. O novo HTML/JS usa os endpoints `/api/multi-family`, mostra familia/role, implementa CRUD de contas e pagamentos, settings e members. Controles visuais seguem roles, mas toda autorizacao permanece no backend.

## Limitacoes e proximos passos

- modo multi-family continua proibido fora de Development/Testing;
- nao houve importacao real, cutover ou deploy;
- nao ha convite, recovery UI ou criacao de usuarios alem do bootstrap administrativo;
- sessao em memoria requer substituicao antes de multiplas replicas;
- a UI operacional e funcional, mas ainda nao replica todos os dashboards/backup/exportacao do modo JSON;
- antes do piloto: review dedicado, testes de navegador, restore ensaiado, secrets rotacionados e cutover separado.
