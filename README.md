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
| Dependency registration and composition | Completed |
| REST API endpoints | Implemented and locally verified |
| Centralized HTTP error handling | Implemented and locally verified |
| OAuth2 authentication and authorization | Implemented and locally verified |
| Swagger and Authorization Code with PKCE | Implemented and locally verified |
| Full API host integration tests | Completed |
| Full Docker Compose deployment | Pending |
| CI/CD | Pending |

## Architecture

The solution follows Clean Architecture with explicit CQRS responsibilities.

- Domain: entities, business rules and stock invariants.
- Application: commands, queries, handlers, validators and persistence abstractions.
- Infrastructure: SQL Server, EF Core, Dapper and transactional persistence.
- API: ASP.NET Core controllers, dependency composition, JWT authentication, authorization, Swagger and centralized error handling.

EF Core handles reads using projections and AsNoTracking.

Dapper handles writes using parameterized SQL, explicit transactions and consistent locking protocols.

SQL Server provides relational constraints, unique indexes and restricted application permissions.

Keycloak acts as the OAuth2 authorization server. The API acts as a protected resource server.

## Technology stack

- .NET 10 LTS
- ASP.NET Core
- Entity Framework Core 10
- Dapper
- SQL Server 2022
- Keycloak 26.8.2
- FluentValidation
- Swashbuckle.AspNetCore 11.0.1
- xUnit
- Moq
- Testcontainers
- Coverlet
- Docker Compose

## Prerequisites

- .NET 10 SDK
- Docker Desktop or compatible Docker Engine
- Docker Compose V2
- Visual Studio 2026 (optional IDE)
- SQL Server Management Studio (optional database administration)

## Local infrastructure setup

From the repository root, create a local environment file if one does not already exist.

PowerShell:

```powershell
Copy-Item .env.example .env
```

Configure the required credentials in `.env`:

| Variable | Purpose |
|---|---|
| MSSQL_SA_PASSWORD | SQL Server administrator password |
| SQL_SERVER_PASSWORD | Restricted application database password |
| KC_BOOTSTRAP_ADMIN_PASSWORD | Initial Keycloak administrator password |
| KEYCLOAK_SERVICE_CLIENT_SECRET | Confidential OAuth2 client secret |
| KEYCLOAK_DEMO_USER_PASSWORD | Local demonstration user password |

Use different strong credentials.

Database initialization requires passwords between 8 and 128 printable ASCII characters, without spaces or single quotes. SQL Server password complexity requirements also apply.

Do not commit `.env` or replace existing credentials without planning credential rotation.

Start SQL Server and initialize the database:

```sh
docker compose up -d --wait sqlserver
docker compose run --rm --no-deps db-init
```

Start Keycloak:

```sh
docker compose up -d --wait keycloak
docker compose ps -a
```

The initialization process creates:

- InventoryManagement database.
- Categories, Products and InventoryMovements tables.
- Primary keys, foreign keys, unique constraints and check constraints.
- The inventory_app login and restricted database user.

Database initialization scripts can be executed repeatedly without deleting existing records. They are not a general schema migration engine and do not automatically rotate existing credentials.

Keycloak imports the inventory realm on initial startup. Existing realms are not overwritten by subsequent imports.

### Local service addresses

| Service | Address |
|---|---|
| SQL Server | 127.0.0.1,14330 |
| SQL database | InventoryManagement |
| Application SQL user | inventory_app |
| Keycloak | http://localhost:8081 |
| Keycloak realm | inventory |
| API HTTPS | https://localhost:7227 |
| API HTTP | http://localhost:5236 |
| Swagger UI | https://localhost:7227/swagger/index.html |

SQL Server and Keycloak data are stored in named Docker volumes.

Do not execute `docker compose down -v` unless intentional deletion of the persisted database and identity-provider data is required.

## Build and run locally

The local `.env` file is consumed by Docker Compose. ASP.NET Core does not automatically load it when running directly from Visual Studio or the .NET CLI.

Open `InventoryManagement.slnx` in Visual Studio.

Set `InventoryManagement.Api` as the startup project.

Right-click the API project and select Manage User Secrets.

Configure the following local settings:

```json
{
  "SQL_SERVER_HOST": "127.0.0.1",
  "SQL_SERVER_PORT": "14330",
  "SQL_SERVER_DATABASE": "InventoryManagement",
  "SQL_SERVER_USER": "inventory_app",
  "SQL_SERVER_PASSWORD": "<application-database-password>",
  "SQL_SERVER_TRUST_SERVER_CERTIFICATE": "true",
  "KEYCLOAK_AUTHORITY": "http://localhost:8081/realms/inventory",
  "KEYCLOAK_ISSUER": "http://localhost:8081/realms/inventory",
  "KEYCLOAK_AUDIENCE": "inventory-api",
  "KEYCLOAK_SWAGGER_CLIENT_ID": "inventory-swagger"
}
```

Replace the password placeholder with the actual application database password.

Select the HTTPS launch profile and press F5 to run and debug the API.

Alternatively, after configuring the same User Secrets:

```sh
dotnet restore InventoryManagement.slnx
dotnet build InventoryManagement.slnx
dotnet run --project src/InventoryManagement.Api --launch-profile https
```

If the local ASP.NET Core development certificate is not trusted:

```sh
dotnet dev-certs https --trust
```

Swagger is enabled only in the Development environment.

For debugging, place breakpoints in controllers, application handlers or persistence repositories and execute the relevant authenticated HTTP request through Swagger.

## Authentication and authorization

All eleven business endpoints require a valid JWT access token containing the `inventory.access` scope.

The API validates:

- Token signature and RS256 signing algorithm.
- Exact issuer.
- Expected audience.
- Token expiration and validity period.
- Required authorization scope.

The configured token lifetime is 300 seconds, with a 30-second validation clock skew.

### Swagger — Authorization Code with PKCE

1. Open Swagger UI.
2. Select Authorize.
3. Use client ID `inventory-swagger`.
4. Leave the client secret empty.
5. Select the `inventory.access` scope.
6. Authenticate using `inventory-user` and the configured demonstration user password.
7. Execute an authorized endpoint.

Keycloak may reuse an existing SSO session.

If an access token expires, remove the current Swagger authorization and authorize again.

Swagger Logout removes its stored authorization but does not necessarily terminate the Keycloak SSO session.

### Service authentication — Client Credentials

The confidential OAuth2 client is `inventory-service`.

PowerShell example:

```powershell
$clientSecret = [System.Net.NetworkCredential]::new(
    "",
    (Read-Host "Keycloak service client secret" -AsSecureString)
).Password

try {
    $token = Invoke-RestMethod `
        -Method Post `
        -Uri "http://localhost:8081/realms/inventory/protocol/openid-connect/token" `
        -ContentType "application/x-www-form-urlencoded" `
        -Body @{
            grant_type = "client_credentials"
            client_id = "inventory-service"
            client_secret = $clientSecret
            scope = "inventory.access"
        }

    Invoke-RestMethod `
        -Method Get `
        -Uri "https://localhost:7227/api/v1/categories" `
        -Headers @{
            Authorization = "Bearer $($token.access_token)"
        }
}
finally {
    Remove-Variable -Name clientSecret,token `
        -ErrorAction SilentlyContinue
}
```

Do not publish access tokens or client secrets in logs, screenshots or source control.

## REST API

Base route: `/api/v1`.

| Method | Endpoint | Success |
|---|---|---|
| GET | /categories | 200 OK |
| GET | /categories/{id} | 200 OK |
| POST | /categories | 201 Created |
| PUT | /categories/{id} | 204 No Content |
| DELETE | /categories/{id} | 204 No Content |
| GET | /products | 200 OK |
| GET | /products/{id} | 200 OK |
| POST | /products | 201 Created |
| PUT | /products/{id} | 204 No Content |
| DELETE | /products/{id} | 204 No Content |
| POST | /inventory-movements | 201 Created |

Category and product creation return the generated identifier and a Location header.

Inventory movement registration returns the generated identifier without a Location header because no movement lookup endpoint is defined.

### Pagination

Category and product collection endpoints support:

| Parameter | Default | Constraint |
|---|---:|---|
| page | 1 | Positive integer |
| pageSize | 20 | Between 1 and 100 |

Results are ordered by identifier.

Response structure:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalCount": 0
}
```

Only active categories and products are exposed.

### Request examples

Create or update a category:

```json
{
  "name": "Electronics",
  "description": "Electronic devices"
}
```

Create or update a product:

```json
{
  "sku": "LAPTOP-001",
  "name": "Laptop",
  "categoryId": 1,
  "description": "Portable computer"
}
```

Register an inventory entry:

```json
{
  "productId": 1,
  "type": "Entry",
  "quantity": 10
}
```

Register an inventory exit:

```json
{
  "productId": 1,
  "type": "Exit",
  "quantity": 4
}
```

Replace example identifiers with existing resource identifiers.

Category names and product names must not exceed 150 characters after trimming. Product SKU values must not exceed 50 characters. Optional descriptions must not exceed 500 characters after trimming.

Movement types are case-sensitive and accept only `Entry` or `Exit`. Quantities and resource identifiers must be positive integers.

Unknown request properties are rejected.

Stock, active state, movement timestamps and generated identifiers are not writable through request DTOs.

### Business rules

- Products start with zero stock.
- Stock changes only through inventory movements.
- Products require an active category.
- Stock cannot become negative or exceed the supported integer range.
- Category names and product SKUs are unique, including inactive records.
- Products with stock cannot be deactivated.
- Categories with active products cannot be deactivated.
- Deactivation is logical and preserves inventory history.
- Stock changes and movement insertion occur within the same transaction.

### Error responses

Errors use the `application/problem+json` media type and identifiable application error codes.

| Status | Meaning |
|---|---|
| 400 | Invalid request or validation failure |
| 401 | Missing or invalid authentication |
| 403 | Authenticated caller without the required scope |
| 404 | Missing or inactive resource |
| 405 | Unsupported HTTP method for the route |
| 409 | Business rule or uniqueness conflict |
| 415 | Unsupported media type |
| 500 | Unexpected server failure |

Error responses include a trace identifier. Validation errors include field-level information.

Internal exception details, stack traces and SQL errors are not returned to consumers.

## Tests

Docker must be running before executing SQL Server integration tests.

Build the complete solution:

```sh
dotnet build --configuration Release --no-incremental
```

Run all tests:

```sh
dotnet test --configuration Release --no-build
```

Run individual test projects:

```sh
dotnet test tests/InventoryManagement.Domain.Tests/InventoryManagement.Domain.Tests.csproj --configuration Release

dotnet test tests/InventoryManagement.Application.Tests/InventoryManagement.Application.Tests.csproj --configuration Release

dotnet test tests/InventoryManagement.IntegrationTests/InventoryManagement.IntegrationTests.csproj --configuration Release
```

Domain and Application unit tests do not require Docker.

Integration tests use Testcontainers to create isolated SQL Server instances with the project database initialization scripts.

HTTP controller tests use controlled repository dependencies.

Full-host integration tests use WebApplicationFactory, the actual API entry point, application services, EF Core, Dapper and isolated SQL Server instances.

JWT integration tests use controlled RSA signing keys and static OpenID Connect metadata. They do not require a running Keycloak instance.

Actual Keycloak authentication has been verified separately through local smoke checks and Swagger.

The automated tests do not modify the local development database.

### Quality baseline — Phase 7

Local Release execution reported on 2026-11-27:

| Test project | Passed |
|---|---:|
| Domain.Tests | 95 |
| Application.Tests | 142 |
| IntegrationTests | 293 |
| Total | 530 |
| Failed | 0 |
| Skipped | 0 |

Complete regression duration: 83.9 seconds.

Phase 7 additionally included 30 manual functional and database verification checks.

### Coverage

Generate separate coverage reports:

```sh
dotnet test tests/InventoryManagement.Domain.Tests/InventoryManagement.Domain.Tests.csproj --configuration Release --collect:"XPlat Code Coverage" --results-directory "./TestResults/DomainCoverage"

dotnet test tests/InventoryManagement.Application.Tests/InventoryManagement.Application.Tests.csproj --configuration Release --collect:"XPlat Code Coverage" --results-directory "./TestResults/ApplicationCoverage"

dotnet test tests/InventoryManagement.IntegrationTests/InventoryManagement.IntegrationTests.csproj --configuration Release --collect:"XPlat Code Coverage" --results-directory "./TestResults/IntegrationCoverage"
```

Measured coverage:

| Test execution | Assembly | Lines | Branches |
|---|---|---:|---:|
| Phase 5 Domain unit tests | Domain | 96.31% | 100.00% |
| Phase 6 Application unit tests | Application | 96.70% | 88.46% |
| Phase 7 integration tests | API | 99.43% | 89.23% |
| Phase 7 integration tests | Application | 100.00% | 88.46% |
| Phase 7 integration tests | Domain | 86.50% | 75.00% |
| Phase 7 integration tests | Infrastructure | 97.54% | 90.62% |
| Phase 7 integration tests | Aggregate | 97.60% | 87.67% |

The Phase 7 aggregate covers 1,506 of 1,543 instrumented lines and 192 of 219 branches.

These are separate test-run measurements, not merged coverage for the complete 530-test suite.

Generated coverage reports are excluded from version control.

### Verified scenarios

- Domain invariants and application input validation.
- Dependency composition and service lifetimes.
- CRUD operations and deterministic pagination.
- SQL Server constraints and restricted database permissions.
- Inventory consistency and transaction rollback.
- Concurrent stock movements and conflicting deletion operations.
- Centralized HTTP error responses.
- JWT signature, issuer, audience and lifetime validation.
- Missing authentication and insufficient authorization scope.
- Protection of all eleven business endpoints.
- Swagger OAuth2 configuration and PKCE initialization.
- Invalid authentication configuration.
- Complete HTTP inventory workflow with real SQL persistence.
- Swagger disabled in Production.

Seven concurrency cases were additionally executed ten consecutive times during Phase 5, with all 70 executions passing.

These are repeated executions, not additional distinct test cases or a load test.

## Docker deployment status

SQL Server, database initialization and Keycloak are configured and locally verified.

The API currently runs directly through the .NET host.

The API Dockerfile, complete Compose startup, container networking verification and CI/CD remain pending.

## Documentation

- [Functional specification](docs/SPEC.md)
- [Architecture and technical decisions](docs/ARCHITECTURE.md)
- [AI usage and review log](docs/AI_USAGE.md)

## Security and limitations

- Local credentials and environment files must not be committed.
- HTTP metadata and trusted SQL Server certificates are allowed only for controlled local development.
- Keycloak development mode, demonstration credentials, embedded database and bootstrap administrator are not production deployment configurations.
- The local authorization model uses a shared API scope rather than fine-grained business roles.
- Inventory consistency depends on business writes following the transactional persistence protocol.
- Automatic retries after an uncertain transaction commit are not implemented.
- Request idempotency is outside the current scope.
- Count and page retrieval do not guarantee snapshot consistency under concurrent writes.
- OpenAPI schema constraints may not express every normalized-input validation rule; the functional specification and server-side validation remain authoritative.
- Full containerized deployment and CI/CD verification remain pending.
