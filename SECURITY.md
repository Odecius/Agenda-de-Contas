# SECURITY

## Checklist de segurança

- [ ] HTTPS se o sistema for exposto fora da máquina local.
- [x] Segredos fora do código.
- [x] User Secrets previsto para desenvolvimento.
- [x] Variáveis de ambiente documentadas para produção.
- [x] `.env`, `data/` e notas locais fora do Git.
- [ ] Token/chat id removidos de `notas.txt` e do histórico se já foram versionados.
- [x] Validação de entrada em rotas de criação/edição.
- [x] Acesso relacional usa EF parametrizado; SQL explicito possui parametros interpolados pelo provider.
- [x] Rate limiting no endpoint de login.
- [x] Cabeçalhos HTTP básicos de segurança aplicados pela aplicação.
- [x] CSP estrita sem `unsafe-inline`.
- [x] Autenticação opcional antes de uso em rede.
- [x] Autorização básica aplicada pelo middleware de proteção.
- [x] Convites multi-family vinculados server-side ao tenant, com hash, expiracao, revogacao e uso unico.
- [x] Password recovery Identity sem enumeracao, token em resposta ou dependencia de tenant.
- [x] Falhas de delivery de recovery preservam resposta generica e a origem publica exige HTTPS.
- [x] Cadastro publico possui switch separado desabilitado por default e nao aceita identificadores de tenant/Owner.
- [x] Criacao de identidade, Family e primeiro Owner e atomica; operacoes concorrentes preservam ao menos um Owner.
- [x] Perfil Pilot falha fechado sem connection string e mantem registration/delivery desabilitados.
- [x] Readiness nao revela connection string, hostname, versao ou topologia.
- [x] Rehearsal de backup/restore usa somente credenciais e dados sinteticos descartaveis.
- [x] Delivery externo e fail-safe, HTTPS-only quando habilitado e nao registra destino, URL, token ou credential.
- [x] Backups do arquivo JSON de dados.
- [x] Logs sem segredos conhecidos.
- [ ] LGPD/GDPR avaliados se dados pessoais forem cadastrados.

## Saúde operacional

O endpoint `/health` retorna apenas liveness. Em MultiFamily, `/health/ready` confirma conectividade e migrations sem incluir caminhos locais, hostnames, versoes, tokens, chat id, usuario, senha, ambiente, horario ou dados de contas.

No deploy Docker, a porta interna da aplicacao nao deve ser publicada diretamente no host.
O reverse proxy deve acessar o servico por uma rede Docker externa, e a exposicao
publica deve usar HTTPS. A imagem inclui apenas a dependencia necessaria para executar
o healthcheck interno contra `/health`.

As chaves ASP.NET Data Protection usadas pelo cookie de login devem permanecer no
volume persistente em `/var/lib/agendador-contas/dataprotection-keys`. Elas nao devem
ser publicadas no Git nem compartilhadas entre aplicacoes diferentes.

`appsettings.Production.json` e `appsettings.Production.local.json` nao devem guardar segredos nem entrar no Git. Em producao, use variaveis de ambiente ou o arquivo `.env` real do servidor, mantido fora do repositorio.

## Cabeçalhos HTTP

A aplicação aplica cabeçalhos como `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`, `Cross-Origin-Opener-Policy` e `Content-Security-Policy`.

Em `Production`, a aplicacao tambem envia `Strict-Transport-Security` por um ano.
O dominio deve permanecer exclusivamente em HTTPS durante esse periodo.

A CSP atual nao permite `unsafe-inline`. A tela principal e a tela de login carregam CSS/JS por arquivos externos.

## Risco atual

`notas.txt`/`NOTAS.txt` deve permanecer ignorado pelo Git. Se algum token/chat id ja tiver sido versionado no passado, a correção recomendada é revogar o token, gerar outro no BotFather e limpar o histórico Git antes de compartilhar o repositório.

Links de convite sao credenciais temporarias. O token fica no fragmento da URL no navegador, e removido do endereco assim que a pagina carrega e nunca deve aparecer em logs. O Owner deve compartilhar o link por canal seguro. A abstracao de delivery existe, mas o provider externo permanece desabilitado ate homologacao separada.

Tokens de recovery sao credenciais temporarias. Somente a abstracao de entrega os recebe; a URL usa origem publica configurada e o provider default nao registra ou entrega o valor. Consulte `docs/password-recovery.md`.
