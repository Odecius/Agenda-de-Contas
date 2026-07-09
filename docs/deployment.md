# Deployment

## PublicaÃ§Ã£o para Raspberry Pi

```powershell
dotnet publish -c Release -r linux-arm64 --self-contained false -o publish
```

Copie os arquivos para o Raspberry e execute:

```bash
dotnet AgendadorContas.dll --urls http://0.0.0.0:5005
```

## VariÃ¡veis de ambiente

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

## systemd

Rodar como serviÃ§o systemd em produÃ§Ã£o, com restart automÃ¡tico, logs via `journalctl` e segredos definidos por ambiente ou arquivo seguro fora do Git.

## PendÃªncia

Validar o procedimento em Raspberry Pi real.
