# Sellora Organization Service

`sellora-organization` is the Organization and Hierarchy microservice of Sellora, a multi-tenant field-sales SaaS for FMCG companies built for SE3022. It owns the company hierarchy (provinces, area managers, agencies, territories, shops and sales-rep assignments), publishes hierarchy events to Kafka through a transactional outbox, and carries the shared platform pieces: JWT validation, role policies and tenant isolation.

For SE3112 this repository is the base codebase for the **Unit Testing and Mocking** part of the group's microservice testing pipeline.

## SE3112 Microservice Testing Pipeline

Module: SE3112 Advanced Software Engineering, take-home assessment (Microservice Testing Pipeline). Group 17.

### Who did which part

- **Unit Testing and Mocking:** Waidyaratne G. D (IT24104152). xUnit, Moq and FluentAssertions, in this repository. Details in [Unit Testing and Mocking](#unit-testing-and-mocking).
- **Code Quality and Metrics:** Pramuditha M. M (IT24103477). SonarQube Cloud, SonarScanner for .NET and Coverlet, run by the CI workflow in every backend service repository, including this one.
- **Load and Performance Testing:** Jayewardhana J. K. N. T (IT24104317). Apache JMeter 5.6.3, against the sellora-order and sellora-notification endpoints.
- **UI / End-to-End Testing:** Udayaratna H. S. S (IT24103532). [Tool and repository]
- **Test automation (group):** a GitHub Actions workflow in each service repository that builds, tests, analyses and packages the service on every push.

### Pipeline in this repository

Workflow: `.github/workflows/ci.yml`, job `Build and Test` on `ubuntu-latest`.

Triggers: every push to any branch, and every pull request into `main` or `dev`.

Service containers: PostgreSQL 16 and Apache Kafka 3.9.2.

Steps, in order:

1. Check out the repository and set up .NET SDK 8.0.403 and JDK 17 (the SonarQube scanner needs Java).
2. Restore `Sellora.CoreService.sln`.
3. SonarQube begin: project `Team-Sellora_sellora-organization` in organization `team-sellora`, reading OpenCover coverage and excluding EF migrations.
4. Build the solution in Release mode.
5. Wait until the Kafka broker answers.
6. Run all tests (unit and integration) with Coverlet coverage in OpenCover format, writing trx results to `./TestResults`.
7. SonarQube end: upload the analysis. The Sonar way quality gate then passes or fails the change.
8. Build the Docker image, tagged with the commit SHA.
9. Start the image and require `GET /health` to succeed.
10. Upload `./TestResults` as the `test-results` artifact.

Reporting: each run keeps its trx results as a downloadable artifact, and the SonarQube Cloud dashboard shows coverage, duplication, code smells and the gate result for the service.

### Running the same checks locally

Prerequisites: .NET SDK 8.0.403 (pinned in `global.json`) and Docker Desktop for the integration tests.

Unit tests only (no database, no Docker, no Kafka):

```bash
dotnet test --filter "Category=Unit"
```

Integration tests (Testcontainers starts PostgreSQL through Docker; the outbox tests need a Kafka broker on `localhost:9092`):

```bash
cp .env.example .env
docker compose up -d kafka
dotnet test --filter "Category=Integration"
```

Everything with coverage, the same way CI runs it:

```bash
dotnet test Sellora.CoreService.sln \
  --collect:"XPlat Code Coverage" \
  --logger trx \
  --results-directory ./TestResults \
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
```

## Unit Testing and Mocking

Owner: Waidyaratne G. D (IT24104152). Tracked in [issue #34](https://github.com/Team-Sellora/sellora-organization/issues/34).

Assignment requirement: implement unit testing using parameterized cases and test doubles (mocks/stubs) to isolate service logic.

### How the requirement is met

- **Unit testing:** 189 unit test cases in `tests/Sellora.CoreService.UnitTests`, kept separate from the 172 integration cases in `tests/Sellora.CoreService.Tests`.
- **Parameterized cases:** 49 xUnit `[Theory]` methods using `InlineData` and `TheoryData`. Each test class documents how its inputs were chosen (input space partitioning, Base Choice coverage, boundary values).
- **Test doubles:** all five kinds (dummy, stub, spy, mock, fake), each used where it fits. See [Where each test double is used](#where-each-test-double-is-used).
- **Isolated service logic:** the unit suite needs no database, Docker or Kafka. This was proven by running it with Docker Desktop quit. Decision logic that was tied to `DbContext` was moved into pure methods, with the integration tests passing before and after.

### At a glance

- **Project:** `tests/Sellora.CoreService.UnitTests`. Folders mirror the production namespaces (`Api/...`, `Infrastructure/...`).
- **Tools and versions:** .NET SDK 8.0.403, xUnit 2.5.3, xunit.runner.visualstudio 2.5.3, Microsoft.NET.Test.Sdk 17.8.0, FluentAssertions 8.10.0, Moq 4.21.0, coverlet.collector 6.0.0.
- **Category traits:** every unit test class carries `[Trait("Category", "Unit")]` and every integration test class carries `[Trait("Category", "Integration")]`, so each layer can run on its own. Every test has exactly one of the two.
- **Scope:** Sprint 1 stories US-E-0-4 and US-E-1-2 to US-E-1-8. That covers E0 (JWT validation, role policies, tenancy) and E1 (provinces, area managers, agencies, territories, territory-agency assignment, shops, sales-rep assignment, hierarchy events and the outbox relay).
- **Out of scope:** SCIM and WSO2 identity provisioning, staff provisioning, `/api/me/scope`, Kafka SASL, E3 stock and other Sellora services. Existing tests for these were moved or tidied, not extended.
- **Rule kept throughout:** `DbContext` and `DbSet` are never mocked, and neither EF InMemory nor SQLite is used as a stand-in. The duplicate handling depends on real PostgreSQL unique constraints, so code whose logic is the query itself stays covered by the integration tests.

### Test design approach

- **Inputs:** input space partitioning. For each method under test, the class comment lists the characteristics, their blocks (disjoint and complete), the boundary values and the coverage criterion.
- **Criteria:** Base Choice where there are several characteristics (1 + the sum of (blocks - 1) cases instead of the product for All Combinations; for example 6 rows instead of 18 in `AgencyUniqueNameViolationTests`), Each Choice for a single characteristic, and Boundary Value Analysis on both sides of every numeric limit (outbox error truncation at 2000 and 2001 characters; page size 1 and 100 here, with 0 and 101 already in `ControllerOutcomeMappingTests`).
- **Theory data:** `InlineData` with primitives and enums so each row is readable in the runner output, and `TheoryData` where an expected value is a `Guid`, a nullable `Guid` or a timestamp. Every row has a trailing comment naming its block or boundary.
- **Style:** `Method_Scenario_ExpectedResult` names, Arrange / Act / Assert comments, one behaviour per test, no branching in test methods, exact-value assertions, and the instance under test named `_sut`.

### Where each test double is used

- **Dummy:** Strict Moq dummies for constructor dependencies a test never uses: `IProvinceReadService` in `Api/Controllers/ProvincesControllerTests.cs`, and the registration, deactivation, assignment and update services in `Api/Controllers/ListPagingBoundaryTests.cs`. Strict makes them fail loudly if production code ever calls them.
- **Stub:** Moq stubs that only supply input: `IHttpContextAccessor` in `Api/Tenancy/HttpTenantContextTests.cs` and `Api/Identity/HttpCurrentUserContextTests.cs`, `ICorrelationIdAccessor` in `Infrastructure/Outbox/HierarchyEventFactoryTests.cs`, and `IProvinceAssignmentService` in the outcome mapping Theory of `ProvincesControllerTests.cs`.
- **Spy:** a hand-written `RequestDelegate` in `Api/Middleware/CorrelationIdMiddlewareTests.cs` that records how often it ran and which correlation ID it saw, checked after the call.
- **Mock:** Moq `Verify` on `IProvinceAssignmentService` (exact request, `Times.Once`, and `VerifyNoOtherCalls` on the invalid-body rejection path) in `ProvincesControllerTests.cs`, and on each read service in `ListPagingBoundaryTests.cs` (page size forwarded unchanged).
- **Fake:** a field-backed `FakeHttpContextAccessor` in `Api/Outbox/HttpCorrelationIdAccessorTests.cs`, a working implementation with a shortcut that would be unsafe in production (no `AsyncLocal`).
- **No doubles:** the pure-logic classes for the constraint matchers, outbox truncation and the province scope decision, and `RolePoliciesTests`, which uses the real `DefaultAuthorizationService`.

### Production code changes (behaviour unchanged)

- **Visibility only:** `AgencyRegistrationService.IsUniqueAgencyNameViolation` and `TerritoryRegistrationService.IsConstraintViolation` changed from private to internal. They were already pure, so this is not a Humble Object extraction.
- **Humble Object, outbox:** `OutboxRelayService.TruncateLastError` is a pure internal static method ported line by line from the catch block. Proof: `OutboxRelayServiceTests` and `OutboxRelayKafkaIntegrationTests`, 8 of 8 passed before and after.
- **Humble Object, territory assignment:** `TerritoryAgencyAssignmentService.DecideProvinceScope` is a pure internal static method that returns the outcome, with the check order and all logging kept in the service. Proof: `TerritoryAgencyAssignmentEndpointTests`, 7 of 7 passed before and after, and `TerritoryAgencyOpenWorkGuardTests` passed after.
- **Whole-change proof:** the full integration suite passed 172 of 172 before and after the three changes.
- **Exposure:** `InternalsVisibleTo Sellora.CoreService.UnitTests` on the Infrastructure project only.

### Assertion quality

Each production line under test was mutated by hand (40 mutants, each reverted afterwards). 36 were killed on the first run. One survivor exposed a weak assertion in `HttpCorrelationIdAccessorTests`, which was sharpened so it now kills that mutant. The integration test `Put_TerritoryOutsideCallerProvinces_ReturnsForbidden` was also strengthened to assert the exact problem detail, so swapping the territory and agency checks now fails it.

Known limitations:

- `TerritoryConstraintViolationTests` does not vary the `PostgresException` type check or the `Ordinal` comparison. The same checks are covered in the agency copy of the predicate, which duplicates this logic.
- Changing `>` to `>=` at the 2000 character limit in `TruncateLastError` is an equivalent mutant. A 2000-character message comes out unchanged either way, so no test can detect it.
- No test asserts the fields of the `AssignAreaManager` 200 response body.
- No integration test drives the territory duplicate-name path end to end through `TerritoryRegistrationService`.

### Before and after

- **Before:** 198 test methods and 292 executed cases (unit 51 methods and 120 cases, integration 147 methods and 172 cases), 31 Theory methods, all in one project. Unit tests could not be run apart from the integration tests.
- **After:** 215 test methods and 361 executed cases (unit 71 methods and 189 cases, integration 144 methods and 172 cases), 49 Theory methods, in two projects with category traits.
- **Unit test execution:** about 0.2 s for 189 cases with no database, Docker or broker. The full `dotnet test` command takes a few seconds, most of it build and test host startup.
- **Integration test execution:** about 25 s for 172 cases (warm), with Docker and Kafka required.
- **Mocking:** before, 4 unit classes used hand-written doubles and the repository had no mocking library. After, Moq is used for stubs, mocks and Strict dummies, and hand-written doubles remain where they are simpler (the spy and the fake).

### Live demo

1. Run the unit suite with Docker Desktop quit: `dotnet test --filter "Category=Unit"`.
2. Open `Infrastructure/Agencies/AgencyUniqueNameViolationTests.cs` and walk through its design comment and its 6 Base Choice rows.
3. Introduce one fault: in `AgencyRegistrationService.IsUniqueAgencyNameViolation`, change `StringComparison.Ordinal` to `StringComparison.OrdinalIgnoreCase`.
4. Rerun that class: `dotnet test tests/Sellora.CoreService.UnitTests --filter "FullyQualifiedName~AgencyUniqueNameViolationTests"`. Exactly one row fails, the `UQ_AGENCY_PROVINCE_NAME` case. Revert the change.

### Prior work and credit

The story-level tests were written by each story's developer in Sprint 1. As part of this role they were separated into unit and integration projects (the genuine unit tests were moved with `git mv` so their history is kept), repetitive Facts were consolidated into Theories (`JwtValidationTests`, `RolePolicyTests` and the SCIM options Fact), and the suite was extended with the new unit test classes listed above.

## Platform overview

Sellora follows a service-oriented architecture. Each backend service owns a specific business capability and follows shared engineering conventions for authentication, authorization, data isolation, logging, health checks and error handling.

### Core principles

- **Secure by default:** JWT bearer authentication and role-based authorization protect service endpoints.
- **Tenant isolation:** company data is isolated at the data-access layer.
- **Clear service boundaries:** business capabilities are separated into focused services.
- **Observable operations:** structured logs, correlation IDs, health checks and consistent error responses simplify diagnosis.
- **Consistent development:** shared templates and conventions make services easier to create, test and maintain.

### Technology

- .NET 8 and ASP.NET Core
- Entity Framework Core with PostgreSQL (Npgsql)
- Apache Kafka for hierarchy events, published through a transactional outbox
- JWT bearer authentication against WSO2 Identity Server, with role-based authorization policies
- Serilog structured logging and Problem Details error responses
- xUnit unit and integration tests, with Testcontainers for PostgreSQL
- Docker Compose for local PostgreSQL and Kafka

### Architecture

Backend services follow Clean Architecture:

```text
Domain
  Core business entities and rules

Application
  Use cases, contracts and application logic

Infrastructure
  Persistence and external integrations

API
  HTTP endpoints, authentication, middleware and configuration

Tests
  Sellora.CoreService.UnitTests: isolated unit tests (Category=Unit)
  Sellora.CoreService.Tests: integration tests against real infrastructure (Category=Integration)
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

### Security model

Sellora services validate a token's issuer, audience, lifetime and signature. Named authorization policies restrict protected operations to the appropriate roles.

Tenant identity is derived from authenticated claims and applied through the persistence layer. Client-provided identifiers must not be trusted as the authority for tenant access.

Security-sensitive behaviour is covered by integration tests, including:

- Missing, expired or invalid tokens returning `401 Unauthorized`
- Authenticated users without the required role receiving `403 Forbidden`
- Users retrieving only records belonging to their company
- Attempts to access another company's records being rejected or filtered

## Local development

Restore, build and test:

```bash
dotnet restore
dotnet build
dotnet test
```

Configuration should be supplied through local development settings, environment variables or a secret manager. Do not commit credentials, signing keys, connection strings or other secrets.

### Local database password

The local PostgreSQL password is not committed. Set it once:

```bash
cp .env.example .env          # docker compose reads ORGANIZATION_DB_PASSWORD from here
docker compose up -d

# Give the API the same password (stored outside the repo by user-secrets).
dotnet user-secrets set "ConnectionStrings:Default" \
  "Host=localhost;Port=5433;Database=organization_db;Username=sellora;Password=<ORGANIZATION_DB_PASSWORD>" \
  --project src/Sellora.CoreService.Api
```

Alternatively, set `PGPASSWORD` in your shell; Npgsql uses it when the connection string has no password.
