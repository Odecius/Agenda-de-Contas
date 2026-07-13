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
- [ ] Rate limiting se exposto em rede.
- [ ] CSP e cabeçalhos de segurança se publicado via servidor/reverse proxy.
- [x] Autenticação opcional antes de uso em rede.
- [x] Autorização básica aplicada pelo middleware de proteção.
- [x] Backups do arquivo JSON de dados.
- [x] Logs sem segredos conhecidos.
- [ ] LGPD/GDPR avaliados se dados pessoais forem cadastrados.

## Saúde operacional

O endpoint `/health` retorna apenas status, nome da aplicação, ambiente e horário UTC. Ele nao deve incluir caminhos locais, tokens, chat id, usuario, senha ou dados de contas.

## Risco atual

`notas.txt` contém token/chat id do Telegram em texto claro. A correção recomendada é revogar o token, gerar outro no BotFather, remover o segredo do arquivo e limpar o histórico Git se ele tiver sido commitado.
