# Timesheets App

Aplicação de timesheets em ASP.NET Core MVC com PostgreSQL.

## Stack

- .NET 8
- ASP.NET Core MVC
- Entity Framework Core
- PostgreSQL 16

## Como correr

1. Arranca a base de dados:

```bash
docker compose up -d
```

2. Executa a aplicação:

```bash
dotnet run --project Timesheets.Web
```

3. Abre o URL indicado no terminal.

## O que já inclui

- Dashboard com métricas da semana e do mês
- Botão simples "Novo registo" no dashboard e na navegação
- Gestão de colaboradores
- Gestão de projetos
- Registo, edição, filtro e remoção de horas
- Seed inicial para veres a app com dados logo no arranque

## Base de dados

A app aplica migrations automaticamente no startup. A connection string por defeito está em [appsettings.json](/Users/antonyferreira/Documents/New project/Timesheets.Web/appsettings.json).
