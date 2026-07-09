# SECURITY

## Checklist de seguranÃ§a

- [ ] HTTPS se o sistema for exposto fora da mÃ¡quina local.
- [ ] Segredos fora do cÃ³digo.
- [x] User Secrets previsto para desenvolvimento.
- [ ] VariÃ¡veis de ambiente configuradas em produÃ§Ã£o.
- [ ] `.env`, `data/` e notas locais fora do Git.
- [ ] Token/chat id removidos de `notas.txt` e do histÃ³rico se jÃ¡ foram versionados.
- [ ] ValidaÃ§Ã£o de entrada em rotas de criaÃ§Ã£o/ediÃ§Ã£o.
- [ ] ProteÃ§Ã£o contra SQL Injection nÃ£o aplicÃ¡vel enquanto nÃ£o houver SQL.
- [ ] Rate limiting se exposto em rede.
- [ ] CSP e cabeÃ§alhos de seguranÃ§a se publicado via servidor/reverse proxy.
- [ ] AutenticaÃ§Ã£o antes de uso em rede.
- [ ] AutorizaÃ§Ã£o apÃ³s autenticaÃ§Ã£o.
- [ ] Backups do arquivo JSON de dados.
- [ ] Logs sem segredos.
- [ ] LGPD/GDPR avaliados se dados pessoais forem cadastrados.

## Risco atual

`notas.txt` contÃ©m token/chat id do Telegram em texto claro. A correÃ§Ã£o recomendada Ã© revogar o token, gerar outro no BotFather, remover o segredo do arquivo e limpar o histÃ³rico Git se ele tiver sido commitado.
