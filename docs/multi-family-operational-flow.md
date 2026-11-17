# Fluxo operacional multi-family

## Status e isolamento de runtime

A Fase 4 torna o modo relacional utilizavel em Development/Testing e no ambiente Pilot isolado. `MultiFamily:Enabled=false` continua default e registra exclusivamente `ContaStore`, AccessProtection, Telegram global, `DailyReminderService` e `AutomaticBackupService`. Nenhum DbContext, Identity, bootstrap ou worker relacional e exigido nesse modo. O Pilot nao executa bootstrap no startup; somente o comando administrativo explicito pode usa-lo sob o gate temporario descrito abaixo.

Com a flag ativa, PostgreSQL e obrigatorio, APIs JSON sao bloqueadas e o frontend redireciona para `multi-family.html`.

## Bootstrap administrativo

Nao existe cadastro publico ou endpoint de bootstrap. O comando explicito e:

```text
dotnet run -- bootstrap-multi-family
```

Em Development/Testing, o comportamento existente permanece disponivel com `MultiFamily:Enabled=true`. Em Pilot, o comando exige adicionalmente `PilotBootstrap__Enabled=true`; o default versionado e `false`. Production e ambientes desconhecidos sempre rejeitam o comando. Email, senha e nome da familia devem vir de `Bootstrap__Email`, `Bootstrap__Password` e `Bootstrap__FamilyName` no ambiente/secret privado. A senha e os identificadores criados nao sao impressos nem logados. O comando cria AppUser, Family, membership Owner e FamilySettings somente quando usuario e familia ainda nao existem. Repetir exatamente a mesma identidade e idempotente. Familia ou usuario preexistente sem a mesma membership Owner falha fechado e exige intervencao administrativa explicita; o bootstrap nunca cria um Owner adicional. Uma membership Owner esperada, mas inativa, so e reativada quando nao existe outro Owner ativo.

No Pilot, `PilotBootstrap__Enabled` e os valores `Bootstrap__*` devem existir somente durante a execucao administrativa explicita. Depois do sucesso, o operador deve remover/desabilitar a flag e remover as credenciais temporarias antes do startup normal. O startup da aplicacao nunca executa bootstrap automaticamente e `Registration:Enabled=false` continua independente. Nao existe endpoint HTTP de bootstrap.

## Reset administrativo de password

O reset de password de uma identidade existente tambem e exclusivamente CLI:

```text
dotnet run -- reset-pilot-password
```

Development/Testing preservam o uso administrativo local com `MultiFamily:Enabled=true`. Em Pilot, o comando exige adicionalmente `PilotPasswordReset__Enabled=true`; o default versionado e `false`. Production e ambientes desconhecidos sempre rejeitam. O email alvo e a nova password devem vir apenas de `PasswordReset__Email` e `PasswordReset__NewPassword` em configuracao privada temporaria.

O servico localiza somente usuario ativo e usa `UserManager.GeneratePasswordResetTokenAsync` seguido de `UserManager.ResetPasswordAsync`; nunca manipula `PasswordHash`. Falhas de policy retornam apenas codes Identity e usuario ausente/inativo recebe erro generico. Email, password, token e UserId nao sao logados nem impressos. Depois do sucesso, a flag e os valores temporarios devem ser removidos antes do startup normal. Nao existe endpoint HTTP administrativo de reset e `Registration:Enabled=false` permanece independente.

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

Mutacoes exigem antiforgery. `FamilyId` de query, headers ou payload nao e autoridade. O ultimo Owner ativo nao pode ser removido nem rebaixado. Novos memberships usam convite de uso unico e podem ser somente Admin ou Member; nao ha cadastro publico, entrega automatizada ou convite para Owner.

## Settings e Telegram

FamilySettings controla moeda default, timezone IANA e horario/minuto. TelegramSettings retorna somente ChatId mascarado e indicador de referencia configurada. O token real e resolvido em runtime por `MultiFamilyTelegramSecrets:{FamilyId}:{reference}`, impedindo reutilizacao cross-family, e nunca e armazenado na tabela ou retornado pela API. Testes usam sender fake e nenhum Telegram real.

## Worker relacional

`MultiFamilyReminderWorker` e registrado somente quando a flag esta ativa. Ele nao usa `ICurrentFamilyContext`: enumera familias ativas e processa cada `FamilyId` explicitamente. Todas as queries de settings, contas, pagamentos e lembretes incluem o tenant. Falha de uma familia gera log tecnico e permite continuar; nenhum Telegram e enviado quando nao ha contas pendentes, e lembrete so e marcado depois de envio bem-sucedido.

## Frontend

O frontend legado foi preservado. O novo HTML/JS usa os endpoints `/api/multi-family`, mostra familia/role, implementa CRUD de contas e pagamentos, settings e members. Controles visuais seguem roles, mas toda autorizacao permanece no backend.

## Limitacoes e proximos passos

- modo multi-family continua proibido fora de Development/Testing;
- nao houve importacao real, cutover ou deploy;
- convite manual existe; entrega automatizada e recovery UI permanecem pendentes;
- sessao em memoria requer substituicao antes de multiplas replicas;
- a UI operacional e funcional, mas ainda nao replica todos os dashboards/backup/exportacao do modo JSON;
- antes do piloto: review dedicado, testes de navegador, restore ensaiado, secrets rotacionados e cutover separado.
