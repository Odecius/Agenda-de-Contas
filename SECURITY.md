# SECURITY

## Checklist de segurança

- [ ] HTTPS se o sistema for exposto fora da máquina local.
- [ ] Segredos fora do código.
- [x] User Secrets previsto para desenvolvimento.
- [ ] Variáveis de ambiente configuradas em produção.
- [ ] `.env`, `data/` e notas locais fora do Git.
- [ ] Token/chat id removidos de `notas.txt` e do histórico se já foram versionados.
- [ ] Validação de entrada em rotas de criação/edição.
- [ ] Proteção contra SQL Injection não aplicável enquanto não houver SQL.
- [ ] Rate limiting se exposto em rede.
- [ ] CSP e cabeçalhos de segurança se publicado via servidor/reverse proxy.
- [ ] Autenticação antes de uso em rede.
- [ ] Autorização após autenticação.
- [ ] Backups do arquivo JSON de dados.
- [ ] Logs sem segredos.
- [ ] LGPD/GDPR avaliados se dados pessoais forem cadastrados.

## Risco atual

`notas.txt` contém token/chat id do Telegram em texto claro. A correção recomendada é revogar o token, gerar outro no BotFather, remover o segredo do arquivo e limpar o histórico Git se ele tiver sido commitado.
