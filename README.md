# Agendador de Contas

Aplicação web em .NET 8 para cadastrar contas, acompanhar vencimentos mensais, marcar pagamentos e enviar lembretes diários via Telegram.

## Objetivo do projeto

Controlar contas recorrentes ou com duração definida, mostrando vencimentos por mês e enviando alertas automáticos para evitar atrasos. O projeto foi pensado para rodar localmente e, futuramente, 24/7 em Raspberry Pi.

## Tecnologias utilizadas

- .NET 8 / ASP.NET Core Minimal API.
- Hosted Service para lembretes diários.
- HTML, CSS e JavaScript em `wwwroot`.
- Armazenamento local em JSON via `ContaStore`.
- Telegram Bot API via `HttpClientFactory`.
- Options Pattern, validação de configuração e User Secrets em desenvolvimento.
- Protecao de acesso opcional por cookie.

## Estrutura do projeto

```text
.
├── Program.cs
├── AgendadorContas.csproj
├── Models/
├── Options/
├── Services/
├── Properties/
├── wwwroot/
│   ├── index.html
│   ├── app.js
│   ├── styles.css
│   └── assets/
└── docs/
```

## Como executar

```powershell
cd "C:\Projetos\Abc\Agendador de contas"
dotnet run --urls http://localhost:5005
```

Acesse `http://localhost:5005`.

## Como testar

```powershell
dotnet build
dotnet run --project tests\AgendadorContas.Tests\AgendadorContas.Tests.csproj
```

Em desenvolvimento, com User Secrets configurado:

```powershell
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --urls http://localhost:5005
```

Depois acesse `http://localhost:5005/test-telegram`.

## Como fazer deploy

O caminho planejado e publicar para Linux/Raspberry Pi:

```powershell
dotnet publish -c Release -r linux-arm64 --self-contained false -o "..\publish\agendador-contas-linux-arm64"
```

No Raspberry, rodar com `systemd`, variaveis de ambiente e `ASPNETCORE_ENVIRONMENT=Production`.

Consulte o guia completo em `docs/deployment.md`. A pasta `deploy/` contem modelos de `systemd` e arquivo de ambiente sem segredos reais.

## Status atual

Aplicação funcional com cadastro, listagem, edição, exclusão, pausa/reativação, vencimentos, marcação de pagamentos, backups manuais, interface responsiva, resumo mensal, dashboard por país/moeda, exportação CSV mensal, suporte inicial a país/moeda por conta e envio Telegram. Não há banco externo. O deploy Raspberry esta documentado e preparado com modelos de apoio, mas ainda precisa ser validado em hardware real.

## Backups

O sistema permite criar backups manuais do arquivo de dados local e restaurar um backup pela interface. Antes de restaurar, a aplicação cria automaticamente um backup `pre-restore` dos dados atuais.

Os backups ficam na pasta `backups` ao lado do arquivo configurado em `Data:FilePath`. Como `data/` esta no `.gitignore`, os backups locais não são enviados ao GitHub.

## Paises e moedas

Cada conta possui um pais e uma moeda. Inicialmente, o projeto suporta:

- United Kingdom / GBP
- Portugal / EUR
- Brazil / BRL

Novas contas usam `UnitedKingdom` e `GBP` como padrao. Contas antigas salvas sem esses campos tambem assumem esses valores ao serem carregadas.

O sistema ainda nao faz conversao cambial. Totais com moedas diferentes sao apresentados agrupados por moeda para evitar soma incorreta entre GBP, EUR e BRL. A interface tambem possui um resumo por pais e moeda para o mes selecionado.

## Exportacao CSV

A tela de vencimentos permite exportar um CSV do mes selecionado. O arquivo inclui conta, pais, moeda, valor, valor formatado, status de pagamento e observacoes.

## Próximos passos

- Validar deploy real em Raspberry Pi.
- Avaliar conversao cambial futura com API externa.
- Melhorar relatorios por moeda e pais.

## Segurança

Segredos do Telegram e senha de acesso devem ficar fora do Git, em User Secrets no desenvolvimento e variáveis de ambiente em produção. O arquivo `notas.txt` contém histórico sensível e deve ser limpo/removido do histórico antes de compartilhar o repositório.

Para ativar login em producao:

```text
AccessProtection__Enabled=true
AccessProtection__Username=admin
AccessProtection__Password=SENHA_FORTE
AccessProtection__SessionHours=12
```
