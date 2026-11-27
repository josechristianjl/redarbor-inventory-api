# Inventory Management API

Technical assessment for Redarbor — Senior Developer.

REST API for managing categories, products and inventory movements. Built with .NET 10, Clean Architecture and explicit CQRS, using Entity Framework Core for reads and Dapper for transactional writes. SQL Server provides persistence and Keycloak provides OAuth2 authentication and authorization.

## Prerequisites

- Windows with PowerShell and Git.
- Docker Desktop running.
- Available local ports: 8080 (API), 8081 (Keycloak) and 14330 (SQL Server).

The .NET 10 SDK is only required for local development and automated tests. Visual Studio is optional and is used below for debugging. No local SQL Server or Keycloak installation is required.

## Quick start

### 1. Clone the repository

```powershell
git clone https://github.com/josechristianjl/redarbor-inventory-api.git
cd redarbor-inventory-api
```

### 2. Generate credentials

```powershell
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\setup.ps1
```

The script generates five independent, cryptographically random credentials in `.env` and opens the file in Windows Notepad. No manual configuration is needed. Keep the file available for the login step. An existing `.env` is opened without modification; it is not regenerated.

### 3. Start the application

```powershell
docker compose up -d --build
```

Docker Compose builds the API, starts SQL Server and Keycloak, initializes the database with a restricted application account, imports the OAuth2 realm and starts the API when its dependencies are ready.

Check startup with `docker compose ps -a`. The `api`, `sqlserver` and `keycloak` services should be healthy; `db-init` should exit with code 0.

### 4. Open Swagger and sign in

Open [Swagger UI](http://localhost:8080/swagger/index.html).

1. Click **Authorize**.
2. Use client ID `inventory-swagger`, leave the client secret empty and select the `inventory.access` scope.
3. Click **Authorize** in the dialog to open the Keycloak sign-in page.
4. Enter username `inventory-user` and the value of `KEYCLOAK_DEMO_USER_PASSWORD` from `.env` (without the surrounding single quotes).
5. Click **Sign In**. After returning to Swagger, close the authorization dialog.

Swagger uses OAuth2 Authorization Code with PKCE. No manual Keycloak client or user configuration is required. Access tokens expire after 5 minutes. If a request returns `401 Unauthorized` because the token expired, click **Authorize**, log out and authorize again. Keycloak may reuse its browser session.

## Try the API

Use **Try it out → Execute** in Swagger. All eleven business endpoints require authorization. Without a token, `GET /api/v1/categories` returns `401 Unauthorized`; after sign-in it returns `200 OK`. On a fresh database, the collection is empty.

### 1. Create a category

`POST /api/v1/categories`

```json
{
  "name": "Electronics",
  "description": "Electronic products"
}
```

Expected: `201 Created`. Save the `id` from the response.

### 2. Create a product

`POST /api/v1/products`

```json
{
  "sku": "LAPTOP-001",
  "name": "Laptop",
  "categoryId": 1,
  "description": "Portable computer"
}
```

Replace `categoryId` with the actual category ID. Expected: `201 Created`. Save the product `id`. Its initial stock is zero.

### 3. Register an inventory entry

`POST /api/v1/inventory-movements`

```json
{
  "productId": 1,
  "type": "Entry",
  "quantity": 10
}
```

Replace `productId` with the actual product ID. Expected: `201 Created`.

### 4. Register an inventory exit

Use the same endpoint and product ID:

```json
{
  "productId": 1,
  "type": "Exit",
  "quantity": 4
}
```

Expected: `201 Created`. Call `GET /api/v1/products/{id}` using the product ID to verify `stock: 6`. An exit of 7 units should return `409 Conflict` and leave stock unchanged.

To test deletion, first register an exit of the remaining 6 units, delete the product, then delete the category. Both DELETE requests should return `204 No Content`. Deletion is logical and preserves inventory history. Use unique names and SKUs if repeating the example.

## API reference

Base path: `/api/v1`. The full OpenAPI contract is available through [Swagger](http://localhost:8080/swagger/index.html).

| Method | Path | Success |
|---|---|---|
| GET | `/categories` | 200 |
| GET | `/categories/{id}` | 200 |
| POST | `/categories` | 201 |
| PUT | `/categories/{id}` | 204 |
| DELETE | `/categories/{id}` | 204 |
| GET | `/products` | 200 |
| GET | `/products/{id}` | 200 |
| POST | `/products` | 201 |
| PUT | `/products/{id}` | 204 |
| DELETE | `/products/{id}` | 204 |
| POST | `/inventory-movements` | 201 |

Collection endpoints accept `page` (default 1) and `pageSize` (default 20, maximum 100), ordered by ID. PUT uses the same body as the corresponding POST. Category and product creation return an ID and a Location header; movement creation returns an ID only.

Products require active categories. Stock changes only through movements, cannot become negative or overflow, and must be zero before product deletion. Categories cannot be deleted while they contain active products. Names (categories) and SKUs (products) remain unique even after deletion. Stock updates and movement insertion are atomic. See the [functional specification](docs/SPEC.md) for validation rules and full contracts.

Errors use `application/problem+json` with application error codes and trace IDs: 400 invalid input, 401 invalid or missing authentication, 403 missing scope, 404 missing/inactive resource, 405 unsupported method, 409 conflict, 415 unsupported media type, and 500 unexpected error.

### Service-to-service authentication

For non-interactive clients, use OAuth2 Client Credentials with client ID `inventory-service` and the value of `KEYCLOAK_SERVICE_CLIENT_SECRET` from `.env`.

```powershell
$secret = [System.Net.NetworkCredential]::new('', (Read-Host 'Service client secret' -AsSecureString)).Password
try {
    $token = Invoke-RestMethod -Method Post `
        -Uri 'http://localhost:8081/realms/inventory/protocol/openid-connect/token' `
        -ContentType 'application/x-www-form-urlencoded' `
        -Body @{
            grant_type = 'client_credentials'
            client_id = 'inventory-service'
            client_secret = $secret
            scope = 'inventory.access'
        }
    Invoke-RestMethod -Uri 'http://localhost:8080/api/v1/categories' `
        -Headers @{ Authorization = "Bearer $($token.access_token)" }
}
finally {
    Remove-Variable -Name secret,token -ErrorAction SilentlyContinue
}
```

## Local development and debugging

Install the .NET 10 SDK and open `InventoryManagement.slnx` in Visual Studio. Complete the credential generation step first if `.env` does not exist. From the repository root, run:

```powershell
docker compose stop api
docker compose up -d --wait --wait-timeout 180 sqlserver keycloak db-init
```

The containerized API is stopped; SQL Server and Keycloak remain available. The `.env` file is consumed by Docker Compose, not automatically loaded by ASP.NET Core outside Docker.

1. Set `InventoryManagement.Api` as the startup project.
2. Right-click the API project and select **Manage User Secrets**.
3. Configure the following values, replacing the password placeholder with `SQL_SERVER_PASSWORD` from `.env`.

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

4. Select the HTTPS launch profile and press F5. Set breakpoints in controllers, handlers or repositories and repeat an authenticated Swagger request at https://localhost:7227/swagger/index.html using the same login steps.

Alternatively, after configuring User Secrets, run `dotnet run --project src/InventoryManagement.Api --launch-profile https`. If needed, trust the local development certificate with `dotnet dev-certs https --trust`.

## Automated tests

From the repository root, with the .NET 10 SDK and Docker Desktop running:

```powershell
dotnet build InventoryManagement.slnx --configuration Release --no-incremental
dotnet test InventoryManagement.slnx --configuration Release --no-build
```

Domain and Application unit tests do not require Docker. Integration tests use Testcontainers with isolated SQL Server databases, the actual API host where applicable, and controlled JWT signing keys. They do not require a running Compose stack or Keycloak and do not modify the local development database. Real Keycloak flows are checked separately through Swagger and Client Credentials.

Latest reported local regression (2026-11-27):

| Test project | Passed |
|---|---:|
| Domain.Tests | 96 |
| Application.Tests | 142 |
| IntegrationTests | 313 |
| **Total** | **551** |

Failed: 0. Skipped: 0. Test duration: 86.2 seconds.

Generate coverage reports independently:

```powershell
dotnet test tests/InventoryManagement.Domain.Tests/InventoryManagement.Domain.Tests.csproj -c Release --collect "XPlat Code Coverage" --results-directory TestResults/DomainCoverage
dotnet test tests/InventoryManagement.Application.Tests/InventoryManagement.Application.Tests.csproj -c Release --collect "XPlat Code Coverage" --results-directory TestResults/ApplicationCoverage
dotnet test tests/InventoryManagement.IntegrationTests/InventoryManagement.IntegrationTests.csproj -c Release --collect "XPlat Code Coverage" --results-directory TestResults/IntegrationCoverage
```

Earlier measured coverage (before the latest refactorings):

| Test execution | Scope | Lines | Branches |
|---|---|---:|---:|
| Domain unit tests (Phase 5) | Domain | 96.31% | 100.00% |
| Application unit tests (Phase 6) | Application | 96.70% | 88.46% |
| Integration tests (Phase 8) | Aggregate | 97.84% | 88.10% |

These are historical, separate Cobertura measurements, not merged coverage for the current 551 tests. Generated reports are ignored by Git.

## Docker operations

If Docker Desktop is configured for Windows containers, switch to Linux containers before starting the project.

| Service | Local address |
|---|---|
| API / Swagger | http://localhost:8080/swagger/index.html |
| OpenAPI JSON | http://localhost:8080/swagger/v1/swagger.json |
| Keycloak | http://localhost:8081 (realm `inventory`) |
| SQL Server | `127.0.0.1,14330` (database `InventoryManagement`) |
| Liveness | http://localhost:8080/health/live |
| Readiness | http://localhost:8080/health/ready |

Both health endpoints are anonymous. Liveness checks the API process; readiness checks SQL connectivity (not the full schema or Keycloak). Healthy responses are HTTP 200; unavailable SQL yields readiness HTTP 503.

```powershell
docker compose ps -a
docker compose logs --tail=100 api
docker compose logs --tail=100 db-init
docker compose up -d --wait --wait-timeout 180
```

`docker compose stop` stops services; `docker compose down` removes containers and networks while preserving named volumes. Do not use `down -v` unless intentional deletion of all database and Keycloak data is required.

**Known SQL Server limitation:** the tested 2022 CU28 image could terminate with exit code 137 on Docker stop timeout ([upstream issue](https://github.com/microsoft/mssql-docker/issues/968), consistent with observed behavior). For controlled local maintenance, stop the API first, connect to SQL Server as an authorized administrator (e.g. `sa` in this local environment), and execute `USE master; SHUTDOWN;` without `WITH NOWAIT`. This procedure was tested with exit code 255 (cause not established) and successful recovery on Compose restart. Do not assume forced shutdown is safe. See the architecture document for technical context.

## Architecture and security

The solution contains Domain (business invariants), Application (commands, queries and validation), Infrastructure (EF Core reads and parameterized Dapper writes with explicit transactions), and API (REST, JWT validation and Swagger). Main dependencies include SQL Server 2022, Keycloak 26.8.2, FluentValidation, xUnit, Moq, Testcontainers and Coverlet.

The API validates JWT signature (RS256), exact issuer and audience, expiration and `inventory.access` scope. All published service ports bind to loopback. Swagger is enabled only in Development. The API container runs as a non-root user.

Generated credentials and User Secrets are local plaintext configuration, not encrypted production secret stores. Never commit `.env`, passwords or tokens. Keep existing credentials consistent with persistent SQL Server and Keycloak data; regenerating a file does not rotate those credentials. The development-mode Keycloak, local HTTP, trusted SQL certificate and shared API scope are intended for local evaluation only. Production hardening, fine-grained roles and container vulnerability scanning are outside the verified scope. The API uses JWT rather than server-side authentication cookies; local Data Protection keys are not persisted.

## Additional documentation

- [Functional specification](docs/SPEC.md)
- [Architecture and technical decisions](docs/ARCHITECTURE.md)
- [AI usage, prompts and review evidence](docs/AI_USAGE.md)
- [Professional decision-making](docs/DECISIONS.md)
