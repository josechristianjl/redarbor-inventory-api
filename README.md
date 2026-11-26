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
| Application use cases and validation | Completed |
| Dependency registration and composition tests | Completed |
| REST API endpoints | Pending |
| Centralized HTTP error handling | Pending |
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

All eleven application use cases have been implemented and tested.

Application and persistence dependency registration methods are available and have been verified through composition tests.

The registration methods are not yet invoked by Program.cs. Business endpoints, centralized HTTP error handling and OAuth2 integration remain pending.

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

Run application unit tests:

```sh
dotnet test tests/InventoryManagement.Application.Tests/InventoryManagement.Application.Tests.csproj --configuration Release
```

Application tests use xUnit, Moq and real FluentValidation validators. They do not require SQL Server or Docker.

Run integration tests:

```sh
dotnet test tests/InventoryManagement.IntegrationTests/InventoryManagement.IntegrationTests.csproj --configuration Release
```

Testcontainers creates isolated SQL Server instances and initializes them using the project database scripts.

The integration tests do not modify the development database.

Run dependency composition tests independently:

```sh
dotnet test tests/InventoryManagement.IntegrationTests/InventoryManagement.IntegrationTests.csproj --configuration Release --filter "FullyQualifiedName~ApplicationRegistrationTests"
```

Composition tests verify service resolution, dependency graphs and service lifetimes without opening SQL Server connections.

Run the complete test suite:

```sh
dotnet test --configuration Release
```

Docker must be running for the complete test suite.

### Coverage

```sh
dotnet test tests/InventoryManagement.Domain.Tests/InventoryManagement.Domain.Tests.csproj --configuration Release --collect:"XPlat Code Coverage" --results-directory "./TestResults/DomainCoverage"

dotnet test tests/InventoryManagement.IntegrationTests/InventoryManagement.IntegrationTests.csproj --configuration Release --collect:"XPlat Code Coverage" --results-directory "./TestResults/IntegrationCoverage"

dotnet test tests/InventoryManagement.Application.Tests/InventoryManagement.Application.Tests.csproj --configuration Release --collect:"XPlat Code Coverage" --results-directory "./TestResults/ApplicationCoverage"
```

Generated coverage reports are excluded from version control.

### Quality baseline — Phase 6

Local Release execution results reported by the candidate:

| Metric | Result |
|---|---:|
| Domain unit test cases | 95 |
| Application unit test cases | 142 |
| Persistence and database integration test cases | 96 |
| Dependency composition test cases | 22 |
| Total test cases | 355 |
| Passed | 355 |
| Failed | 0 |
| Skipped | 0 |
| Release build warnings | 0 |

### Application use cases

| Module | Use cases | Unit test cases |
|---|---:|---:|
| Categories | 5 | 55 |
| Products | 5 | 70 |
| Inventory movements | 1 | 17 |
| Total | 11 | 142 |

Application implements explicit CQRS handlers without MediatR.

Request validation is performed using FluentValidation before persistence operations. Domain entities retain their own business invariants.

Inventory movement timestamps are generated through TimeProvider and stored in UTC.

### Measured code coverage

| Phase | Test project | Assembly | Line coverage | Branch coverage |
|---|---|---|---:|---:|
| 5 | Domain.Tests | Domain | 96.31% | 100.00% |
| 5 | IntegrationTests | Infrastructure | 97.54% | 90.62% |
| 6 | Application.Tests | Application | 96.70% | 88.46% |

Coverage values originate from separate assembly-level reports and are not combined solution coverage.

Domain and Infrastructure percentages are retained from the Phase 5 measurements.

All eleven Application handlers report 100% line coverage and 100% branch coverage.

The twelve uncovered Application lines belong to data transport records. No artificial tests or coverage exclusions were introduced solely to increase the coverage percentage.

### Verified application scenarios

- Paginated category and product queries.
- Active-resource lookup by identifier.
- Category and product creation, update and logical deletion.
- Required input validation and normalized text length validation.
- Rejection of invalid identifiers, quantities and movement types.
- Correct construction of new products with zero stock.
- Deterministic UTC timestamp generation for inventory movements.
- Propagation of domain and persistence rule exceptions.
- Cancellation token propagation.
- Rejection of invalid requests before repository calls.
- Resolution of all eleven handlers and ten validators through native dependency injection.
- Scoped handler and validator lifetimes.
- Singleton TimeProvider registration.

HTTP behavior and authentication remain subject to API-level verification.

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
