# TODO

## Alta prioridade

- Revisar e aprovar a Fase 4 antes de qualquer ensaio com copia de dados reais.
- Implementar convite/recovery e politica segura para adicionar novos Owners antes do piloto.
- Executar testes dedicados de navegador para a UI multi-family.
- Revisar a foundation da Fase 3 antes de qualquer ensaio com copia de dados reais.
- Definir CLI/admin command ou procedimento offline para a futura execucao controlada.
- Planejar separadamente settings, lembretes e Telegram sem transportar secrets.
- Trocar sessao em memoria por armazenamento apropriado antes de multiplas replicas.
- Revisar a migration `InitialMultiTenantSchema` antes de preparar qualquer ambiente PostgreSQL.
- Remover token/chat id de `notas.txt` e limpar histórico Git se o segredo já tiver sido versionado.
- Confirmar e registrar a rotação de qualquer token Telegram historicamente exposto.
- Planejar PostgreSQL, autenticação individual e `Family/Tenant` conforme `docs/multi-family-postgresql-plan.md`.
- Definir RPO/RTO e ensaiar restore antes de qualquer migração dos dados reais.
- Registrar correspondência verificável entre futura imagem de produção e commit/tag de origem.
- Validar deploy real em Raspberry Pi quando o hardware estiver disponivel.

## Média prioridade

- Migrar a branch remota principal de `master` para `main` em manutencao Git coordenada.
- Avaliar conversao cambial futura com API externa.
- Melhorar relatorios por moeda e pais.
- Expandir testes automatizados para endpoints completos com servidor em memoria.
- Avaliar HTTPS/reverse proxy apos validacao em rede local.
- Validar Nginx Proxy Manager apontando para `agendador-contas:5005` na rede `proxy`.

## Baixa prioridade

- Corrigir resposta 500 quando nenhuma familia esta selecionada (LOW da Fase 2.2).
- Restringir tratamento amplo de `DbUpdateException` no pagamento (LOW da Fase 2.2).
- Avaliar banco leve, como SQLite, se o JSON deixar de ser suficiente.
- Criar painel mensal com métricas.
- Avaliar novos canais de notificação.

## Concluído

- Fluxo operacional multi-family local: bootstrap, UI, members, settings, Telegram abstrato e worker tenant-aware.
- Foundation de importacao JSON idempotente, transacional e tenant-aware com dry-run.
- Repositories tenant-aware de Conta e Pagamento implementados para o modo experimental.
- Matriz Owner/Admin/Member aplicada com 404 cross-family e 403 por role insuficiente.
- Testes PostgreSQL descartaveis cobrem leitura, escrita, pagamentos, parametros adulterados e troca de familia.

- Identity, contextos de usuario/familia e selecao familiar preparados atras de feature flag local.
- Fundacao local EF Core/PostgreSQL multi-tenant criada, sem conexao com producao.
- Modelo relacional e primeira migration versionados; testes usam SQLite descartavel.
- Deploy Docker real validado no servidor HP Linux.
- Aplicação funcional em produção no container `agendador-contas`.
- Dados JSON persistidos em `/srv/data/apps/agendador`.
- AccessProtection, sessões, Data Protection keys, backups automáticos e health check ativos em produção.
- Timezone de produção confirmado como `Europe/London`.
- Baseline pós-produção registrado para `v1.0.4`/`e06d30e`.

- Cadastro e listagem de contas.
- Marcação/desmarcação de pagamentos.
- Pausar, reativar, editar e excluir contas.
- Lembretes via Telegram.
- Rota de teste em desenvolvimento.
- Interface responsiva.
- Melhorias de UX no cadastro, edicao, exclusao, filtros e feedback visual.
- Painel de resumo mensal.
- Suporte inicial a pais e moeda por conta, sem conversao cambial.
- Dashboard por pais e moeda, sem conversao cambial.
- Exportacao CSV mensal de vencimentos.
- Backup automatico configuravel e retencao segura de backups automaticos.
- Endpoint `/health` para verificacao operacional.
- Checklist final em `docs/final-checklist.md`.
- Dockerfile e Docker Compose preparados para HP Linux.
- Horario do lembrete diario configuravel pela interface.
- Testes automatizados basicos para protecao de acesso.
- Cabeçalhos HTTP basicos de seguranca.
- Tela principal sem handlers `onclick` inline.
- CSP estrita sem `unsafe-inline`.
- Preparacao de deploy Raspberry Pi com systemd, ambiente, logs e checklist.
- Protecao opcional de acesso por cookie.
- Backup manual e restauração com backup `pre-restore`.
- Documentação padronizada em 2026-07-09.
