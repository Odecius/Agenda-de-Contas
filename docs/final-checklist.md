# Checklist final

## Estado do projeto

O Agendador de Contas esta pronto para uso local e preparado para deploy futuro em Raspberry Pi. A validacao em hardware real continua pendente porque o Raspberry ainda nao esta disponivel.

## Validacao local

```powershell
dotnet build
dotnet run --project tests\AgendadorContas.Tests\AgendadorContas.Tests.csproj
```

Para verificar a aplicacao em desenvolvimento:

```powershell
dotnet run --urls http://localhost:5005
```

Depois acesse:

```text
http://localhost:5005/
http://localhost:5005/health
```

## Configuracao obrigatoria antes de usar em producao

- Configurar `Telegram__BotToken` e `Telegram__ChatId` por variaveis de ambiente.
- Configurar `AccessProtection__Enabled=true`.
- Configurar `AccessProtection__Username` e `AccessProtection__Password`.
- Confirmar `Data__FilePath=/var/lib/agendador-contas/contas.json`.
- Confirmar `Backup__AutomaticEnabled=true` se quiser backup automatico.
- Confirmar timezone final em `Reminder__TimeZoneId` e `Backup__TimeZoneId`.

## Pontos que dependem do Raspberry

- Confirmar arquitetura `linux-arm64` ou `linux-arm`.
- Confirmar .NET Runtime 8 instalado.
- Confirmar acesso pela rede local a partir de outro aparelho.
- Confirmar login em producao.
- Confirmar envio Telegram em producao.
- Confirmar criacao de backup automatico em `/var/lib/agendador-contas/backups`.
- Confirmar reinicio automatico via `systemd`.

## Segurança

- Nao commitar `notas.txt`, arquivos `.env`, arquivos em `data/` ou segredos.
- Manter o token Telegram apenas em User Secrets no desenvolvimento ou variaveis de ambiente em producao.
- Usar HTTPS/reverse proxy se o sistema for exposto fora da rede local.
- Revogar e trocar qualquer token que apareca em print, chat, commit ou arquivo compartilhado.
