# Agendador de Contas

AplicaÃ§Ã£o web em .NET 8 para cadastrar contas, acompanhar vencimentos mensais, marcar pagamentos e enviar lembretes diÃ¡rios via Telegram.

## Objetivo do projeto

Controlar contas recorrentes ou com duraÃ§Ã£o definida, mostrando vencimentos por mÃªs e enviando alertas automÃ¡ticos para evitar atrasos. O projeto foi pensado para rodar localmente e, futuramente, 24/7 em Raspberry Pi.

## Tecnologias utilizadas

- .NET 8 / ASP.NET Core Minimal API.
- Hosted Service para lembretes diÃ¡rios.
- HTML, CSS e JavaScript em `wwwroot`.
- Armazenamento local em JSON via `ContaStore`.
- Telegram Bot API via `HttpClientFactory`.
- Options Pattern, validaÃ§Ã£o de configuraÃ§Ã£o e User Secrets em desenvolvimento.

## Estrutura do projeto

```text
.
â”œâ”€â”€ Program.cs
â”œâ”€â”€ AgendadorContas.csproj
â”œâ”€â”€ Models/
â”œâ”€â”€ Options/
â”œâ”€â”€ Services/
â”œâ”€â”€ Properties/
â”œâ”€â”€ wwwroot/
â”‚   â”œâ”€â”€ index.html
â”‚   â”œâ”€â”€ app.js
â”‚   â”œâ”€â”€ styles.css
â”‚   â””â”€â”€ assets/
â””â”€â”€ docs/
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
```

Em desenvolvimento, com User Secrets configurado:

```powershell
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --urls http://localhost:5005
```

Depois acesse `http://localhost:5005/test-telegram`.

## Como fazer deploy

O caminho planejado Ã© publicar para Linux/Raspberry Pi:

```powershell
dotnet publish -c Release -r linux-arm64 --self-contained false -o publish
```

No Raspberry, rodar com `systemd`, variÃ¡veis de ambiente e `ASPNETCORE_ENVIRONMENT=Production`.

## Status atual

AplicaÃ§Ã£o funcional com cadastro, listagem, ediÃ§Ã£o, exclusÃ£o, pausa/reativaÃ§Ã£o, vencimentos, marcaÃ§Ã£o de pagamentos, interface responsiva, resumo mensal, suporte inicial a paÃ­s/moeda por conta e envio Telegram. NÃ£o hÃ¡ banco externo nem autenticaÃ§Ã£o. A documentaÃ§Ã£o de Raspberry existe, mas o deploy real ainda precisa ser validado em hardware.

## Paises e moedas

Cada conta possui um pais e uma moeda. Inicialmente, o projeto suporta:

- United Kingdom / GBP
- Portugal / EUR
- Brazil / BRL

Novas contas usam `UnitedKingdom` e `GBP` como padrao. Contas antigas salvas sem esses campos tambem assumem esses valores ao serem carregadas.

O sistema ainda nao faz conversao cambial. Totais com moedas diferentes sao apresentados agrupados por moeda para evitar soma incorreta entre GBP, EUR e BRL.

## PrÃ³ximos passos

- Preparar autenticaÃ§Ã£o simples antes de expor na rede.
- Validar deploy real em Raspberry Pi.
- Avaliar conversao cambial futura com API externa.
- Criar dashboard por pais e relatÃ³rios por moeda.

## SeguranÃ§a

Segredos do Telegram devem ficar fora do Git, em User Secrets no desenvolvimento e variÃ¡veis de ambiente em produÃ§Ã£o. O arquivo `notas.txt` contÃ©m histÃ³rico sensÃ­vel e deve ser limpo/removido do histÃ³rico antes de compartilhar o repositÃ³rio.
