# Deployment Linux com Docker

Este documento registra o modelo publico de deployment do Agendador de Contas. A instalacao validada usa Docker Compose em um servidor Linux, reverse proxy e armazenamento persistente fora do container.

Detalhes reais do host, nomes de dispositivos, caminhos, topologia Docker, portas administrativas, destinos de backup e configuracao sensivel pertencem exclusivamente a documentacao privada de infraestrutura.

## Estado conhecido

- A persistencia atual usa JSON, com `contas.json` como arquivo principal.
- Configuracao operacional, backups e chaves ASP.NET Data Protection sobrevivem a recriacao do container.
- AccessProtection, sessoes persistidas, backups automaticos e health check estao ativos.
- O timezone e fornecido externamente por um identificador IANA.
- O baseline historico desta implantacao e a tag `v1.0.4`, commit `e06d30e`.

## Componentes publicos

- `Dockerfile`: build multi-stage, runtime ASP.NET Core 8 e usuario nao-root.
- `.dockerignore`: impede inclusao de dados locais, configuracao sensivel e artefatos temporarios.
- `deploy/docker-compose.hp.yml`: modelo parametrizado de orquestracao, persistencia, rede externa e healthcheck.
- Arquivos `*.example`: documentam apenas a estrutura de configuracao, sem valores reais.
- Modelo systemd: alternativa preservada para ambientes sem Docker.

## Principios operacionais

- Aplicacao, configuracao e dados persistentes devem permanecer separados.
- Configuracao sensivel deve ser fornecida externamente e nunca versionada.
- A porta interna da aplicacao deve ser acessada pelo reverse proxy, sem exposicao direta desnecessaria.
- O volume de dados deve ter privilegio minimo e permanecer fora do filesystem efemero do container.
- Atualizacoes devem registrar a versao implantada e preservar um caminho de rollback.
- Backups devem ser replicados para um destino fora do host e ter restauracao testada.
- O healthcheck deve retornar somente estado operacional minimo.

## Limite deste documento

Este repositorio explica arquitetura, requisitos e resultado do deployment; ele nao e um dump operacional do servidor. O runbook executavel, inventario e valores reais ficam na documentacao privada controlada.
