# Inventory Management API

Technical assessment for Redarbor — Senior Developer.

RESTful inventory management API designed with .NET 10, Clean Architecture, CQRS, SQL Server, Entity Framework Core for reads and Dapper for writes.

## Development status

Project setup and the initial domain implementation are complete. The domain includes categories, products, immutable inventory movements, stock invariants, input normalization and identifiable business-rule exceptions. The domain has no infrastructure dependencies.

Business endpoints, application handlers, database initialization, persistence, OAuth2 and Docker Compose are not implemented yet. In-memory stock rules do not establish database transaction safety or concurrency correctness.

## Prerequisites

- .NET 10 SDK
- Visual Studio 2026 (optional IDE)
- Docker with Docker Compose V2 (required for upcoming integration tests and full deployment)

## Build and run locally

From the repository root:

```sh
dotnet restore InventoryManagement.slnx
dotnet build InventoryManagement.slnx
dotnet run --project src/InventoryManagement.Api
```

Use the local URL printed by the application and append `/swagger` when running in Development. Alternatively, open `InventoryManagement.slnx` in Visual Studio, set the API as startup project and press F5. Swagger is configured; business endpoints are not available yet.

## Tests

Run solution tests:

```sh
dotnet test InventoryManagement.slnx
```

Run domain tests with coverage:

```sh
dotnet test tests/InventoryManagement.Domain.Tests/InventoryManagement.Domain.Tests.csproj --collect:"XPlat Code Coverage" --results-directory "./TestResults/DomainCoverage"
```

The collector generates a `coverage.cobertura.xml` file under the results directory. Generated reports should not be committed.

### Domain quality baseline

Local results reported by the candidate on 2026-11-25:

| Metric | Result |
|---|---:|
| Domain unit test cases | 84 |
| Passed / failed / skipped | 84 / 0 / 0 |
| Build warnings (latest domain test run) | 0 |
| Domain line coverage | 94.94% |
| Domain branch coverage | 100.00% |

Coverage values are from the inspected 84-case Coverlet report immediately before the test naming analyzer configuration was corrected. The subsequent 84-case run generated a new report and completed without warnings; its XML has not been inspected. Coverage is specific to the domain assembly and does not measure the whole solution. Nine uncovered lines were consistent with the private parameterless constructors reserved for persistence materialization. No coverage exclusions or reflection-only tests were introduced to inflate the metric.

Application, database, concurrency, rollback and authentication tests remain pending. See the [AI usage and review log](docs/AI_USAGE.md) for the reported RED/GREEN cycles and regression checks.

## Architecture and documentation

- [Functional specification](docs/SPEC.md)
- [Architecture and technical decisions](docs/ARCHITECTURE.md)
- [AI usage and review log](docs/AI_USAGE.md)

Full deployment, authentication, database setup and endpoint instructions will be added once implemented and verified. Do not commit local secrets or `.env` files.
