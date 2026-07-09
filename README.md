# Agendador de Contas

Aplicacao web em .NET 8 para cadastrar contas, acompanhar vencimentos mensais e enviar lembretes diarios via Telegram.

## Funcionalidades

- Cadastro de conta com nome, valor, dia de vencimento, data de inicio e duracao em meses.
- Duracao `0` significa conta sem fim definido.
- Dashboard com vencimentos de hoje, pendencias do mes e contas ativas.
- Marcacao de pagamento por mes.
- Pausar, reativar, editar e excluir contas.
- Servico em segundo plano que envia mensagem diaria no horario configurado.
- Armazenamento local em JSON em `data/contas.json`, sem banco externo.
- Integracao com Telegram usando `HttpClientFactory` e configuracao segura.

## Rodar localmente

Na pasta do projeto:

```powershell
dotnet run --urls http://localhost:5005
```

Depois acesse:

```text
http://localhost:5005
```

## Configuracao segura do Telegram em desenvolvimento

Nao coloque `BotToken`, `ChatId`, senhas ou chaves em `appsettings.json`.

O `appsettings.json` deve ficar apenas com valores de exemplo:

```json
"Telegram": {
  "Enabled": true,
  "BotToken": "",
  "ChatId": "",
  "ApiBaseUrl": "https://api.telegram.org"
}
```

Use User Secrets no ambiente de desenvolvimento:

```powershell
dotnet user-secrets init
dotnet user-secrets set "Telegram:Enabled" "true"
dotnet user-secrets set "Telegram:BotToken" "SEU_TOKEN"
dotnet user-secrets set "Telegram:ChatId" "SEU_CHAT_ID"
dotnet user-secrets list
```

Para testar o envio no desenvolvimento:

```powershell
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --urls http://localhost:5005
```

Depois acesse:

```text
http://localhost:5005/test-telegram
```

Essa rota existe somente em `Development`. Em `Production`, ela retorna `404`.

## Criar bot e descobrir ChatId

1. No Telegram, fale com `@BotFather`.
2. Envie `/newbot`.
3. Escolha nome e username do bot.
4. Copie o token fornecido pelo BotFather.
5. Envie uma mensagem qualquer para o seu bot.
6. Acesse, trocando `SEU_TOKEN` pelo token real:

```text
https://api.telegram.org/botSEU_TOKEN/getUpdates
```

Procure por:

```json
"chat": {
  "id": 123456789
}
```

Esse valor e o `Telegram:ChatId`.

## Preparar Raspberry Pi

Ainda nao testado em Raspberry Pi real.

Quando tiver o Raspberry, instale o runtime do ASP.NET Core 8. Em Raspberry Pi OS/Debian, consulte a documentacao oficial da Microsoft para instalar o runtime compativel com a versao do sistema.

Depois escolha uma pasta para o app:

```bash
sudo mkdir -p /opt/agendador-contas
sudo chown -R $USER:$USER /opt/agendador-contas
```

## Opcao A: publicar no Windows e copiar para o Raspberry

No Windows:

```powershell
dotnet publish -c Release -r linux-arm64 --self-contained false -o publish
```

Copie para o Raspberry:

```bash
scp -r publish/* pi@IP_DO_RASPBERRY:/opt/agendador-contas/
```

Teste no Raspberry:

```bash
cd /opt/agendador-contas
dotnet AgendadorContas.dll --urls http://0.0.0.0:5005
```

## Opcao B: clonar do GitHub no Raspberry

No Raspberry:

```bash
cd /opt
git clone https://github.com/Odecius/Agenda-de-Contas.git agendador-contas-src
cd agendador-contas-src
dotnet publish -c Release -o /opt/agendador-contas
```

## Configurar variaveis de ambiente no Raspberry

Em producao, use variaveis de ambiente. Nao use User Secrets no Raspberry.

Variaveis necessarias:

```text
ASPNETCORE_ENVIRONMENT=Production
Telegram__Enabled=true
Telegram__BotToken=SEU_TOKEN
Telegram__ChatId=SEU_CHAT_ID
Telegram__ApiBaseUrl=https://api.telegram.org
Reminder__Hour=8
Reminder__Minute=0
Reminder__TimeZoneId=Europe/Lisbon
```

## Rodar 24/7 com systemd

Crie o arquivo:

```bash
sudo nano /etc/systemd/system/agendador-contas.service
```

Conteudo:

```ini
[Unit]
Description=Agendador de Contas
After=network-online.target
Wants=network-online.target

[Service]
WorkingDirectory=/opt/agendador-contas
ExecStart=/usr/bin/dotnet /opt/agendador-contas/AgendadorContas.dll --urls http://0.0.0.0:5005
Restart=always
RestartSec=10
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=Telegram__Enabled=true
Environment=Telegram__BotToken=SEU_TOKEN
Environment=Telegram__ChatId=SEU_CHAT_ID
Environment=Telegram__ApiBaseUrl=https://api.telegram.org
Environment=Reminder__Hour=8
Environment=Reminder__Minute=0
Environment=Reminder__TimeZoneId=Europe/Lisbon

[Install]
WantedBy=multi-user.target
```

Ative e inicie:

```bash
sudo systemctl daemon-reload
sudo systemctl enable agendador-contas
sudo systemctl start agendador-contas
sudo systemctl status agendador-contas
```

Ver logs:

```bash
journalctl -u agendador-contas -f
```

Reiniciar apos alteracoes:

```bash
sudo systemctl restart agendador-contas
```

## Atualizar o app no Raspberry usando GitHub

Se usar a opcao de clone no Raspberry:

```bash
cd /opt/agendador-contas-src
git pull
dotnet publish -c Release -o /opt/agendador-contas
sudo systemctl restart agendador-contas
```

Verifique:

```bash
sudo systemctl status agendador-contas
journalctl -u agendador-contas -n 80
```

## Checklist antes de publicar ou atualizar

- `dotnet build` sem erros.
- `appsettings.json` sem token, senha ou chave.
- `notas.txt` fora do Git.
- `.env` fora do Git.
- `data/` fora do Git.
- `Telegram__BotToken` configurado apenas em User Secrets ou variaveis de ambiente.
- `/test-telegram` disponivel somente em `Development`.

## Comandos uteis

Build:

```powershell
dotnet build
```

Rodar localmente:

```powershell
dotnet run --urls http://localhost:5005
```

Publicar:

```powershell
dotnet publish -c Release -r linux-arm64 --self-contained false -o publish
```
