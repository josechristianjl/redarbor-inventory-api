# Inventory Management API

Technical assessment for Redarbor — Senior Developer.

RESTful inventory management API designed with .NET 10, Clean Architecture, CQRS, SQL Server, Entity Framework Core for reads and Dapper for writes.

## Development status

Project setup is in progress. Business endpoints, database initialization, OAuth2 and Docker Compose are not implemented yet.

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

Use the local URL printed by the application and append `/swagger` when running in Development. Alternatively, open `InventoryManagement.slnx` in Visual Studio, set the API as startup project and press F5.

## Tests

```sh
dotnet test InventoryManagement.slnx
```

Business tests will be added using TDD. No functional coverage is claimed at this stage.

## Architecture and documentation

- [Functional specification](docs/SPEC.md)
- [Architecture and technical decisions](docs/ARCHITECTURE.md)
- [AI usage and review log](docs/AI_USAGE.md)

Full deployment, authentication and endpoint instructions will be added once implemented and verified. Do not commit local secrets or `.env` files.
