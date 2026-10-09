## Platform overview

Sellora follows a service-oriented architecture. Each backend service owns a specific business capability and follows shared engineering conventions for authentication, authorization, data isolation, logging, health checks, and error handling.

### Core principles

- **Secure by default** — JWT bearer authentication and role-based authorization protect service endpoints.
- **Tenant isolation** — company data is isolated at the data-access layer.
- **Clear service boundaries** — business capabilities are separated into focused services.
- **Observable operations** — structured logs, correlation IDs, health checks, and consistent error responses simplify diagnosis.
- **Consistent development** — shared templates and conventions make services easier to create, test, and maintain.

## Technology

The current backend foundation uses:

- .NET 8 and ASP.NET Core
- Entity Framework Core
- JWT bearer authentication
- Role-based authorization policies
- Serilog structured logging
- Problem Details error responses
- Automated unit and integration tests
- Docker-based local infrastructure as the platform evolves

## Architecture

Backend services follow Clean Architecture:

```text
Domain
  Core business entities and rules

Application
  Use cases, contracts, and application logic

Infrastructure
  Persistence and external integrations

API
  HTTP endpoints, authentication, middleware, and configuration

Tests
  Unit and integration tests for security and application behaviour
```

Requests pass through the platform approximately as follows:

```text
Client
  -> API gateway
  -> Authentication and authorization
  -> Tenant context
  -> Application service
  -> Tenant-filtered persistence
```

## Security model

Sellora services validate a token's issuer, audience, lifetime, and signature. Named authorization policies restrict protected operations to the appropriate roles.

Tenant identity is derived from authenticated claims and applied through the persistence layer. Client-provided identifiers must not be trusted as the authority for tenant access.

Security-sensitive behaviour is covered by integration tests, including:

- Missing, expired, or invalid tokens returning `401 Unauthorized`
- Authenticated users without the required role receiving `403 Forbidden`
- Users retrieving only records belonging to their company
- Attempts to access another company's records being rejected or filtered

## Local development

Each repository contains its own setup instructions. A typical .NET service can be restored, built, and tested with:

```bash
dotnet restore
dotnet build
dotnet test
```

Configuration should be supplied through local development settings, environment variables, or a secret manager. Do not commit credentials, signing keys, connection strings, or other secrets.

### Local database password

The local Postgres password is not committed. Set it once:

```bash
cp .env.example .env          # docker compose reads ORGANIZATION_DB_PASSWORD from here
docker compose up -d

# Give the API the same password (stored outside the repo by user-secrets).
dotnet user-secrets set "ConnectionStrings:Default" \
  "Host=localhost;Port=5433;Database=organization_db;Username=sellora;Password=<ORGANIZATION_DB_PASSWORD>" \
  --project src/Sellora.CoreService.Api
```

Alternatively set `PGPASSWORD` in your shell; Npgsql uses it when the connection string has no password.
