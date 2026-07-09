# Agendador de Contas

Aplicacao web simples para cadastrar contas, controlar vencimentos mensais e enviar um lembrete diario pela manha via Telegram.

## Funcionalidades

- Cadastro de conta com nome, valor, dia de vencimento, data de inicio e duracao em meses.
- Duracao `0` significa conta sem fim definido.
- Lista de vencimentos do mes.
- Marcacao de pagamento por mes.
- Pausar, reativar, editar e excluir contas.
- Servico em segundo plano que envia mensagem diaria no horario configurado.
- Armazenamento em JSON em `data/contas.json`, sem banco externo.

## Rodar localmente

```powershell
dotnet run --urls http://localhost:5005
```

Depois acesse:

```text
http://localhost:5005
```

## Configurar Telegram

Edite `appsettings.json` ou use variaveis de ambiente:

```json
{
  "Reminder": {
    "Hour": 8,
    "Minute": 0,
    "TimeZoneId": "Europe/Lisbon"
  },
  "Telegram": {
    "BotToken": "TOKEN_DO_BOT",
    "ChatId": "ID_DO_CHAT"
  }
}
```

Para criar o bot, fale com `@BotFather` no Telegram. Para descobrir o `ChatId`, envie uma mensagem para o bot e acesse:

```text
https://api.telegram.org/botTOKEN_DO_BOT/getUpdates
```

## Publicar para Raspberry Pi

No Windows, gere a publicacao para Linux ARM64:

```powershell
dotnet publish -c Release -r linux-arm64 --self-contained false -o publish
```

Copie a pasta `publish` para o Raspberry Pi, por exemplo:

```bash
scp -r publish pi@IP_DO_RASPBERRY:/home/pi/agendador-contas
```

No Raspberry Pi, instale o runtime do ASP.NET Core 8 e teste:

```bash
cd /home/pi/agendador-contas
dotnet AgendadorContas.dll --urls http://0.0.0.0:5005
```

## Rodar 24/7 como servico

Crie `/etc/systemd/system/agendador-contas.service`:

```ini
[Unit]
Description=Agendador de Contas
After=network-online.target
Wants=network-online.target

[Service]
WorkingDirectory=/home/pi/agendador-contas
ExecStart=/usr/bin/dotnet /home/pi/agendador-contas/AgendadorContas.dll --urls http://0.0.0.0:5005
Restart=always
RestartSec=10
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=Telegram__BotToken=TOKEN_DO_BOT
Environment=Telegram__ChatId=ID_DO_CHAT
Environment=Reminder__Hour=8
Environment=Reminder__Minute=0
Environment=Reminder__TimeZoneId=Europe/Lisbon

[Install]
WantedBy=multi-user.target
```

Ative:

```bash
sudo systemctl daemon-reload
sudo systemctl enable agendador-contas
sudo systemctl start agendador-contas
sudo systemctl status agendador-contas
```

Logs:

```bash
journalctl -u agendador-contas -f
```
