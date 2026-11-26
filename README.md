# Inventory Management API

Technical assessment for Redarbor — Senior Developer.

RESTful inventory management API built with .NET 10, Clean Architecture, CQRS, SQL Server, Entity Framework Core for reads and Dapper for writes.

## Development status

| Component | Status |
|---|---|
| Functional specification | Completed |
| Architecture and technical design | Completed |
| Solution and project configuration | Completed |
| Domain model and business rules | Completed |
| Database schema and initialization | Completed |
| EF Core read repositories | Completed |
| Dapper write repositories | Completed |
| Inventory transactions and rollback | Completed |
| Concurrency verification | Completed |
| Application use cases and validation | Pending |
| REST API endpoints | Pending |
| OAuth2 authentication and authorization | Pending |
| Full Docker Compose deployment | Pending |
| CI/CD | Pending |

## Architecture

The solution follows Clean Architecture with explicit CQRS responsibilities.

- Domain: entities, business rules and stock invariants.
- Application: persistence contracts, read models and business use cases.
- Infrastructure: SQL Server, EF Core, Dapper and transactional persistence.
- API: ASP.NET Core controllers and dependency composition.

EF Core handles application reads using projections and AsNoTracking.

Dapper handles application writes using parameterized SQL, explicit transactions and consistent locking protocols.

SQL Server provides relational constraints, unique indexes and restricted application permissions.

## Prerequisites

- .NET 10 SDK
- Docker Desktop or a compatible Docker Engine
- Docker Compose V2
- Visual Studio 2026 (optional IDE)
- SQL Server Management Studio (optional database administration)

## Database setup

From the repository root, create a local environment file.

PowerShell:

```powershell
Copy-Item .env.example .env
```

Configure the required SQL Server passwords in `.env`.

Use different strong passwords for MSSQL_SA_PASSWORD and SQL_SERVER_PASSWORD.

Start SQL Server and initialize the database:

```sh
docker compose up -d --wait sqlserver
docker compose run --rm --no-deps db-init
docker compose ps -a
```

The initialization process creates:

- InventoryManagement database.
- Categories, Products and InventoryMovements tables.
- Primary keys, foreign keys, unique constraints and check constraints.
- The inventory_app login and restricted database user.

The initialization scripts can be executed repeatedly without deleting existing records. They are initialization scripts, not a general schema migration engine.

### Local SQL Server connection

| Setting | Value |
|---|---|
| Server | 127.0.0.1,14330 |
| Database | InventoryManagement |
| Application user | inventory_app |
| Administrative user | sa |
| Authentication | SQL Server Authentication |

Passwords are obtained from the local environment configuration.

Trust Server Certificate may be enabled for local development only.

SQL Server data is stored in a named Docker volume.

Do not execute docker compose down -v unless intentional database deletion is required.

## Build and run locally

From the repository root:

```sh
dotnet restore InventoryManagement.slnx
dotnet build InventoryManagement.slnx
dotnet run --project src/InventoryManagement.Api
```

Alternatively, open InventoryManagement.slnx in Visual Studio, set InventoryManagement.Api as the startup project and press F5.

Swagger is available in the Development environment.

Business endpoints and API persistence registration are pending. The persistence components can currently be exercised through integration tests.

## Tests

Docker must be running before executing SQL Server integration tests.

Build the complete solution:

```sh
dotnet build --configuration Release --no-incremental
```

Run domain unit tests:

```sh
dotnet test tests/InventoryManagement.Domain.Tests/InventoryManagement.Domain.Tests.csproj --configuration Release
```

Run integration tests:

```sh
dotnet test tests/InventoryManagement.IntegrationTests/InventoryManagement.IntegrationTests.csproj --configuration Release
```

Testcontainers creates isolated SQL Server instances and initializes them using the project database scripts.

The integration tests do not modify the development database.

### Coverage

```sh
dotnet test tests/InventoryManagement.Domain.Tests/InventoryManagement.Domain.Tests.csproj --configuration Release --collect:"XPlat Code Coverage" --results-directory "./TestResults/DomainCoverage"

dotnet test tests/InventoryManagement.IntegrationTests/InventoryManagement.IntegrationTests.csproj --configuration Release --collect:"XPlat Code Coverage" --results-directory "./TestResults/IntegrationCoverage"
```

Generated coverage reports are excluded from version control.

### Quality baseline — Phase 5

Local execution results reported by the candidate on 2026-11-26:

| Metric | Result |
|---|---:|
| Domain unit test cases | 95 |
| Integration project test cases | 96 |
| Total test cases | 191 |
| Passed | 191 |
| Failed | 0 |
| Skipped | 0 |
| Release build warnings | 0 |
| Domain line coverage | 96.31% |
| Domain branch coverage | 100.00% |
| Infrastructure line coverage | 97.54% |
| Infrastructure branch coverage | 90.62% |

Domain coverage is obtained from Domain.Tests. Infrastructure coverage is obtained from IntegrationTests. These are separate assembly-level measurements, not combined solution coverage.

The Application.Tests project is currently empty and will be populated during application use case development.

### Verified persistence scenarios

- Active-only queries and deterministic pagination.
- Category and product creation, update and logical deletion.
- Case-insensitive uniqueness, including inactive records.
- Inventory entry and exit registration.
- Insufficient stock and stock overflow prevention.
- Atomic stock updates and inventory movement insertion.
- Transaction rollback after a database insertion failure.
- Concurrent stock updates and conflicting deletion operations.
- Restricted application account permissions.
- Physical SQL Server constraints.

Seven concurrency test cases were additionally executed ten consecutive times, with all 70 executions passing.

Database volume persistence was verified by recreating the SQL Server container and rerunning the initialization process.

## Documentation

- [Functional specification](docs/SPEC.md)
- [Architecture and technical decisions](docs/ARCHITECTURE.md)
- [AI usage and review log](docs/AI_USAGE.md)

## Security and limitations

- No local secrets or .env files should be committed.
- The application database account follows a restricted permission model.
- Inventory consistency depends on all business writes following the transactional persistence protocol.
- Automatic retries after an uncertain transaction commit are not implemented.
- HTTP endpoints, OAuth2 protection and complete containerized deployment remain pending.
