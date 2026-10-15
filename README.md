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

## Unit Testing and Mocking (Gayuth)

Tracked in issue #34: https://github.com/Team-Sellora/sellora-organization/issues/34

### At a glance

Project: tests/Sellora.CoreService.UnitTests. Folders mirror the production namespaces (Api/..., Infrastructure/...).
Tools and versions: .NET SDK 8.0.403, xUnit 2.5.3, xunit.runner.visualstudio 2.5.3, Microsoft.NET.Test.Sdk 17.8.0, FluentAssertions 8.10.0, Moq 4.21.0, coverlet.collector 6.0.0.
Category traits: every unit test class carries [Trait("Category", "Unit")] and every integration test class carries [Trait("Category", "Integration")], so each layer can be run on its own.
Database or Docker required for unit tests: no. Proven by running the unit suite with Docker Desktop quit.

Run the unit tests (no database, no Docker, no Kafka):

```bash
dotnet test --filter "Category=Unit"
```

Run the integration tests (needs Docker for the Testcontainers PostgreSQL, and a Kafka broker on localhost:9092):

```bash
ORGANIZATION_DB_PASSWORD=unused-for-kafka docker compose up -d kafka
dotnet test --filter "Category=Integration"
```

### Scope

Stories: Sprint 1 only, US-E-0-4 and US-E-1-2 to US-E-1-8 (E0 platform, JWT validation, role policies and tenancy; E1 provinces, area managers, agencies, territories, territory-agency assignment, shops, sales-rep assignment, hierarchy events and the outbox relay).
Out of scope: SCIM and WSO2 identity provisioning, staff provisioning, /api/me/scope, Kafka SASL, E3 stock and other Sellora services. Existing tests for these were only moved or tidied, not extended.
Rule kept throughout: DbContext and DbSet are never mocked, and neither EF InMemory nor SQLite is used as a stand-in. Code whose logic is the query itself stays covered by the integration tests.

### Test design approach

Inputs: input space partitioning. For each method under test the class comment lists the characteristics, their blocks (disjoint and complete), the boundary values and the coverage criterion.
Criteria: Base Choice where there are several characteristics (1 + sum of (blocks - 1) cases instead of the product for All Combinations), Each Choice for a single characteristic, and Boundary Value Analysis with both sides of every numeric limit (outbox error truncation at 2000 and 2001; page size 1 and 100 here, with 0 and 101 already in ControllerOutcomeMappingTests).
Theory data: InlineData with primitives and enums so each row is readable in the runner output; TheoryData where an expected value is a Guid, a nullable Guid or a timestamp. Every row has a trailing comment naming its block or boundary.
Style: Method_Scenario_ExpectedResult names, Arrange / Act / Assert comments, one behaviour per test, no branching in test methods, exact-value assertions, the instance under test named _sut.
Assertion quality: each production line under test was mutated by hand (40 mutants, then reverted). 36 were killed at first. One survivor showed a weak assertion in HttpCorrelationIdAccessorTests, which was sharpened; the others are listed as known limitations in the PR.

### Where each test double is used

Dummy: Strict Moq dummies for constructor dependencies a test never uses: IProvinceReadService in Api/Controllers/ProvincesControllerTests.cs, and the registration, deactivation, assignment and update services in Api/Controllers/ListPagingBoundaryTests.cs. Strict makes them fail loudly if production code ever calls them.
Stub: Moq stubs that only supply input: IHttpContextAccessor in Api/Tenancy/HttpTenantContextTests.cs and Api/Identity/HttpCurrentUserContextTests.cs, ICorrelationIdAccessor in Infrastructure/Outbox/HierarchyEventFactoryTests.cs, and IProvinceAssignmentService in the outcome mapping Theory of ProvincesControllerTests.cs.
Spy: a hand-written RequestDelegate in Api/Middleware/CorrelationIdMiddlewareTests.cs that records how often it ran and which correlation ID it saw, checked after the call.
Mock: Moq Verify on IProvinceAssignmentService (exact request, Times.Once, and VerifyNoOtherCalls on the invalid-body rejection path) in ProvincesControllerTests.cs, and on each read service in ListPagingBoundaryTests.cs (page size forwarded unchanged).
Fake: a field-backed FakeHttpContextAccessor in Api/Outbox/HttpCorrelationIdAccessorTests.cs, a working implementation with a production-unsafe shortcut (no AsyncLocal).
No doubles: the pure-logic classes for the constraint matchers, outbox truncation and the province scope decision, and RolePoliciesTests, which uses the real DefaultAuthorizationService.

### Production code changes (behaviour unchanged)

H1, visibility only: AgencyRegistrationService.IsUniqueAgencyNameViolation and TerritoryRegistrationService.IsConstraintViolation changed from private to internal. They were already pure; this is not a Humble Object extraction.
H2, Humble Object: OutboxRelayService.TruncateLastError, a pure internal static method ported line by line from the catch block. Proof: OutboxRelayServiceTests and OutboxRelayKafkaIntegrationTests, 8 of 8 passed before and after.
H3, Humble Object: TerritoryAgencyAssignmentService.DecideProvinceScope, a pure internal static method returning the outcome, with the check order and all logging kept in the service. Proof: TerritoryAgencyAssignmentEndpointTests, 7 of 7 passed before and after; TerritoryAgencyOpenWorkGuardTests passed after.
Whole-phase proof: the full integration suite passed 172 of 172 before and after the three changes.
Exposure: InternalsVisibleTo Sellora.CoreService.UnitTests on the Infrastructure project only.

### Prior work

The story-level tests were written by each story's developer in Sprint 1. I separated them into unit and integration projects (moving the genuine unit tests with git mv so their history is kept), consolidated repetitive Facts into Theories (JwtValidationTests, RolePolicyTests and the SCIM options Fact), and extended them with the new unit test classes above.

### Before and after

Before: 198 test methods, 292 executed cases (unit 51 methods and 120 cases, integration 147 methods and 172 cases), 31 Theory methods, all in one project. Unit tests were not separable from the integration run.
After: 215 test methods, 361 executed cases (unit 71 methods and 189 cases, integration 144 methods and 172 cases), 49 Theory methods, in two projects with category traits.
Unit run time: about 0.2 s for 189 cases, with no database, Docker or broker required.
Integration run time: about 25 s for 172 cases (warm), with Docker and Kafka required.
Mocking: before, 4 unit classes used hand-written doubles and the repo had no mocking library; after, Moq is added for stubs, mocks and Strict dummies, and hand-written doubles remain where they are simpler (the spy and the fake).
