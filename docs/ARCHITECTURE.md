# Inventory Management API — Architecture

Status: Candidate-approved architecture baseline v0.2, with domain implementation update v0.3, 2026-11-25. Implementation in progress.
Functional baseline: SPEC.md v0.3 (candidate-approved scope; architecture references updated).

## ADR-001 — Runtime and architecture
Use .NET 10 LTS and EF Core 10 (matching major versions), ASP.NET Core controllers, SQL Server 2022 and a modular monolith with Clean Architecture. The assessment permits .NET 8 or higher; .NET 8 reached end of support on 2026-11-10. .NET 10 is supported until November 2028. Reference: https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core

Dependency direction (compile-time): Application -> Domain; Infrastructure -> Application and Domain; API -> Application and Infrastructure (composition root). Domain has no infrastructure dependencies. Test projects reference the relevant production projects. No microservices or separate read database.

## ADR-002 — CQRS and persistence
Use explicit commands, queries and typed handlers registered with built-in DI; no mediator library initially. Controllers delegate to handlers. Application defines narrow read and write ports; Infrastructure implements them. EF Core uses AsNoTracking, DTO projections and deterministic pagination. Dapper executes parameterized SQL for all application writes. No generic repository or EF SaveChanges for business writes. Use versioned SQL initialization scripts, not competing EF migrations. The database initializer applies scripts before API readiness; repeated startup must preserve data.

## ADR-003 — Relational model
All IDs are int IDENTITY primary keys. Categories: Name nvarchar(150) NOT NULL UNIQUE, Description nvarchar(500) NULL, IsActive bit NOT NULL DEFAULT 1. Products: CategoryId int NOT NULL FK, SKU nvarchar(50) NOT NULL UNIQUE, Name nvarchar(150) NOT NULL, Description nvarchar(500) NULL, Stock int NOT NULL DEFAULT 0 CHECK (Stock >= 0), IsActive bit NOT NULL DEFAULT 1. InventoryMovements: ProductId int NOT NULL FK, Type tinyint NOT NULL CHECK (Type IN (1,2)), Quantity int NOT NULL CHECK (Quantity > 0), CreatedAt datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(). Entry=1, Exit=2 internally; API accepts strings Entry/Exit only. Explicit case-insensitive collation on unique strings; trim at input boundary. Unique values stay reserved after logical deletion. No cascading deletes. Index Products(CategoryId, IsActive) and InventoryMovements(ProductId, CreatedAt). Use UTC timestamps and serialize as UTC. Add DB checks for nonblank names/SKU. Map SQL uniqueness conflicts to HTTP 409.

## ADR-004 — Transaction and concurrency
Use one open SQL connection and one transaction per movement. Acquire a product row update lock before inspecting current state, validate active status and stock with overflow-safe arithmetic, update stock and insert movement using Dapper, then commit. Roll back on failure. Conditional SQL update with stock bounds is a defense in depth. Return new movement identifier. Product deletion takes the same product lock and requires stock zero. Product creation/category reassignment and category deletion serialize on the affected category row via UPDLOCK/HOLDLOCK within a transaction; lock affected category rows in ascending ID order before product rows when both are needed, revalidate relevant state after locking, and handle deadlock errors with bounded retry of the full transaction when safe. A category delete checks active products while holding its category lock. All participating write paths must follow the protocol. Do not read stock via EF outside the write transaction to decide whether a movement is allowed. Integration tests must prove rollback and concurrent operations; unit tests alone cannot prove SQL behavior.

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
Status: Initial domain implementation completed locally; persistence integration pending.

Use a behavior-oriented domain model, with private setters and explicit operations instead of public mutation. Category starts active, validates and normalizes name and optional description, supports updates and logical deactivation. Product starts active with zero stock, validates SKU, name, optional description and positive CategoryId, supports updates and logical deactivation, and exposes RegisterEntry and RegisterExit as the only public stock-changing operations. Inactive entities reject updates; inactive products reject movements. Product deactivation requires zero stock. Repeated domain deactivation is idempotent; the HTTP contract for inactive resources remains 404.

Product checks positive quantities, insufficient stock and overflow before modifying stock. Updates validate all incoming values before assigning any properties. Required text is trimmed and must be nonblank; optional text is trimmed and whitespace-only values become null. Length limits stay in each entity. Database uniqueness and active-category membership require persistence checks, not just domain validation.

InventoryMovement has no public mutation methods. Its product ID and quantity must be positive; the byte-backed MovementType allows Entry=1 and Exit=2 only. The constructor requires DateTimeKind.Utc and an explicit timestamp supplied by the application, rather than reading the system clock itself. The API must not accept client-controlled creation timestamps. The future persistence mapping must preserve UTC semantics when reading SQL datetime2 values.

Product and InventoryMovement have separate responsibilities: one protects current stock and the other records the operation. A future transactional write implementation must coordinate them atomically. Persisted product identity, stock and active state must be faithfully restored before applying a write rule; the new-product constructor must not be used to reconstruct an existing product as if its stock were zero. The exact restoration mechanism remains to be implemented and tested. Private parameterless constructors are retained as materialization candidates, not evidence of working ORM mapping.

## ADR-011 — Domain refactoring and business-rule errors
Status: Implemented locally and regression-tested.

Extract internal static TextValidation for required and optional text normalization. Keep entity-specific restrictions and behavior in entities; no base entity class or generic repository is introduced merely to remove superficial duplication.

Use a sealed ProductDetails record as a parameter object for the Product constructor and UpdateDetails. Product still owns validation; ProductDetails is not a validated value object. ApplyDetails shares validation and assignment logic, rejects null and prevents partial mutation. Keep simpler Category and InventoryMovement constructor signatures.

Use DomainRuleException with DomainRuleCode to distinguish known state conflicts from unexpected runtime errors. InactiveCategory and InactiveProduct are intended to map to 404 for inactive resources; InsufficientStock, StockLimitExceeded and ProductHasStock map to 409. Application/API must determine the appropriate response in context, including validation of referenced categories. Argument exceptions remain for invalid domain arguments; request validation should produce 400 without globally treating arbitrary internal argument exceptions as client errors. Unknown or unexpected errors must not be silently mapped to business conflicts. No HTTP dependencies are introduced into Domain.

## ADR-012 — Domain testing and analyzer convention
Status: Local validation reported on 2026-11-25.

84 domain unit test cases (20 Category, 51 Product, 13 InventoryMovement) passed, with zero failures and skips. The inspected 84-case Coverlet report measured 94.94% line coverage and 100.00% branch coverage for InventoryManagement.Domain, exceeding the internal 85% objective. Nine uncovered lines were consistent with the three private parameterless constructors. Do not add reflection-only tests or coverage exclusions solely to improve the metric. Persistence materialization requires integration tests.

Use descriptive Method_Scenario_ExpectedBehavior test names. The CA1707 exception is scoped to test source files via tests/.editorconfig; production analyzers remain enabled. The final local test command completed with 84 passing cases and no warnings. Its new coverage XML was not inspected; the reported percentages are from the preceding 84-case run.

## Repository layout
- src/InventoryManagement.Domain (Entities, Enums, Exceptions, Parameters, Validation)
- src/InventoryManagement.Application
- src/InventoryManagement.Infrastructure
- src/InventoryManagement.Api
- tests/.editorconfig
- tests/InventoryManagement.Domain.Tests/Entities
- tests/InventoryManagement.Application.Tests
- tests/InventoryManagement.IntegrationTests
- docs/ (SPEC.md, ARCHITECTURE.md, AI_USAGE.md)

Planned additions, not yet implemented:

- database/ (versioned SQL scripts)
- infrastructure/keycloak/ (realm configuration)
- docs/DECISIONS.md (assessment decision-making answers)
- .github/workflows/ci.yml
- Dockerfile and docker-compose.yml

Root configuration includes .env.example, .gitignore, README.md and central SDK/build/package settings.

## Pending validation
The initial domain implementation and its unit tests have been validated locally. The phase 4 code has not yet been reviewed on the remote repository. Application handlers, persistence mappings, SQL initialization, transaction and concurrency protocol, container image versions, OAuth issuer/backchannel behavior, API endpoints, integration tests and clean-start verification remain pending. Domain coverage does not establish system-wide correctness.
