# ROADMAP

## Sprint 10 - Experiência de cadastro e edição

- Melhorar validações visuais.
- Exibir mensagens claras.
- Confirmar exclusões.
- Melhorar edição.
- Adicionar filtros simples.

## Sprint 11 - Histórico e pagamentos

- Refinar histórico de pagamentos.
- Melhorar visualização de meses pagos e pendentes.
- Criar testes das regras de pagamento.

## Sprint 12 - Resumo mensal

- Criar painel de resumo mensal.
- Exibir total previsto, pago e pendente.
- Destacar contas vencendo hoje.

## Sprint 13 - Pais e moeda

- Adicionar pais e moeda por conta.
- Exibir valores com GBP, EUR ou BRL.
- Agrupar totais por moeda sem conversao cambial.
- Preparar caminho para conversao futura.

## Sprint 14 - Deploy Raspberry Pi

- Preparar modelo `systemd`.
- Preparar exemplo de variaveis de ambiente de producao.
- Documentar instalacao, atualizacao, logs e backup manual.
- Deixar validacao real pendente ate o Raspberry estar disponivel.

## Sprint 15 - Proteção de acesso

- Adicionar autenticação simples.
- Revisar autorização das rotas.
- Avaliar HTTPS/reverse proxy se exposto fora da rede local.

## Sprint 16 - Backup e recuperação

- Criar backup manual dos dados locais.
- Listar backups disponíveis.
- Restaurar backup com confirmação.
- Criar backup `pre-restore` antes de substituir dados atuais.

## Sprint 17 - Testes automatizados

- Criar test runner automatizado sem dependencias externas.
- Testar vencimentos, pagamentos, lembretes, pais/moeda e backups.
- Manter autenticacao e endpoints completos para uma proxima rodada de testes.

## Sprint 18 - Dashboard por pais e moeda

- Criar resumo visual agrupado por pais e moeda.
- Filtrar contas cadastradas por pais e moeda.
- Manter valores separados por moeda, sem conversao cambial.
- Preparar base visual para relatorios por pais/moeda em sprint futura.

## Sprint 19 - Exportacao CSV mensal

- Exportar vencimentos do mes selecionado em CSV.
- Incluir pais, moeda, valor, status e observacoes.
- Manter exportacao sem conversao cambial.
- Preparar base para relatorios mais completos por pais e moeda.

## Sprint 20 - Backup automatico e retencao

- Criar hosted service para backup automatico diario.
- Configurar `Backup__*` por appsettings/variaveis de ambiente.
- Aplicar retencao apenas em backups automaticos antigos.
- Preservar backups manuais e `pre-restore`.

## Sprint 21 - Saude operacional

- Criar endpoint `/health` para verificacao rapida.
- Manter resposta sem segredos ou caminhos locais.
- Documentar teste do endpoint no deploy Raspberry.

## Sprint 22 - Fechamento operacional

- Criar checklist final do projeto.
- Consolidar comandos de validacao local.
- Registrar pendencias que dependem de Raspberry real.
- Manter proximas evolucoes como melhorias, nao bloqueios do MVP.

## Sprint 23 - Testes de seguranca basica

- Cobrir rotas anonimas permitidas pela protecao de acesso.
- Confirmar que APIs e pagina principal nao entram na lista anonima.
- Confirmar que protecao ativa exige senha configurada.

## Sprint 24 - Visibilidade de sincronizacao

- Exibir ultima atualizacao dos dados no cabecalho.
- Atualizar timestamp apos carregamento bem-sucedido.
- Melhorar confianca de uso quando a tela fica aberta por muito tempo.

## Sprint 25 - Cabeçalhos HTTP de seguranca

- Aplicar headers basicos no pipeline ASP.NET Core.
- Reduzir risco de MIME sniffing, clickjacking e vazamento de referencia.
- Configurar CSP compatível com a interface atual.
- Registrar que CSP estrita depende de remover scripts/handlers inline.

## Sprint 26 - Remover inline handlers da tela principal

- Trocar `onclick` gerado no dashboard por delegacao de eventos.
- Centralizar acoes de contas, vencimentos e backups em handlers JavaScript.
- Deixar a tela principal preparada para CSP mais rigida.

## Sprint 27 - CSP estrita

- Externalizar CSS e JavaScript da tela de login.
- Remover `unsafe-inline` da Content Security Policy.
- Confirmar paginas sem scripts, estilos ou handlers inline.

## Sprint 28 - Rate limiting no login

- Aplicar limite de tentativas em `/api/auth/login`.
- Retornar HTTP 429 quando o limite for excedido.
- Reduzir risco de tentativa automatizada de senha.

## Sprint 29 - Deploy HP Linux

- Preparar runbook especifico para servidor HP Linux x64.
- Validar publish `linux-x64`.
- Reusar modelos de `systemd` e variaveis de ambiente sem segredos reais.
- Deixar deploy real pendente de acesso SSH, .NET Runtime 8 e configuracao do ambiente no servidor.

## Sprint 30 - Deploy Docker HP

- Criar Dockerfile multi-stage com .NET 8.
- Criar `.dockerignore` com exclusao de segredos, dados locais e artefatos de build.
- Criar Docker Compose para HP usando rede externa `proxy`.
- Montar dados persistentes em `/srv/data/apps/agendador`.
- Usar arquivo `.env` externo em `/srv/stacks/apps/agendador`.
- Manter porta `5005` apenas em `127.0.0.1` e acesso publico via Nginx Proxy Manager.

## Sprint 31 - Horario do lembrete pela interface

- Criar configuracao persistente para o horario do lembrete diario.
- Expor rotas protegidas para consultar e atualizar hora/minuto.
- Adicionar controle na interface principal.
- Manter `Reminder__*` como defaults iniciais para ambiente.
- Validar persistencia local e preparar validacao no volume Docker.
