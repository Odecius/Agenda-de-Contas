# Password recovery multi-family

O recovery atua sobre a identidade global `AppUser` e nao recebe `FamilyId`, tenant, membership ou role. Owner, Admin e Member usam o mesmo fluxo. A feature existe somente com `MultiFamily:Enabled=true`; o runtime legado `ContaStore + JSON` nao muda.

`POST /api/multi-family/auth/forgot-password` sempre devolve a mesma resposta generica. Identity gera o token com `GeneratePasswordResetTokenAsync`; a aplicacao o codifica como Base64URL. O link usa `PasswordRecovery:PublicBaseUrl`, nunca o Host header, e transporta os parametros no fragmento removido pelo navegador antes do request.

`IPasswordRecoveryDeliveryService` desacopla SMTP, Telegram e outros providers. O provider default nao entrega nem registra email, URL ou token. Falha, timeout ou excecao do provider sao contidos internamente e nao alteram a resposta publica generica. `POST /api/multi-family/auth/reset-password` usa `ResetPasswordAsync`, respeita a policy Identity e atualiza o SecurityStamp.

O lifespan default e 60 minutos, configuravel entre 5 e 1440. Antiforgery protege ambas as mutacoes. Por IP, sao permitidas 10 solicitacoes e 15 resets a cada 15 minutos. Limites adicionais em memoria usam apenas hashes SHA-256: 3 solicitacoes por email normalizado e 5 tentativas por token na mesma janela. Antes de multiplas replicas, esses contadores devem usar armazenamento distribuido.

## Threat model

- resposta externa nao enumera identidade, familia ou role;
- origem configurada exige URI HTTPS absoluta, sem credentials, query ou fragment, e impede envenenamento pelo Host header;
- endpoint e logs nao devolvem senha, token, hash ou SecurityStamp;
- replay, adulteracao e expiracao sao validados pelo token provider Identity;
- reset nao consulta nem modifica families, memberships ou roles;
- teste PostgreSQL concorrente exige no maximo um vencedor e senha final consistente.

Provider real de entrega, browser E2E, observabilidade e configuracao operacional da origem publica permanecem pendentes.
