# Deployment Raspberry Pi

Este guia prepara o Agendador de Contas para rodar 24/7 em Raspberry Pi usando .NET 8, variaveis de ambiente e `systemd`.

> Status: ainda nao validado em Raspberry real. Os comandos abaixo foram preparados para uso futuro e precisam ser confirmados no hardware quando ele estiver disponivel.
> Para o servidor HP Linux x64, use `docs/deployment-hp-linux.md`.

## Premissas

- Raspberry Pi com sistema Linux 64-bit.
- .NET Runtime 8 instalado no Raspberry.
- Usuario Linux dedicado chamado `agendador`.
- Aplicacao instalada em `/opt/agendador-contas`.
- Dados locais salvos em `/var/lib/agendador-contas/contas.json`.
- Segredos em `/etc/agendador-contas/agendador-contas.env`, fora do Git.

## Publicar no computador de desenvolvimento

No Windows, dentro do projeto:

```powershell
cd "C:\Projetos\Abc\Agendador de contas"
dotnet publish -c Release -r linux-arm64 --self-contained false -o "..\publish\agendador-contas-linux-arm64"
```

Use `linux-arm64` para Raspberry Pi OS 64-bit. Se estiver usando um sistema 32-bit, avaliar `linux-arm`.

## Preparar o Raspberry

```bash
sudo adduser --system --group --home /opt/agendador-contas agendador
sudo mkdir -p /opt/agendador-contas
sudo mkdir -p /var/lib/agendador-contas
sudo mkdir -p /etc/agendador-contas
sudo chown -R agendador:agendador /opt/agendador-contas /var/lib/agendador-contas
sudo chmod 750 /etc/agendador-contas
```

## Copiar arquivos publicados

Exemplo usando `scp` a partir do computador de desenvolvimento:

```powershell
scp -r "..\publish\agendador-contas-linux-arm64\*" pi@IP_DO_RASPBERRY:/tmp/agendador-contas/
```

No Raspberry:

```bash
sudo rsync -a --delete /tmp/agendador-contas/ /opt/agendador-contas/
sudo chown -R agendador:agendador /opt/agendador-contas
```

## Configurar variaveis de ambiente

Use `deploy/agendador-contas.env.example` como referencia. Crie o arquivo real no Raspberry:

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

Nao versionar o arquivo real de ambiente.

## Instalar systemd

Copie o modelo `deploy/agendador-contas.service` do repositorio para o Raspberry e instale:

```bash
sudo cp deploy/agendador-contas.service /etc/systemd/system/agendador-contas.service
sudo systemctl daemon-reload
sudo systemctl enable agendador-contas
sudo systemctl start agendador-contas
```

Se o arquivo `deploy/agendador-contas.service` for copiado manualmente, confira se `WorkingDirectory`, `EnvironmentFile`, `ExecStart` e `ReadWritePaths` batem com os caminhos usados no Raspberry.

## Verificar funcionamento

```bash
systemctl status agendador-contas
journalctl -u agendador-contas -f
curl http://localhost:5005/
curl http://localhost:5005/health
curl http://localhost:5005/api/contas
```

De outro aparelho na mesma rede:

```text
http://IP_DO_RASPBERRY:5005
```

## Atualizar uma versao futura

1. Publicar novamente no computador de desenvolvimento.
2. Copiar os arquivos para `/tmp/agendador-contas/`.
3. Parar o servico.
4. Sincronizar `/opt/agendador-contas`.
5. Iniciar o servico.

```bash
sudo systemctl stop agendador-contas
sudo rsync -a --delete /tmp/agendador-contas/ /opt/agendador-contas/
sudo chown -R agendador:agendador /opt/agendador-contas
sudo systemctl start agendador-contas
sudo systemctl status agendador-contas
```

## Backup minimo dos dados

O arquivo principal fica em:

```text
/var/lib/agendador-contas/contas.json
```

Backups manuais criados pela interface, backups automaticos e backups `pre-restore` ficam em:

```text
/var/lib/agendador-contas/backups/
```

Para ativar backup automatico no Raspberry, configure no arquivo de ambiente:

```text
Backup__AutomaticEnabled=true
Backup__Hour=2
Backup__Minute=0
Backup__TimeZoneId=Europe/London
Backup__RetentionDays=30
Backup__MinimumBackupsToKeep=10
```

A retencao automatica remove apenas arquivos `contas.auto.*.json` antigos. Backups manuais e `pre-restore` nao entram nessa limpeza.

Backup manual:

```bash
sudo cp /var/lib/agendador-contas/contas.json /var/lib/agendador-contas/contas.json.$(date +%Y%m%d%H%M%S).bak
```

## Checklist pendente para hardware real

- Confirmar arquitetura do Raspberry: `linux-arm64` ou `linux-arm`.
- Confirmar instalacao do .NET Runtime 8.
- Confirmar acesso pela rede local em outro aparelho.
- Confirmar `/health` retornando `status=ok`.
- Confirmar login com `AccessProtection__Enabled=true`.
- Confirmar envio real de Telegram em `Production`.
- Confirmar timezone final (`Europe/London`, `Europe/Lisbon` ou outro).
- Confirmar criacao de backup automatico e retencao em `/var/lib/agendador-contas/backups`.
- Confirmar reinicio automatico apos reboot.
- Confirmar permissao de escrita em `/var/lib/agendador-contas`.
