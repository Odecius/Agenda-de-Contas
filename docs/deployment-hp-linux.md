# Deployment HP Linux

Este guia prepara o Agendador de Contas para rodar 24/7 no servidor HP Linux antigo, usando .NET 8, variaveis de ambiente e `systemd`.

> Status: preparado para validacao no servidor HP. O deploy real ainda depende de acesso ao servidor, instalacao do .NET Runtime 8 e configuracao das variaveis de ambiente reais.

## Premissas

- Servidor HP com Linux 64-bit x64.
- Acesso SSH ao servidor.
- .NET Runtime 8 instalado no servidor.
- Usuario Linux dedicado chamado `agendador`.
- Aplicacao instalada em `/opt/agendador-contas`.
- Dados locais salvos em `/var/lib/agendador-contas/contas.json`.
- Segredos em `/etc/agendador-contas/agendador-contas.env`, fora do Git.
- Acesso inicial apenas pela rede local.

## Confirmar arquitetura do servidor

No servidor HP:

```bash
uname -m
```

Valores esperados:

- `x86_64`: usar publish `linux-x64`.
- `aarch64`: usar publish `linux-arm64`.

Para o laptop HP antigo, o mais provavel e `x86_64`.

## Publicar no computador de desenvolvimento

No Windows, dentro do projeto:

```powershell
cd "C:\Projetos\Abc\Agendador de contas"
dotnet publish -c Release -r linux-x64 --self-contained false -o "..\publish\agendador-contas-linux-x64"
```

Esse comando gera uma publicacao dependente do runtime. Portanto, o servidor precisa ter o .NET Runtime 8 instalado.

## Preparar o servidor HP

No servidor:

```bash
sudo adduser --system --group --home /opt/agendador-contas agendador
sudo mkdir -p /opt/agendador-contas
sudo mkdir -p /var/lib/agendador-contas
sudo mkdir -p /etc/agendador-contas
sudo chown -R agendador:agendador /opt/agendador-contas /var/lib/agendador-contas
sudo chmod 750 /etc/agendador-contas
```

## Copiar arquivos publicados

No Windows, ajuste `USUARIO` e `IP_DO_SERVIDOR_HP`:

```powershell
scp -r "..\publish\agendador-contas-linux-x64\*" USUARIO@IP_DO_SERVIDOR_HP:/tmp/agendador-contas/
scp "deploy\agendador-contas.service" USUARIO@IP_DO_SERVIDOR_HP:/tmp/agendador-contas.service
```

No servidor:

```bash
sudo rsync -a --delete /tmp/agendador-contas/ /opt/agendador-contas/
sudo chown -R agendador:agendador /opt/agendador-contas
sudo cp /tmp/agendador-contas.service /etc/systemd/system/agendador-contas.service
```

## Configurar ambiente sem segredos no Git

Use `deploy/agendador-contas.env.example` como referencia. Crie o arquivo real no servidor:

```bash
sudo nano /etc/agendador-contas/agendador-contas.env
sudo chown root:agendador /etc/agendador-contas/agendador-contas.env
sudo chmod 640 /etc/agendador-contas/agendador-contas.env
```

Exemplo:

```text
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://0.0.0.0:5005

Data__FilePath=/var/lib/agendador-contas/contas.json

AccessProtection__Enabled=true
AccessProtection__Username=admin
AccessProtection__Password=SENHA_FORTE
AccessProtection__SessionHours=12

Reminder__Hour=8
Reminder__Minute=0
Reminder__TimeZoneId=Europe/London

Backup__AutomaticEnabled=true
Backup__Hour=2
Backup__Minute=0
Backup__TimeZoneId=Europe/London
Backup__RetentionDays=30
Backup__MinimumBackupsToKeep=10
Backup__RunOnStartup=false

Telegram__Enabled=true
Telegram__BotToken=SEU_TOKEN
Telegram__ChatId=SEU_CHAT_ID
Telegram__ApiBaseUrl=https://api.telegram.org
```

Nunca commitar o arquivo real de ambiente.

## Instalar e iniciar o servico

No servidor:

```bash
sudo systemctl daemon-reload
sudo systemctl enable agendador-contas
sudo systemctl start agendador-contas
sudo systemctl status agendador-contas
```

## Verificar funcionamento

No servidor:

```bash
curl http://localhost:5005/health
curl -I http://localhost:5005/
journalctl -u agendador-contas -n 80 --no-pager
```

De outro aparelho na mesma rede:

```text
http://IP_DO_SERVIDOR_HP:5005
```

## Atualizar uma versao futura

1. Publicar novamente no Windows.
2. Copiar os arquivos para `/tmp/agendador-contas/`.
3. Parar o servico.
4. Sincronizar `/opt/agendador-contas`.
5. Iniciar e validar.

```bash
sudo systemctl stop agendador-contas
sudo rsync -a --delete /tmp/agendador-contas/ /opt/agendador-contas/
sudo chown -R agendador:agendador /opt/agendador-contas
sudo systemctl start agendador-contas
sudo systemctl status agendador-contas
curl http://localhost:5005/health
```

## Checklist antes de expor fora da rede local

- Confirmar login ativo com `AccessProtection__Enabled=true`.
- Confirmar senha forte no arquivo de ambiente.
- Confirmar Telegram funcionando em `Production`.
- Confirmar backup automatico criando arquivos em `/var/lib/agendador-contas/backups`.
- Confirmar reinicio automatico apos reboot.
- Confirmar firewall local.
- Configurar HTTPS e reverse proxy antes de acesso pela internet.
- Evitar expor a porta `5005` diretamente na internet.
