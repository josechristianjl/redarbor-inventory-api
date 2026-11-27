# Inventory Management API — Architecture

Status: Candidate-approved architecture baseline v0.2, with domain update v0.3, persistence implementation update v0.4, application implementation update v0.5 and REST API and security implementation update v0.6. Full container deployment and CI/CD remain pending.
Functional baseline: SPEC.md v0.3 (candidate-approved scope; architecture references updated).

## ADR-001 — Runtime and architecture
Use .NET 10 LTS and EF Core 10 (matching major versions), ASP.NET Core controllers, SQL Server 2022 and a modular monolith with Clean Architecture. The assessment permits .NET 8 or higher; .NET 8 reached end of support on 2026-11-10. .NET 10 is supported until November 2028. Reference: https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core

Dependency direction (compile-time): Application -> Domain; Infrastructure -> Application and Domain; API -> Application and Infrastructure (composition root). Domain has no infrastructure dependencies. Test projects reference the relevant production projects. No microservices or separate read database.

## ADR-002 — CQRS and persistence
Use explicit commands, queries and typed handlers registered with built-in DI; no mediator library initially. Controllers delegate to handlers. Application defines narrow read and write ports; Infrastructure implements them. EF Core uses AsNoTracking, DTO projections and deterministic pagination. Dapper executes parameterized SQL for all application writes. No generic repository or EF SaveChanges for business writes. Use versioned SQL initialization scripts, not competing EF migrations. The database initializer applies scripts before API readiness; repeated startup must preserve data.

## ADR-003 — Relational model
All IDs are int IDENTITY primary keys. Categories: Name nvarchar(150) NOT NULL UNIQUE, Description nvarchar(500) NULL, IsActive bit NOT NULL DEFAULT 1. Products: CategoryId int NOT NULL FK, SKU nvarchar(50) NOT NULL UNIQUE, Name nvarchar(150) NOT NULL, Description nvarchar(500) NULL, Stock int NOT NULL DEFAULT 0 CHECK (Stock >= 0), IsActive bit NOT NULL DEFAULT 1. InventoryMovements: ProductId int NOT NULL FK, Type tinyint NOT NULL CHECK (Type IN (1,2)), Quantity int NOT NULL CHECK (Quantity > 0), CreatedAt datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(). Entry=1, Exit=2 internally; API accepts strings Entry/Exit only. Explicit case-insensitive collation on unique strings; trim at input boundary. Unique values stay reserved after logical deletion. No cascading deletes. Index Products(CategoryId, IsActive) and InventoryMovements(ProductId, CreatedAt). Use UTC timestamps and serialize as UTC. Add DB checks for nonblank names/SKU. Map SQL uniqueness conflicts to HTTP 409.

## ADR-004 — Transaction and concurrency

Status: Implemented and locally integration-tested during Phase 5.

Inventory movements use one SQL connection and one explicit ReadCommitted transaction.

ProductLockReader acquires UPDLOCK and HOLDLOCK using the primary key index before restoring the persisted product state. Domain methods validate and apply stock changes.

Dapper updates the stock and inserts the immutable movement within the same transaction. The stock update must affect exactly one row. The transaction commits only after both operations succeed. Uncommitted transactions are disposed and rolled back on failure.

Product deactivation uses the same product locking protocol and requires zero stock.

Product creation, category reassignment and category deactivation coordinate through locks acquired on category primary keys.

Category reassignment acquires the affected category locks in ascending identifier order before locking the product. The original category is revalidated after the product lock is acquired. If it has changed, the transaction is discarded and the operation is retried, with a maximum of three attempts.

Category deactivation checks for active products while holding its category lock.

The originally proposed additional conditional stock update was not implemented. The current design relies on the locked domain state, a single-row update check and database stock constraints.

Seven concurrency integration scenarios passed, followed by ten successful repeated executions of the same seven cases.

A forced database constraint violation verified rollback after a stock update and before movement insertion.

General deadlock retries were deferred after the concurrency verification. This does not establish that deadlocks are impossible.

Automatic retries after an uncertain commit are deliberately avoided because they could duplicate inventory movements. Request idempotency is outside the current scope.

## ADR-005 — OAuth2
Keycloak is the authorization server; API is a resource server using JWT Bearer. For Swagger use authorization code + PKCE with a public client; for automated smoke tests use client credentials with a confidential client and service account. Configure realm, audience inventory-api and scope inventory.access; enforce signature, issuer, audience, lifetime and required scope. Missing/invalid token -> 401, valid token without permission -> 403. Configure public issuer separately from container-reachable metadata/backchannel if necessary; never disable issuer or signature validation to fix Docker networking. HTTP and Keycloak dev mode only for local evaluation; production requires HTTPS and production Keycloak configuration. No secrets in Git. Realm import and environment-based secret provisioning are part of setup. Verify both flows from a clean Compose environment.

## ADR-006 — Validation and errors
Use FluentValidation core with explicit invocation in handlers or at the API boundary; avoid duplicate validation. Use domain rules for pure stock invariants and database constraints for persistence invariants. Central exception handling and ProblemDetails with consistent codes; do not expose stack traces or SQL details. Native ILogger; do not log credentials or tokens. Movement POST returns 201 and ID without inventing a GET endpoint or Location URI.

## ADR-007 — Docker and configuration
Compose services: api, sqlserver, db-init (one-shot), keycloak. SQL data persisted in a named volume. Health checks and startup ordering ensure schema initialization completes before API is ready. Keycloak readiness and token availability verified during deployment validation. Multi-stage API Dockerfile, non-root runtime where supported. Use .env.example with placeholders, .gitignore for .env, configuration via environment variables. Pin compatible image/package versions during setup; document local debug configuration and commands. No external cloud account needed.

## ADR-008 — Quality and delivery
xUnit, Moq for application ports when useful, Coverlet for unit coverage, Testcontainers for targeted SQL Server integration tests. TDD sequence: failing test, minimal implementation, refactor. Test business rules, handlers, DB constraints, rollback, concurrent exits, category deletion races, CRUD and authentication. GitHub Actions builds and runs tests when the planned workflow is implemented; integration tests need Docker. Preserve actual test results and coverage, do not fabricate metrics. Follow assessment-specific formatting, English identifiers, one class/enum per file, no commented-out code, short single-purpose methods. README includes setup, debugging, OAuth token examples and Swagger. Document AI prompts and real candidate decisions separately.

## ADR-009 — Project setup and OpenAPI
Use Visual Studio 2026 as the primary IDE, SDK 10.0.404 as reported, global.json to select the .NET 10 SDK family, and the SLNX solution format. Centralize package versions using Directory.Packages.props and shared build settings using Directory.Build.props. Use Swashbuckle.AspNetCore 11.0.1 as the only OpenAPI generator: Microsoft.AspNetCore.OpenApi 10.0.15 was removed after its Microsoft.OpenApi dependency range conflicted with Swashbuckle. Candidate reported successful build, template tests and local Swagger verification after the correction. Initial setup commit c0669d1 was subsequently reviewed on GitHub.

## ADR-010 — Domain model and invariants
Status: Implemented locally, with explicit persistence restoration and EF Core materialization verified during Phase 5.

Use a behavior-oriented domain model, with private setters and explicit operations instead of public mutation. Category starts active, validates and normalizes name and optional description, supports updates and logical deactivation. Product starts active with zero stock, validates SKU, name, optional description and positive CategoryId, supports updates and logical deactivation, and exposes RegisterEntry and RegisterExit as the only public stock-changing operations. Inactive entities reject updates; inactive products reject movements. Product deactivation requires zero stock. Repeated domain deactivation is idempotent; the HTTP contract for inactive resources remains 404.

Product checks positive quantities, insufficient stock and overflow before modifying stock. Updates validate all incoming values before assigning any properties. Required text is trimmed and must be nonblank; optional text is trimmed and whitespace-only values become null. Length limits stay in each entity. Database uniqueness and active-category membership require persistence checks, not just domain validation.

InventoryMovement has no public mutation methods. Its product ID and quantity must be positive; the byte-backed MovementType allows Entry=1 and Exit=2 only.

The constructor requires DateTimeKind.Utc and an explicit timestamp. Application now provides this timestamp through TimeProvider.GetUtcNow().UtcDateTime rather than accepting client-controlled creation dates.

EF Core maps CreatedAt to datetime2(7) and restores DateTimeKind.Utc during materialization. Dapper explicitly binds movement timestamps using DbType.DateTime2 with scale 7.

Category.Restore and Product.Restore reconstruct persisted entities without exposing public setters. Restoration preserves identity and active status. Product restoration additionally preserves stock and rejects invalid persisted stock states.

Private parameterless constructors remain available for EF Core materialization.

Product protects current stock and InventoryMovement records the operation. Infrastructure coordinates both responsibilities atomically through its transactional write repository.

## ADR-011 — Domain refactoring and business-rule errors
Status: Implemented locally and regression-tested.

Extract internal static TextValidation for required and optional text normalization. Keep entity-specific restrictions and behavior in entities; no base entity class or generic repository is introduced merely to remove superficial duplication.

Use a sealed ProductDetails record as a parameter object for the Product constructor and UpdateDetails. Product still owns validation; ProductDetails is not a validated value object. ApplyDetails shares validation and assignment logic, rejects null and prevents partial mutation. Keep simpler Category and InventoryMovement constructor signatures.

Use DomainRuleException with DomainRuleCode to distinguish known state conflicts from unexpected runtime errors. InactiveCategory and InactiveProduct are intended to map to 404 for inactive resources; InsufficientStock, StockLimitExceeded and ProductHasStock map to 409. Application/API must determine the appropriate response in context, including validation of referenced categories. Argument exceptions remain for invalid domain arguments; request validation should produce 400 without globally treating arbitrary internal argument exceptions as client errors. Unknown or unexpected errors must not be silently mapped to business conflicts. No HTTP dependencies are introduced into Domain.

## ADR-012 — Domain testing and analyzer convention
Status: Local validation reported on 2026-11-25.

84 domain unit test cases (20 Category, 51 Product, 13 InventoryMovement) passed, with zero failures and skips. The inspected 84-case Coverlet report measured 94.94% line coverage and 100.00% branch coverage for InventoryManagement.Domain, exceeding the internal 85% objective. Nine uncovered lines were consistent with the three private parameterless constructors. Do not add reflection-only tests or coverage exclusions solely to improve the metric. Persistence materialization requires integration tests.

Use descriptive Method_Scenario_ExpectedBehavior test names. The CA1707 exception is scoped to test source files via tests/.editorconfig; production analyzers remain enabled. The final local test command completed with 84 passing cases and no warnings. Its new coverage XML was not inspected; the reported percentages are from the preceding 84-case run.

## ADR-013 — SQL Server initialization and permissions

Status: Implemented and locally verified.

Use SQL Server 2022 Developer Edition for local development and integration testing.

Pinned image:

mcr.microsoft.com/mssql/server:2022-CU28-ubuntu-22.04

The database uses Latin1_General_100_CI_AS_SC collation for case-insensitive unique values.

Database initialization is organized into three versioned scripts:

- 001_create_database.sql
- 002_create_schema.sql
- 003_create_permissions.sql

A one-shot db-init container executes the scripts after SQL Server becomes healthy.

Repeated initialization preserves existing data. These scripts initialize the current schema but do not implement general schema migration or drift detection.

The inventory_app account receives SELECT and INSERT permissions and restricted column-level UPDATE permissions.

Physical deletion of categories, products and inventory movements is not granted. Inventory movement updates are not granted.

Additional database constraints reject blank required values, negative stock and inactive products with nonzero stock.

The application account is not a member of sysadmin or db_owner.

Database permissions do not independently enforce every business rule. Stock changes and active-category relationships rely on the application persistence protocol.

SQL Server listens internally on port 1433. Local development access is bound to 127.0.0.1:14330.

A named volume preserves database files. Container recreation and repeated initialization were verified without losing the test record.


## ADR-014 — Persistence implementation

Status: Implemented and locally integration-tested.

Application defines five narrow persistence contracts:

- ICategoryReadRepository
- IProductReadRepository
- ICategoryWriteRepository
- IProductWriteRepository
- IInventoryMovementWriteRepository

Infrastructure implements these contracts without a generic repository or generic Unit of Work.

InventoryDbContext uses explicit EF Core entity configurations.

Read repositories use AsNoTracking, active-record filtering, DTO projections and deterministic ID ordering.

PaginationHelper centralizes count, offset and page retrieval. Offset arithmetic uses long to prevent integer overflow. Requests beyond the available record count return an empty page.

Page validation is implemented in Application through PageRequestValidator. Both category and product query handlers validate pagination before invoking their read repositories.

Count and page retrieval are separate database operations and do not guarantee snapshot consistency under concurrent writes.

Dapper write repositories use parameterized SQL. Persisted entities are explicitly reconstructed before applying domain behavior.

SQL uniqueness violations are translated into identifiable persistence-rule exceptions using SQL error numbers and stable constraint names.

Unknown SQL errors remain unexpected infrastructure failures.


## ADR-015 — Database connections and dependency injection

Status: Implemented and locally integration-tested.

SqlServerConnectionString centralizes environment-based database configuration through SqlConnectionStringBuilder.

Database connections require encryption. TrustServerCertificate defaults to false and may be enabled explicitly for local development.

SqlConnectionFactory is registered as Singleton and creates independent SQL connections for individual operations.

InventoryDbContext and persistence repositories are registered as Scoped.

AddInventoryPersistence centralizes persistence dependency registration.

ApplicationServiceRegistration and AddInventoryPersistence have been verified together through dependency composition tests.

Program.cs integration was completed and locally verified during Phase 7.

The local .env file is not automatically loaded by ASP.NET Core. Docker Compose and local API debugging must provide the required configuration explicitly.


## ADR-016 — Persistence verification and quality baseline

Status: Local verification completed on 2026-11-26.

Testcontainers provides isolated SQL Server instances using the same SQL Server image and initialization scripts as local development.

Integration tests verify EF Core mapping, read repositories, connection configuration, Dapper repositories, transaction rollback, concurrency, physical database constraints and application account permissions.

SQL integration test classes execute sequentially to limit container resource consumption. Individual concurrency tests execute independent database operations concurrently.

The concurrent exit test explicitly observes SQL Server lock contention before releasing a control transaction.

The remaining concurrency tests coordinate task startup but do not guarantee a specific SQL execution interleaving.

Final local Release results:

- 95 domain unit tests passed.
- 96 integration project tests passed.
- 191 total tests passed.
- Zero failures and skipped tests.
- Zero Release build warnings.
- Domain line coverage: 96.31%.
- Domain branch coverage: 100.00%.
- Infrastructure line coverage: 97.54%.
- Infrastructure branch coverage: 90.62%.
- Seven concurrency cases passed in ten additional runs.

Coverage values are obtained from separate assembly-level reports, not combined solution coverage.

At the Phase 5 milestone, Application.Tests did not contain business use case tests. Phase 6 subsequently introduced 142 Application unit test cases.

These results are based on candidate-executed local tests and supplied command output, not independent assistant execution.

## ADR-017 — Explicit application CQRS handlers

Status: Implemented and locally unit-tested during Phase 6.

Application organizes use cases by functional module: Categories, Products and InventoryMovements.

Use explicit typed contracts:

- `IQueryHandler<TQuery, TResult>`
- `ICommandHandler<TCommand, TResult>`
- `ICommandHandler<TCommand>`

Queries return read models or paginated results.

Creation commands and inventory movement registration return the generated identifier. Update and logical deletion commands do not return a result.

Eleven handlers implement the approved functional use cases.

No mediator library, generic dispatcher, service locator or custom messaging pipeline is introduced.

Handlers depend on persistence abstractions rather than EF Core, Dapper, SQL Server or ASP.NET Core.

Application coordinates use cases while Domain protects business invariants and Infrastructure coordinates transactional persistence.

Product and category existence checks, uniqueness checks and state-dependent business rules remain protected by the transactional persistence protocol rather than redundant application-level prechecks.

Get-by-ID handlers translate unavailable read results into the existing identifiable not-found exception contract.

Known domain and persistence exceptions propagate to the future API exception handler.

Cancellation tokens are forwarded to persistence operations.


## ADR-018 — Application input validation

Status: Implemented and locally unit-tested.

Use FluentValidation core with explicit asynchronous invocation through ValidateAndThrowAsync.

Handlers validate requests before calling persistence dependencies.

PageRequestValidator is shared by category and product pagination queries.

CategoryValidationRules centralizes category input rules.

ProductValidationRules centralizes product input rules, including identifiers reused by inventory movement registration.

Required text must be nonblank. Length validation uses trimmed input and reuses maximum length constants defined in Domain.

Optional descriptions may be null or whitespace.

CascadeMode.Stop prevents redundant validation failures within the same required-text rule.

Independent properties continue to be validated so multiple input errors can be reported together.

Domain remains responsible for effective normalization and entity invariants.

No database-backed validation prechecks, custom validation pipeline or validator inheritance hierarchy is introduced.

HTTP error mapping remains an API responsibility.


## ADR-019 — Inventory movement timestamp generation

Status: Implemented and locally unit-tested.

RegisterInventoryMovementCommand contains ProductId, Type and Quantity.

Movement type validation explicitly permits Entry and Exit.

Application receives the native .NET TimeProvider abstraction.

TimeProvider.GetUtcNow().UtcDateTime supplies the movement timestamp.

The timestamp represents movement construction time in Application, not necessarily the exact SQL transaction commit time.

The client cannot supply the movement identifier, timestamp or resulting stock through the command.

Unit tests use a controlled TimeProvider mock to verify exact timestamp ticks and DateTimeKind.Utc.

Invalid or previously cancelled commands do not invoke the clock or persistence repository.

The movement repository retains responsibility for atomic stock updates and movement insertion.


## ADR-020 — Application dependency composition

Status: Registration implemented and locally verified through composition tests.

The API project owns ApplicationServiceRegistration and its AddInventoryApplication extension method.

This keeps Application independent of dependency injection container packages.

Registrations are explicit rather than assembly-scanned.

Service lifetimes:

- Eleven CQRS handlers: Scoped.
- Ten FluentValidation validators: Scoped.
- TimeProvider.System: Singleton.
- Persistence repositories: Scoped.
- InventoryDbContext: Scoped.
- SqlConnectionFactory: Singleton.

Application and persistence registration are verified together using ValidateOnBuild and ValidateScopes.

Twenty-two composition test cases verify implementation types, registration uniqueness and service lifetimes.

Composition tests do not open SQL Server connections.

Program.cs registration, actual HTTP request processing and host-level validation were completed during Phase 7. See ADR-021 and ADR-025.

## ADR-021 — REST API and host composition

Status: Implemented and locally verified during Phase 7.

ASP.NET Core controllers expose eleven business endpoints under /api/v1.

Controllers receive typed application handlers through constructor injection. They do not access SQL Server, EF Core or Dapper directly.

HTTP request contracts are separate from domain entities and application commands.

Request DTOs reject unmapped JSON members to prevent clients from supplying unsupported properties, including stock and active state.

Application read models are reused as response contracts.

Category and product creation return HTTP 201 with a generated identifier and a Location header.

Inventory movement registration returns HTTP 201 with the generated identifier and no Location header.

Updates and logical deletions return HTTP 204 without a response body.

Movement type parsing accepts Entry and Exit using exact case-sensitive string matching.

Program.cs is the composition root and registers controllers, Application, Infrastructure, error handling, Swagger and authentication services.

The request pipeline invokes authentication before authorization.

## ADR-022 — Centralized HTTP error handling

Status: Implemented and locally verified.

Use the native ASP.NET Core IExceptionHandler abstraction.

GlobalExceptionHandler delegates exception translation to ExceptionProblemDetailsMapper.

Application validation failures become HTTP 400 responses with field-level validation errors.

Known missing-resource exceptions become HTTP 404.

Known uniqueness and business-rule conflicts become HTTP 409.

Unexpected exceptions become HTTP 500 with a generic public message.

Invalid model binding and otherwise empty HTTP error responses are handled separately through HttpErrorResponses.

Error responses include a stable application code, trace identifier and request path.

The application does not expose internal exception messages, SQL details or stack traces to consumers.

Expected business failures do not require the same diagnostic treatment as unexpected server failures.

## ADR-023 — OAuth2 resource server and Keycloak

Status: Implemented and verified against local Keycloak. Complete container networking verification remains pending.

Use Keycloak 26.8.2 as the OAuth2 authorization server.

Realm: inventory.

Clients:

- inventory-api: resource-server audience.
- inventory-swagger: public Authorization Code client with mandatory PKCE S256.
- inventory-service: confidential Client Credentials client.

The API requires the inventory.access scope for every business endpoint.

JWT Bearer validates the token signature, RS256 algorithm, exact issuer, audience, expiration and validity period.

Audience validation does not ignore trailing slash differences.

Scope comparison is case-sensitive and matches complete space-delimited scope values.

The access token lifetime is 300 seconds. JWT validation uses a 30-second clock skew.

The API uses separate KEYCLOAK_AUTHORITY and KEYCLOAK_ISSUER settings to support different metadata and public issuer addresses without relaxing issuer validation.

HTTP metadata is accepted only in Development.

Required configuration values and invalid authentication URLs are rejected during service registration.

The local Keycloak instance uses development mode and an embedded database. Its named volume preserves realm data across container recreation.

Existing imported realms are not overwritten automatically.

Bootstrap administration, demonstration users and HTTP endpoints are restricted to the local evaluation environment.

Production deployment requires HTTPS, production identity-provider configuration, appropriate administrator provisioning and credential management.

The current shared scope is not a fine-grained role-based authorization system.

## ADR-024 — OpenAPI and interactive authorization

Status: Implemented and locally verified.

Use Swashbuckle.AspNetCore 11.0.1.

OpenAPI describes the Authorization Code flow and required inventory.access scope.

A global security requirement is appropriate because all current business endpoints use the same authorization policy.

Swagger UI uses the public inventory-swagger client and enables PKCE.

No confidential client secret is provided to Swagger UI.

Swagger authorization is not persisted across page reloads by an application-specific persistence configuration.

Swagger UI and the OpenAPI endpoint are exposed only in Development.

Controller metadata describes applicable successful responses and ProblemDetails errors, including HTTP 401, 403 and 500.

Swagger UI initialization tests inspect both the HTML document and its generated index.js script.

The OpenAPI document does not replace server-side validation or authorization.

Additional schema metadata could improve client generation and communicate normalized-input restrictions more precisely.

## ADR-025 — API integration testing and quality baseline

Status: Locally verified on 2026-11-27.

Testing is organized into complementary levels:

- Domain unit tests.
- Application unit tests with real validators and mocked persistence ports.
- SQL Server integration tests using Testcontainers.
- Dependency composition tests.
- HTTP controller tests using isolated TestServer hosts.
- JWT authentication and authorization tests using controlled RSA keys.
- OpenAPI and Swagger UI documentation tests.
- Complete API host integration tests using WebApplicationFactory.
- Manual smoke checks against actual Keycloak.
- Manual functional verification through Swagger and SQL Server.

Complete-host integration tests execute the actual API entry point in Production.

They use real application services and persistence implementations with isolated SQL Server instances.

Only configuration and OpenID Connect metadata are controlled by the test infrastructure. JWT validation and authorization policies remain active.

The local Keycloak authentication flow is verified separately rather than starting an identity-provider container for every automated test.

Local final regression:

- Domain.Tests: 95 passed.
- Application.Tests: 142 passed.
- IntegrationTests: 293 passed.
- Total: 530 passed.
- Failed: 0.
- Skipped: 0.

The final integration coverage run reported:

| Assembly | Line coverage | Branch coverage |
|---|---:|---:|
| API | 99.43% | 89.23% |
| Application | 100.00% | 88.46% |
| Domain | 86.50% | 75.00% |
| Infrastructure | 97.54% | 90.62% |
| Aggregate | 97.60% | 87.67% |

The coverage report represents 293 integration tests, not merged coverage of the complete test suite.

The three uncovered API lines concern the Development-only host branch and generic HTTP error mapping.

The Development-only Swagger path was manually exercised through Visual Studio, and Swagger service configuration was verified through isolated automated tests.

Additional tests were not introduced solely to achieve 100% coverage.

The candidate executed the tests locally. The assistant inspected the supplied terminal output and Cobertura XML without independently executing the code.

## Repository layout

- src/InventoryManagement.Domain
- src/InventoryManagement.Application
  - Abstractions/Messaging
  - Abstractions/Persistence
  - Categories
  - Products
  - InventoryMovements
  - Validation
  - Models
  - Exceptions
- src/InventoryManagement.Infrastructure
  - Persistence/Configurations
  - Persistence/Connections
  - Persistence/Models
  - Persistence/Pagination
  - Persistence/Repositories/Read
  - Persistence/Repositories/Write
- src/InventoryManagement.Api
  - Configuration
  - Contracts
  - Controllers
  - ErrorHandling
- tests/InventoryManagement.Domain.Tests
- tests/InventoryManagement.Application.Tests
- tests/InventoryManagement.IntegrationTests
  - Composition
  - ErrorHandling
  - Fixtures
  - Persistence
  - Http/Authentication
  - Http/Categories
  - Http/Products
  - Http/InventoryMovements
  - Http/Documentation
  - Http/EndToEnd
- database/scripts
- database/init.sh
- infrastructure/keycloak/inventory-realm.json
- docs/

Root configuration includes .env.example, .gitignore, .gitattributes, docker-compose.yml, README.md and central SDK/build/package settings.

Planned additions:

- API Dockerfile and Docker build exclusions.
- Complete API service configuration in Docker Compose.
- GitHub Actions workflow.
- Candidate decision-making answers in docs/DECISIONS.md.

## Pending validation

The Phase 4 domain and Phase 5 persistence commits were statically reviewed on GitHub.

The Phase 6 application implementation was statically reviewed after publication of commit cc6b32f7c57db6793b86584f04f11ba62acb030b.

Phase 7 introduced REST endpoints, centralized error handling, OAuth2, Swagger and complete-host integration tests.

The candidate reported successful local execution of all 530 automated tests.

Manual verification covered CRUD operations, inventory entries and exits, logical deletion, persisted movement history and stock reconciliation.

The Phase 7 source changes remain subject to static review after publication of the corresponding commit.

Remaining work:

- API container image and full Docker Compose deployment.
- Clean-start verification and service startup ordering.
- Container-to-Keycloak metadata and signing-key retrieval.
- Complete containerized authentication smoke checks.
- Final OpenAPI contract review.
- CI/CD.
- Candidate decision-making answers.
- Final deployment and delivery documentation.

The measured test coverage and successful local checks do not establish complete production readiness.
