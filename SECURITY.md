# SECURITY

## Checklist de segurança

- [ ] HTTPS se o sistema for exposto fora da máquina local.
- [x] Segredos fora do código.
- [x] User Secrets previsto para desenvolvimento.
- [x] Variáveis de ambiente documentadas para produção.
- [x] `.env`, `data/` e notas locais fora do Git.
- [ ] Token/chat id removidos de `notas.txt` e do histórico se já foram versionados.
- [x] Validação de entrada em rotas de criação/edição.
- [ ] Proteção contra SQL Injection não aplicável enquanto não houver SQL.
- [x] Rate limiting no endpoint de login.
- [x] Cabeçalhos HTTP básicos de segurança aplicados pela aplicação.
- [x] CSP estrita sem `unsafe-inline`.
- [x] Autenticação opcional antes de uso em rede.
- [x] Autorização básica aplicada pelo middleware de proteção.
- [x] Backups do arquivo JSON de dados.
- [x] Logs sem segredos conhecidos.
- [ ] LGPD/GDPR avaliados se dados pessoais forem cadastrados.

## Saúde operacional

O endpoint `/health` retorna apenas status operacional minimo. Ele nao deve incluir caminhos locais, tokens, chat id, usuario, senha, ambiente, horario ou dados de contas.

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
