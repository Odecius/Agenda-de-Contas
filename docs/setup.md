# Setup

## Pré-requisitos

- .NET SDK 8.
- Opcional: bot Telegram para testar notificações.

## Instalação

```powershell
cd "C:\Projetos\Abc\Agendador de contas"
dotnet restore
```

## Configurar Telegram em desenvolvimento

```powershell
dotnet user-secrets init
dotnet user-secrets set "Telegram:Enabled" "true"
dotnet user-secrets set "Telegram:BotToken" "SEU_TOKEN"
dotnet user-secrets set "Telegram:ChatId" "SEU_CHAT_ID"
dotnet user-secrets set "Telegram:ApiBaseUrl" "https://api.telegram.org"
```

## Executar

```powershell
dotnet run --urls http://localhost:5005
```

Abra `http://localhost:5005`.
