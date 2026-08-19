# Checklist final

## Estado do projeto

O Agendador de Contas esta pronto para uso local e preparado para deploy Docker em Linux. O modelo Docker Compose foi validado em producao; Raspberry Pi continua como alvo futuro quando o hardware estiver disponivel.

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
- Confirmar `DataProtection__KeysPath=/var/lib/agendador-contas/dataprotection-keys`.
- Confirmar `Backup__AutomaticEnabled=true` se quiser backup automatico.
- Confirmar timezone final em `Reminder__TimeZoneId` e `Backup__TimeZoneId`.
- Confirmar na interface o horario desejado do lembrete diario.

## Pontos que dependem do servidor Linux

- Confirmar arquitetura com `uname -m`; normalmente `x86_64`.
- Confirmar Docker e Docker Compose instalados.
- Confirmar rede Docker externa gerenciada pelo reverse proxy.
- Confirmar separacao entre aplicacao, configuracao e dados persistentes.
- Confirmar que o Compose e o ambiente real ficam fora do repositorio publico.
- Confirmar acesso pela rede local a partir de outro aparelho.
- Confirmar login em producao.
- Confirmar envio Telegram em producao.
- Confirmar que alterar o horario do lembrete atualiza `settings.json` no volume persistente.
- Confirmar criacao de backup automatico no armazenamento persistente.
- Confirmar persistencia das chaves Data Protection fora do container.
- Confirmar reinicio automatico via `restart: unless-stopped`.

## Pontos futuros que dependem do Raspberry

- Confirmar arquitetura `linux-arm64` ou `linux-arm`.
- Confirmar .NET Runtime 8 instalado.
- Confirmar acesso pela rede local a partir de outro aparelho.
- Confirmar login em producao.
- Confirmar envio Telegram em producao.
- Confirmar que alterar o horario do lembrete cria/atualiza `/var/lib/agendador-contas/settings.json`.
- Confirmar criacao de backup automatico em `/var/lib/agendador-contas/backups`.
- Confirmar reinicio automatico via `systemd`.

## Segurança

- Nao commitar `notas.txt`, arquivos `.env`, arquivos em `data/` ou segredos.
- Manter o token Telegram apenas em User Secrets no desenvolvimento ou variaveis de ambiente em producao.
- Usar HTTPS/reverse proxy se o sistema for exposto fora da rede local.
- Revogar e trocar qualquer token que apareca em print, chat, commit ou arquivo compartilhado.
