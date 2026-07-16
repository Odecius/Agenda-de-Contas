# Deployment HP Linux com Docker

Este guia prepara o Agendador de Contas para rodar 24/7 no HP Pavilion com Ubuntu Server 24.04 LTS usando Docker Compose, Nginx Proxy Manager e dados persistentes fora do container.

> Status: preparado para validacao no servidor HP. Este guia nao contem segredos reais e nao substitui a configuracao do arquivo `.env` real no servidor.

## Metodo recomendado

Para o servidor HP, o metodo recomendado e Docker Compose.

O deploy via `systemd` continua preservado como alternativa em `docs/deployment.md` e `deploy/agendador-contas.service`.

## Layout no servidor

```text
/srv/apps/agendador              # Codigo fonte do projeto
/srv/data/apps/agendador         # Dados persistentes montados no container
/srv/stacks/apps/agendador       # Compose e arquivo .env real
```

Dentro do container, a aplicacao usa:

```text
/var/lib/agendador-contas/contas.json
/var/lib/agendador-contas/settings.json
/var/lib/agendador-contas/backups/
```

O volume Docker mapeia:

```text
/srv/data/apps/agendador:/var/lib/agendador-contas
```

Esse volume preserva contas, horario configurado do lembrete diario e backups.

## Rede Docker

O servidor ja possui uma rede externa chamada `proxy`, usada pelo Nginx Proxy Manager.

O compose usa essa rede:

```yaml
networks:
  proxy:
    external: true
```

Isso permite que o Nginx Proxy Manager alcance o container pelo nome:

```text
agendador-contas:5005
```

## Arquivos preparados no repositorio

- `Dockerfile`: build multi-stage com SDK apenas na fase de build e `aspnet:8.0` na fase final. A imagem final instala `curl` exclusivamente para o healthcheck do container e continua executando a aplicacao como usuario nao-root.
- `.dockerignore`: exclui Git, binarios, dados locais, `.env`, User Secrets, notas locais e arquivos temporarios.
- `deploy/docker-compose.hp.yml`: compose recomendado para o HP, incluindo healthcheck em `/health`.
- `deploy/agendador-contas.docker.env.example`: exemplo de ambiente sem segredos reais.
- `deploy/agendador-contas.service`: alternativa systemd preservada.
- `deploy/agendador-contas.env.example`: alternativa systemd preservada.

## Preparar diretorios no servidor

No HP:

```bash
sudo mkdir -p /srv/apps/agendador
sudo mkdir -p /srv/data/apps/agendador
sudo mkdir -p /srv/stacks/apps/agendador
```

Como o container roda como usuario nao-root da imagem oficial .NET, ajuste a permissao do diretorio de dados para o UID/GID do usuario `app` da imagem:

```bash
sudo chown -R 1654:1654 /srv/data/apps/agendador
sudo chmod 750 /srv/data/apps/agendador
```

## Copiar arquivos para o servidor

O codigo deve ficar em:

```text
/srv/apps/agendador
```

O compose deve ficar em:

```text
/srv/stacks/apps/agendador/docker-compose.yml
```

Exemplo a partir do Windows:

```powershell
scp -r "C:\Projetos\Abc\Agendador de contas\*" USUARIO@IP_DO_SERVIDOR_HP:/srv/apps/agendador/
scp "C:\Projetos\Abc\Agendador de contas\deploy\docker-compose.hp.yml" USUARIO@IP_DO_SERVIDOR_HP:/srv/stacks/apps/agendador/docker-compose.yml
scp "C:\Projetos\Abc\Agendador de contas\deploy\agendador-contas.docker.env.example" USUARIO@IP_DO_SERVIDOR_HP:/srv/stacks/apps/agendador/agendador.env.example
```

Nao copie `NOTAS.txt`, `.env`, `data/`, `bin/` ou `obj/` para compartilhamento publico.

## Criar arquivo de ambiente real

No servidor:

```bash
cd /srv/stacks/apps/agendador
cp agendador.env.example agendador.env
nano agendador.env
chmod 600 agendador.env
```

Preencha no arquivo real:

```text
AccessProtection__Username=...
AccessProtection__Password=...
Telegram__BotToken=...
Telegram__ChatId=...
```

Nunca commitar nem enviar o arquivo `agendador.env` para GitHub.

Tambem nao use `appsettings.Production.json` para segredos reais. Configuracoes sensiveis devem ficar no arquivo `.env` real do servidor ou em variaveis de ambiente, nunca no Git.

## Validar compose

No servidor:

```bash
cd /srv/stacks/apps/agendador
docker compose config
```

## Build e subida

No servidor:

```bash
cd /srv/stacks/apps/agendador
docker compose build
docker compose up -d
```

## Verificar funcionamento

No servidor:

```bash
docker compose ps
docker compose logs -f agendador-contas
curl http://127.0.0.1:5005/health
```

O container tambem possui healthcheck interno configurado para chamar:

```text
http://127.0.0.1:5005/health
```

Esse endpoint retorna apenas `{"status":"ok"}` e nao deve expor dados sensiveis.

De outro container na rede `proxy`, o alvo interno e:

```text
http://agendador-contas:5005
```

## Porta local

O compose publica a porta apenas no loopback do servidor:

```yaml
ports:
  - "127.0.0.1:5005:5005"
```

Isso permite teste local no servidor sem expor diretamente a porta `5005` na rede publica. Para acesso externo, use Nginx Proxy Manager na rede `proxy`.

## Nginx Proxy Manager

No Nginx Proxy Manager, configure o Proxy Host apontando para:

```text
Scheme: http
Forward Hostname / IP: agendador-contas
Forward Port: 5005
```

Ative SSL/HTTPS antes de expor fora da rede local.

## Atualizar uma versao futura

No servidor:

```bash
cd /srv/apps/agendador
git pull

cd /srv/stacks/apps/agendador
docker compose build
docker compose up -d
docker compose logs -n 80 agendador-contas
curl http://127.0.0.1:5005/health
```

## Checklist antes de expor fora da rede local

- Confirmar `AccessProtection__Enabled=true`.
- Confirmar senha forte no arquivo `/srv/stacks/apps/agendador/agendador.env`.
- Confirmar Telegram funcionando em `Production`.
- Confirmar backup automatico criando arquivos em `/srv/data/apps/agendador/backups`.
- Confirmar que `docker compose ps` mostra o container em execucao.
- Confirmar que `/health` retorna apenas status operacional minimo.
- Configurar HTTPS no Nginx Proxy Manager.
- Evitar expor `0.0.0.0:5005` diretamente no servidor.
