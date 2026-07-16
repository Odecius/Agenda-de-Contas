# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY AgendadorContas.csproj ./
RUN dotnet restore ./AgendadorContas.csproj

COPY . .
RUN dotnet publish ./AgendadorContas.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://0.0.0.0:5005 \
    DOTNET_EnableDiagnostics=0

EXPOSE 5005

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /var/lib/agendador-contas \
    && chown -R app:app /app /var/lib/agendador-contas

COPY --from=build --chown=app:app /app/publish .

USER app

ENTRYPOINT ["dotnet", "AgendadorContas.dll"]
