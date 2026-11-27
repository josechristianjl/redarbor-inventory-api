# AI Usage Log

Status: Updated through local container deployment, persistence, recovery and security validation, Phase 8. Do not represent proposed decisions as implemented or verified. This log is a retrospective summary of the development conversation, not an independently timestamped transcript or test-run archive.

## Session 1 — Assessment review and specification (2026-11-23)
Tool: ChatGPT.

### Prompt summary
- Review the supplied Redarbor technical assessment and evaluate difficulty, risks and an appropriate solution approach (original user prompt: "Como lo vez?").
- Begin with the analysis and specification phase, prioritizing prompt delivery while noting there is no fixed deadline (original user prompt: "Me parece bien, empecemos con primero. Para aclarar, la prueba en si no tienen un fecha especifica de entrega, pero si lo mas pronto posible.").

### Evidence of specification-first development
The assistant drafted SPEC.md v0.1 before application code was generated. It distinguishes explicit assessment requirements from proposed business rules and defines acceptance criteria. Candidate subsequently approved the specification in Session 2. The initial repository commit contains the specification and architecture before business implementation.

### Initial AI proposals
- Pragmatic Clean Architecture with CQRS over a single SQL Server database.
- EF reads and Dapper writes, with atomic inventory updates and movement inserts.
- Keep inventory movements immutable and preserve historical data with logical deletion.
- Use a standards-compliant OAuth2 provider rather than treating JWT generation alone as OAuth2.
- Test inventory invariants, rollback and concurrent stock changes.
- Avoid microservices, messaging and unrelated functionality.

### Candidate decisions and validation
The candidate agreed to begin with specification-first development. Subsequent approvals are recorded below.

## Session 2 — Candidate approval and architecture (2026-11-24)
Candidate explicitly approved the preceding specification and scope: "Estoy de acuerdo con todo esto. Pasemos a lo siguiente". SPEC.md became v0.2; the published baseline is v0.3. No individual rationale beyond agreement was supplied; do not attribute invented reasons to the candidate.

AI proposed ARCHITECTURE.md v0.1, including .NET 10 LTS (superseding initial .NET 8 suggestion after checking Microsoft support dates), explicit CQRS handlers without MediatR, narrow persistence ports, Dapper transactional locking, SQL initialization scripts, Keycloak OAuth2 with PKCE and client credentials, and focused unit/integration tests. Candidate subsequently approved the architecture: "Estoy de acuerdo. Pasemos a la fase 3 entonces." No code or tests were executed during architecture design.

## Session 3 — Project setup (2026-11-24 to 2026-11-25)
Tool: ChatGPT.

### Prompt summary
- Continue with phase 3 and configure the development environment, solution and project references.
- Explain why setup was being performed through the terminal and adapt the instructions to Visual Studio.
- Review the project files supplied by the candidate and centralize the actual NuGet versions.
- Diagnose the reported Microsoft.OpenApi version conflict.

### Candidate decisions and AI proposals
- Candidate chose to create the GitHub repository manually and create the solution again from Visual Studio 2026 rather than use the CLI. The assistant adapted the procedure and proposed retaining SLNX.
- Assistant proposed shared build settings, EditorConfig, environment variable template and Central Package Management. Candidate followed setup instructions and provided project files.
- Assistant initially proposed keeping both OpenAPI generators. Candidate reported a build failure: Swashbuckle 11.0.1 required Microsoft.OpenApi >= 3.11.0, while Microsoft.AspNetCore.OpenApi 10.0.15 required >= 2.13.0 and < 3.0.0. Assistant corrected its proposal by removing Microsoft.AspNetCore.OpenApi from the API project and central versions, and replacing AddOpenApi/MapOpenApi with Swagger registration and middleware. Candidate reported successful build and tests after the change. No forced transitive version override was used in the proposed fix.

### Verification and limitations
- Candidate reported Docker validation, .NET 10 SDK 10.0.404, successful build and successful template tests after the dependency correction, and local Swagger verification. These are candidate-reported results, not independently executed by the assistant.
- Template tests are not evidence of business-rule coverage or TDD.
- Initial Git Changes showed 922 entries. Candidate ran read-only Git commands to identify generated bin/obj files, amended ignore rules, and confirmed the reduction to 22 changes before committing. The exact cause of the earlier inconsistent ignore diagnostics was not established.
- Candidate published initial commit c0669d1b128dc645189bcfda17b6cf0d95eaaa1f. The assistant inspected the remote commit and key configuration/documentation files on request; that review was static, not a remote build or test execution. No business code had been implemented at that milestone.

## Session 4 — Domain implementation, TDD and refactoring (2026-11-25)
Tool: ChatGPT.

### Main prompts and collaboration
Original candidate request to begin this phase: "Empecemos la fase 4 entonces". Subsequent messages confirmed local RED and GREEN runs and requested continuation through the agreed sequence. Examples of actual confirmations: "Confirmo red y grren de product", "Confírmo los resultados de las 80 pruebas en RED y GREEN", and "Confírmo que las 84 pruebas pasan correctamente." The candidate also supplied actual PowerShell test and coverage output, including a discrepancy between 82 and 84 tests and the CA1707 analyzer warnings.

The assistant proposed the test cases, minimal compilable RED implementations, GREEN implementations, explanations and incremental refactorings. The candidate created or edited the files in Visual Studio and ran the tests locally. The assistant did not independently execute these tests. Proposed technical choices are described below; the candidate's continuation and test confirmations are evidence of the workflow, not proof of a separately stated personal rationale for every design choice.

### AI proposals, implementation decisions and review

| Proposal | Outcome and rationale |
|---|---|
| Encapsulate state in Category, Product and InventoryMovement | Implemented locally. Public operations protect invariants and keep database dependencies outside Domain. |
| Normalize required and optional strings | Implemented; trim inputs, reject invalid required values, normalize blank optional descriptions to null. The latter is a design convention added by the assistant. |
| Validate all update arguments before mutation | Implemented to avoid partially changed entities after validation errors. |
| Reject updates on inactive entities; make domain deactivation idempotent | Implemented as a design refinement. HTTP handling for inactive resources remains separate. |
| Use positive quantities, zero initial stock and overflow-safe arithmetic | Implemented and covered by unit tests. Persistence concurrency and atomicity remain unverified. |
| Use byte-backed MovementType and require explicit UTC timestamp | Implemented; the timestamp is intended to come from Application rather than the client or a clock inside the entity. SQL UTC mapping is pending. |
| Extract shared TextValidation | Implemented after adding two regression cases for excessive SKU/name length on Product updates. Avoided a base class or unnecessary interface. |
| Replace generic state-conflict exceptions | Implemented DomainRuleException and DomainRuleCode; tests assert codes instead of message text. HTTP mapping is not implemented. |
| Introduce ProductDetails | Implemented a parameter record and shared ApplyDetails logic for construction and updates; retained entity-owned validation. Added two null argument regression cases. |
| Keep private materialization constructors without artificial coverage tests | Retained; the persistence mapping and restoration of existing product stock must be tested later. |

### TDD evidence (candidate-confirmed local execution)

The RED/GREEN counts below are the results reported or confirmed in the conversation, not independently captured historical test logs. The test scenarios were proposed before their GREEN implementations; temporary compilable stubs enabled the RED runs. The exception-contract cycle changes existing test assertions rather than increasing the test count.

| Cycle | RED passed | RED failed | GREEN passed | GREEN failed |
|---|---:|---:|---:|---:|
| Category constructor | 1 | 9 | 10 | 0 |
| Category update and deactivation | 10 | 10 | 20 | 0 |
| Product constructor | 21 | 15 | 36 | 0 |
| Product update and deactivation | 36 | 14 | 50 | 0 |
| Product inventory operations | 50 | 17 | 67 | 0 |
| InventoryMovement constructor | 70 | 10 | 80 | 0 |
| Domain rule exception contract | 74 | 8 | 82 | 0 |

Between the sixth and seventh cycles, two regression cases were added before extracting TextValidation; the candidate confirmed 82 passing cases after refactoring. The ProductDetails refactor retained those 82 cases and added two null-argument regression cases. These four additions are not represented as extra RED/GREEN cycles because their behavior was already implemented when tested.

### Corrections and measured verification

- First coverage command executed 82 cases rather than the expected 84 and reported 57 CA1707 warnings. The assistant identified the two missing null-details tests as the likely discrepancy; the subsequent output listed both and confirmed 84 cases.
- The first proposed root EditorConfig rule did not resolve the CA1707 warnings; the 84-case coverage run showed 59 warnings, all associated with descriptive test method identifiers containing underscores. The assistant proposed a test-directory EditorConfig with only CA1707 disabled for test source files, retaining production analyzer settings. The final local test execution completed without reported warnings.
- One coverage extraction command initially failed because formatting tags were copied along with PowerShell. The assistant supplied a plain code block and the candidate subsequently obtained the project, class and uncovered-line results.

Command used:

```sh
dotnet test tests/InventoryManagement.Domain.Tests/InventoryManagement.Domain.Tests.csproj --collect:"XPlat Code Coverage" --results-directory "./TestResults/DomainCoverage"
```

Measured domain coverage from the inspected 84-case run:

| Class | Line coverage | Branch coverage |
|---|---:|---:|
| TextValidation | 100.00% | 100.00% |
| ProductDetails | 100.00% | 100.00% |
| DomainRuleException | 100.00% | 100.00% |
| Category | 90.32% | 100.00% |
| InventoryMovement | 92.10% | 100.00% |
| Product | 96.15% | 100.00% |
| InventoryManagement.Domain | 94.94% | 100.00% |

The nine uncovered lines were Category.cs 15–17, InventoryMovement.cs 13–15 and Product.cs 20–22 in that local snapshot, consistent with private parameterless constructors. No artificial reflection tests or coverage exclusions were proposed to inflate the result.

Final local command output after the analyzer configuration change: 84 total, 84 passed, 0 failed, 0 skipped, 2.7 seconds test duration, successful compilation in 4.7 seconds, no reported warnings. A new coverage XML was generated but has not been inspected. The coverage table above therefore refers to the preceding 84-case run, not the newly generated XML. The domain test distribution is 20 Category, 51 Product and 13 InventoryMovement cases as designed.

### Remaining limitations
The initial domain is implemented and locally tested, but its latest code has not yet been inspected on GitHub. No application handlers, database persistence, real concurrent transactions, OAuth2 protection, business HTTP endpoints or full Compose deployment have been verified. Domain coverage is not solution-wide coverage. The phase 4 commit and remote code review remain pending at the time this update is prepared. The assessment's separate decision-making answers must reflect the candidate's actual experiences and are not invented here.

## Session 5 — Persistence, transactions and concurrency (2026-11-25 to 2026-11-26)
Tool: ChatGPT.

### Main prompts and collaboration

Continue with the approved persistence phase, including SQL Server configuration, database initialization, EF Core mappings, Dapper repositories, inventory transactions and integration testing.

The candidate implemented the proposed files locally using Visual Studio, managed SQL Server through Docker Desktop and SSMS, and executed test and coverage commands through PowerShell.

The assistant proposed incremental implementations and test scenarios. The candidate reported RED and GREEN results before continuing through subsequent development stages.

Examples of actual candidate confirmations:

- "Confírmo que las 102 pruebas pasan después de la refactorización y que posteriormente las 108 pruebas pasan sin errores ni advertencias."
- "Confírmo los resultados de las 133 y 154 pruebas en RED y GREEN."
- "Confírmo los resultados de las 166 pruebas en RED y GREEN."
- "Confírmo que las 173 pruebas pasan sin errores ni advertencias y que las diez ejecuciones de concurrencia finalizan correctamente."

The candidate also supplied actual Release build, test execution and coverage output.

### AI proposals and implementation decisions

| Proposal | Outcome and rationale |
|---|---|
| SQL Server 2022 with Docker Compose | Implemented and locally verified. Provides reproducible database infrastructure. |
| Versioned SQL initialization scripts | Implemented instead of EF migrations to maintain one explicit schema initialization mechanism. |
| Restricted database account | Implemented inventory_app with application-specific permissions. Verified through integration tests. |
| EF Core DTO projections | Implemented for active-only reads without entity tracking. |
| Shared pagination helper | Extracted after the initial repository tests passed. The candidate confirmed all 102 regression tests passed afterward. |
| Testcontainers integration tests | Implemented using isolated SQL Server instances and shared initialization scripts. |
| Centralized connection string and connection factory | Implemented to provide consistent EF Core and Dapper database configuration. |
| Explicit entity restoration | Implemented Category.Restore and Product.Restore to preserve persisted identity, active state and stock. |
| Narrow Dapper write repositories | Implemented without a generic repository or generic Unit of Work. |
| Ordered transactional locks | Implemented to coordinate product changes, stock movements and category deletion. |
| Atomic stock and movement persistence | Implemented with one connection and transaction. |
| Real SQL constraint failure for rollback testing | Implemented to verify actual SQL Server rollback rather than relying on mocked failures. |
| Additional conditional stock update | Not implemented. The selected implementation uses locked state, domain validation, an affected-row check and database constraints. |
| General deadlock retry policy | Deferred after concurrency verification. Arbitrary SQL errors and uncertain commits are not retried automatically. |
| Development demonstration data | Deferred by the candidate until a later deployment phase. Integration test data remains isolated. |

The technical rationale in this table describes the proposed and implemented design. It should not be interpreted as independently stated personal reasoning from the candidate unless explicitly recorded in the conversation.

### TDD evidence

Candidate-confirmed local executions:

| Cycle | RED passed | RED failed | GREEN passed | GREEN failed |
|---|---:|---:|---:|---:|
| EF Core read repositories | 92 | 10 | 102 | 0 |
| Category restoration | 108 | 4 | 112 | 0 |
| Category write repository | 112 | 14 | 126 | 0 |
| Product restoration | 126 | 7 | 133 | 0 |
| Product write repository | 133 | 21 | 154 | 0 |
| Inventory movement write repository | 154 | 12 | 166 | 0 |

Four EF Core mapping tests, six configuration and connection tests, seven concurrency tests and eighteen database integrity and permission tests were additional verification cases, not separate RED/GREEN cycles.

Phase 5 added 107 test cases, increasing the total from 84 to 191.

The project has thirteen recorded RED/GREEN cycles across the domain and persistence phases.

### Transaction and concurrency verification

The candidate confirmed successful verification of:

- Atomic inventory entry and exit registration.
- Insufficient stock and stock overflow prevention.
- Stock rollback after a forced SQL constraint violation.
- Concurrent exits with insufficient stock for both operations.
- Ten simultaneous inventory entries.
- Inventory entry versus product deactivation.
- Product creation versus category deactivation.
- Product reassignment versus destination category deactivation.
- Concurrent category changes.
- Concurrent duplicate SKU creation.

Seven concurrency tests were additionally repeated ten times, producing 70 successful test-case executions.

These repetitions are not 70 distinct test cases or a formal load test.

### Corrections during verification

The first Release coverage execution completed with 191 passing tests but reported three warnings:

- CA1861 in DatabaseIntegrityTests.cs.
- CA1861 in ConcurrentWriteRepositoryTests.cs.
- No tests discovered in the currently empty Application.Tests project.

The assistant proposed replacing repeated constant array arguments with static readonly fields instead of suppressing CA1861.

The candidate subsequently executed a clean Release build and ran the two populated test projects separately.

Application.Tests was retained for the upcoming application use case phase. No artificial tests were added to suppress its discovery warning.

### Final measured results

Local execution date: 2026-11-26.

Commands:

```powershell
dotnet build --configuration Release --no-incremental

dotnet test tests/InventoryManagement.Domain.Tests/InventoryManagement.Domain.Tests.csproj --configuration Release --no-build --collect:"XPlat Code Coverage" --results-directory .\TestResults\Phase5Final\Domain

dotnet test tests/InventoryManagement.IntegrationTests/InventoryManagement.IntegrationTests.csproj --configuration Release --no-build --collect:"XPlat Code Coverage" --results-directory .\TestResults\Phase5Final\Integration
```

| Metric | Result |
|---|---:|
| Release build duration | 6.4 seconds |
| Release build warnings | 0 |
| Domain unit tests | 95 passed |
| Integration project tests | 96 passed |
| Total tests | 191 passed |
| Failed / skipped | 0 / 0 |
| Domain test duration | 3.0 seconds |
| Integration test duration | 68.2 seconds |

Coverage reported from the final execution:

| Test project | Assembly | Line coverage | Branch coverage |
|---|---|---:|---:|
| Domain.Tests | Domain | 96.31% | 100.00% |
| IntegrationTests | Domain | 86.50% | 75.00% |
| IntegrationTests | Infrastructure | 97.54% | 90.62% |
| IntegrationTests | Application | 100.00% | 100.00% |
| IntegrationTests | API | 0.00% | 0.00% |

These measurements are not combined solution coverage.

Application coverage currently reflects the limited executable code in its contracts, models and exceptions. It does not demonstrate coverage of application use cases that have not yet been implemented.

Infrastructure branch coverage was 91.66% in the preceding execution and 90.62% in the final execution. The exact reason for the difference was not established. Concurrency execution paths may vary between runs.

### Docker persistence verification

The candidate confirmed that a temporary database record survived SQL Server container recreation and repeated database initialization.

The temporary verification table was removed afterward.

No demonstration inventory data was added to the development database as part of this verification.

### Remaining limitations

The assistant did not independently execute the local tests.

The persistence implementation was subsequently statically reviewed on GitHub after publication of commit 3c553e35e5e32848c8bb17915089a82b4ad0e777. The assistant inspected the committed changes, implementation files, test scenarios and documentation. No independent build or test execution was performed during that review.

Application use cases, HTTP endpoints, OAuth2, full container deployment, API integration tests and CI/CD remained pending at this milestone.

The assessment's decision-making answers must reflect the candidate's actual professional experience and will be documented separately.

## Session 6 — Application use cases, TDD and dependency composition

Tool: ChatGPT.

### Main prompts and collaboration

The candidate requested continuation into Phase 6 after the persistence implementation and its remote review.

Subsequent prompts confirmed incremental RED/GREEN results, refactoring regressions and full solution test executions.

The candidate also reported a PowerShell coverage extraction failure and supplied the corrected coverage report for analysis.

The assistant proposed implementation instructions, test scenarios, minimal compilable stubs, production implementations and refactorings. The candidate created and edited the files locally in Visual Studio and executed the commands.

The assistant did not independently compile or execute the Phase 6 code.

### Specification-first development

The application use cases were derived from the previously approved SPEC.md v0.3 and architecture baseline.

No additional business functionality was introduced.

Before implementing each use case, the assistant described its inputs, expected behavior, validation rules, persistence interactions and error outcomes.

Test scenarios were proposed before their GREEN implementations.

### AI proposals and implementation decisions

| Proposal | Outcome and rationale |
|---|---|
| Explicit CQRS handler contracts | Implemented typed query and command contracts without MediatR or a generic dispatcher. |
| Functional module organization | Implemented Categories, Products and InventoryMovements folders containing their use cases. |
| Explicit FluentValidation invocation | Implemented request validation before persistence operations. |
| Shared pagination validation | Implemented PageRequestValidator for both paginated query handlers. |
| Validate normalized text length | Implemented validation consistent with existing Domain normalization rules. |
| Preserve domain-owned invariants | Retained Domain validation and entity state protection. |
| Reuse ProductDetails | Implemented product creation and update without exposing stock or active-state mutation. |
| Avoid persistence prechecks | Retained transactional existence, uniqueness and state checks inside Infrastructure to avoid redundant and race-prone prechecks. |
| Reuse identifiable exception contracts | Implemented not-found handling and propagation of known domain and persistence exceptions. |
| Simplify handler validation | Replaced repeated validation-result inspection with FluentValidation ValidateAndThrowAsync after successful regression tests. |
| Extract category validation rules | Implemented shared CategoryValidationRules after completing category use cases. |
| Extract product validation rules | Implemented shared ProductValidationRules after completing product use cases. |
| Use native TimeProvider | Implemented deterministic UTC timestamp generation without a custom clock abstraction. |
| Register dependencies explicitly | Implemented AddInventoryApplication in API without adding dependency injection packages to Application. |
| Verify dependency lifetimes | Implemented composition tests using ValidateOnBuild and ValidateScopes. |

The candidate implemented the proposed changes and confirmed the local test results. These confirmations establish the reported implementation and verification workflow, but do not constitute independently stated candidate rationales for every AI proposal.

### TDD evidence

The following table records candidate-confirmed local RED and GREEN executions.

Application counts are cumulative within Application.Tests. Dependency composition counts refer only to the filtered ApplicationRegistrationTests class.

| Cycle | RED passed | RED failed | GREEN passed | GREEN failed |
|---|---:|---:|---:|---:|
| GetCategories | 0 | 12 | 12 | 0 |
| GetCategoryById | 12 | 7 | 19 | 0 |
| CreateCategory | 19 | 13 | 32 | 0 |
| UpdateCategory | 32 | 16 | 48 | 0 |
| DeleteCategory | 48 | 7 | 55 | 0 |
| GetProducts | 55 | 12 | 67 | 0 |
| GetProductById | 67 | 7 | 74 | 0 |
| CreateProduct | 74 | 20 | 94 | 0 |
| UpdateProduct | 94 | 24 | 118 | 0 |
| DeleteProduct | 118 | 7 | 125 | 0 |
| RegisterInventoryMovement | 125 | 17 | 142 | 0 |
| Dependency composition | 0 | 22 | 22 | 0 |

The candidate confirmed twelve additional RED/GREEN cycles during Phase 6.

### Refactoring verification

| Refactoring | Regression result |
|---|---:|
| Replace repeated handler validation with ValidateAndThrowAsync | 55 passed |
| Extract CategoryValidationRules | 55 passed |
| Extract ProductValidationRules | 125 passed |

No additional abstraction was introduced solely to eliminate superficial duplication.

### Corrections and limitations of AI-generated instructions

The initial PowerShell coverage extraction instructions failed in the candidate's terminal.

Assigning a FileInfo result to the existing report variable triggered an XML conversion error. Subsequent commands failed because no valid file path or coverage data had been loaded.

The likely cause was an existing typed variable retained from an earlier PowerShell session.

The assistant corrected the instructions by removing the previous variables, locating the report with Get-ChildItem, checking that the file existed and loading it using XmlDocument.Load.

The candidate subsequently supplied successful assembly-level, class-level and uncovered-line output.

No Application production code change was required to correct the coverage extraction procedure.

### Application coverage verification

Command used:

```sh
dotnet test tests/InventoryManagement.Application.Tests/InventoryManagement.Application.Tests.csproj --configuration Release --no-build --collect:"XPlat Code Coverage" --results-directory "./TestResults/Phase6/Application"
```

The candidate supplied actual terminal output reporting 142 passed tests, zero failures and zero skipped tests.

Measured coverage from the supplied report:

| Assembly | Line coverage | Branch coverage |
|---|---:|---:|
| InventoryManagement.Application | 96.70% | 88.46% |
| InventoryManagement.Domain | 49.07% | 37.50% |

The Domain figures above represent only the code exercised by Application.Tests. They are not the Domain unit test coverage baseline.

All eleven Application handlers report 100% line and branch coverage.

CategoryValidationRules reports 100% line coverage and 87.50% branch coverage.

ProductValidationRules reports 100% line coverage and 83.33% branch coverage.

The exact unexecuted validation branches were not identified from the supplied report.

The twelve uncovered Application lines belong to CategoryReadModel, ProductReadModel and PagedResult.

The assistant recommended against adding artificial property-access tests solely to increase the coverage metric. The candidate continued with dependency composition verification.

### Final local verification

| Metric | Result |
|---|---:|
| Domain unit tests | 95 passed |
| Application unit tests | 142 passed |
| Persistence integration tests | 96 passed |
| Dependency composition tests | 22 passed |
| Total tests | 355 passed |
| Failed / skipped | 0 / 0 |
| Release build warnings | 0 |

The candidate confirmed successful RED and GREEN composition tests and complete solution regression.

The composition tests verify resolution and lifetimes of registered services without opening SQL Server connections.

### Remaining limitations

Phase 6 results are based on candidate-executed local tests, explicit confirmations and supplied coverage output.

The assistant did not independently execute these tests. The Phase 6 implementation was subsequently statically reviewed on GitHub after publication of commit cc6b32f7c57db6793b86584f04f11ba62acb030b.

Program.cs integration, HTTP endpoints, centralized error responses, OAuth2, complete Docker Compose deployment, API integration tests and CI/CD remained pending at this milestone.

The candidate's professional decision-making answers remain a separate submission requirement and must reflect actual experience.

## Session 7 — REST API, OAuth2, Swagger and integration verification

Date: 2026-11-27.

Tool: ChatGPT.

### Main prompts and collaboration

The candidate requested continuation into the REST API and security implementation phase.

The assistant proposed HTTP contracts, controller implementations, error handling, OAuth2 configuration, Swagger integration and test scenarios.

The candidate implemented the files locally in Visual Studio, configured Docker services and Keycloak, executed PowerShell commands and performed manual functional checks.

Actual candidate confirmations included:

- "Te confimo que todo salio como mencionaste en swagger y tambien todos los tests pasaron"
- "Listo. Ya hice estas pruebas como mencionaste y todo salio bien"
- "Confírmo las 13 pruebas nuevas exitosas y las 513 pruebas de regresión sin errores ni advertencias."

The candidate subsequently supplied terminal output confirming 530 successful regression tests and the final Cobertura XML report.

### Specification-first development

HTTP endpoints were derived from the previously approved SPEC.md v0.3 and architecture baseline.

The implementation retained the agreed eleven business operations, HTTP status codes, validation rules, authentication requirements and inventory consistency guarantees.

No additional business endpoints were introduced.

Request contracts and expected HTTP behavior were defined before controller implementation.

Filtered failing tests preceded the implementation of the initial error handling and controller functionality.

### AI proposals and implementation decisions

| Proposal | Outcome and rationale |
|---|---|
| Separate HTTP request DTOs | Implemented to avoid exposing domain mutation through public request contracts. |
| Reject unmapped JSON members | Implemented to prevent unsupported fields, including direct stock modification. |
| Reuse application read models | Implemented to avoid unnecessary response mapping and duplicate models. |
| Native IExceptionHandler | Implemented for centralized exception handling. |
| Identifiable ProblemDetails codes | Implemented to provide consistent machine-readable errors without leaking internal details. |
| Thin ASP.NET Core controllers | Implemented to preserve application and persistence boundaries. |
| Keycloak as OAuth2 provider | Implemented and locally verified instead of creating a custom token-issuing service. |
| Authorization Code with PKCE | Implemented for interactive Swagger authentication using a public client. |
| Client Credentials | Implemented for machine-to-machine smoke verification. |
| Exact issuer and audience validation | Implemented without weakening validation for local infrastructure. |
| Shared authorization scope | Implemented for all current business endpoints. Fine-grained role-based authorization remains outside the current scope. |
| Development-only Swagger | Implemented to avoid exposing interactive documentation in the Production environment. |
| Controlled RSA signing keys in tests | Implemented to exercise JWT validation without requiring a live Keycloak instance for every automated test. |
| WebApplicationFactory | Implemented to verify the actual API entry point and dependency composition. |
| Real SQL persistence in full-host tests | Implemented using isolated Testcontainers instances rather than mocked repositories. |
| Additional security configuration tests | Implemented after reviewing actual uncovered code paths. |
| Artificial tests for complete coverage | Not pursued. Remaining coverage gaps were evaluated according to functional risk. |

These rationales describe the proposed and implemented technical design. They must not be interpreted as independently stated personal reasoning from the candidate unless explicitly recorded.

### TDD evidence

The following filtered RED/GREEN executions were confirmed during the initial API implementation.

| Component | RED passed | RED failed | GREEN passed | GREEN failed |
|---|---:|---:|---:|---:|
| GlobalExceptionHandler | 0 | 16 | 16 | 0 |
| HTTP error responses | 2 | 12 | 14 | 0 |
| CategoriesController | 0 | 25 | 25 | 0 |
| ProductsController | 0 | 34 | 34 | 0 |
| InventoryMovementsController | 0 | 25 | 25 | 0 |

JWT and Swagger test scenarios were also defined before their corresponding complete implementations.

The exact observed JWT RED count was not retained in the available verification evidence. Expected RED results are not presented as measured results.

The full-host and complementary coverage tests were regression additions against existing functionality. They are not represented as additional artificial RED/GREEN cycles.

### Corrections and limitations of AI-generated instructions

#### Local configuration

The local ASP.NET Core host initially lacked required SQL Server configuration because Docker Compose environment files are not automatically loaded by Visual Studio.

The assistant proposed configuring local User Secrets instead of embedding credentials in launchSettings.json or version-controlled configuration.

The candidate subsequently confirmed successful HTTPS startup in Visual Studio.

#### Authentication composition

An unauthenticated request initially returned HTTP 500 because JWT Bearer authentication and its middleware had not been incorporated into the actual Program.cs host.

The issue was diagnosed using the exception handler and Visual Studio debugging.

Program.cs was corrected to register and invoke authentication before authorization.

Subsequent real requests returned the expected HTTP 401, 403 and 200 responses.

The incident demonstrated that isolated controller and authentication tests do not independently guarantee correct production-host composition.

Full-host integration tests were added to address this gap.

#### Swagger client identification

During manual verification, the distinction between the public OAuth2 client ID and the demonstration user account required clarification.

The candidate was instructed to use inventory-swagger as the client ID and inventory-user for interactive authentication.

Keycloak reused its existing SSO session during subsequent authorization.

#### Token expiration

A manual request returned HTTP 401 while an Authorization header was present.

Token expiration was identified as the likely cause because access tokens have a five-minute lifetime.

Swagger authorization renewal instructions were provided.

The candidate subsequently confirmed successful completion of the manual functional workflow. The precise token expiration event was not independently established from token diagnostics.

#### Coverage extraction

The initial PowerShell XPath expression searched for lowercase branch="true".

The actual Coverlet XML uses branch="True".

The assistant identified and corrected the case-sensitive XPath mismatch. Coverage analysis was performed directly from the XML.

#### Swagger UI regression test

The first additional Swagger UI test searched for the configured OAuth2 client identifier in index.html.

The candidate reported 12 passing tests and one failing test.

The assistant identified that Swashbuckle 11.0.1 places the OAuth2 initialization configuration in the generated index.js resource.

The test was corrected to inspect both index.html and index.js, including the client identifier, required scope and PKCE configuration.

No production code change was required for this correction.

The candidate subsequently confirmed the corrected tests and complete 530-case regression.

### Actual Keycloak smoke verification

The candidate executed requests using tokens issued by the locally running Keycloak instance.

| Scenario | Expected status | Actual status |
|---|---:|---:|
| Missing access token | 401 | 401 |
| Malformed token | 401 | 401 |
| Tampered token signature | 401 | 401 |
| Valid token without inventory.access | 403 | 403 |
| Valid token with inventory.access | 200 | 200 |

Swagger Authorization Code with PKCE was also manually verified.

An authorized GET request succeeded, and removing Swagger authorization restored HTTP 401 behavior.

These local smoke checks are separate from the automated test suite.

### Manual functional and database verification

The candidate confirmed successful completion of 26 functional HTTP checks and four SQL verification checks.

The verification covered:

- Category creation, retrieval, update and logical deletion.
- Product creation, retrieval, update and logical deletion.
- Case-insensitive duplicate rejection.
- Missing category rejection.
- Rejection of direct stock input.
- Inventory entry and exit registration.
- Insufficient stock rejection.
- Invalid movement type and quantity rejection.
- Preservation of stock during product updates.
- Product and category deletion restrictions.
- Rejection of movements for inactive products.
- Persisted inactive categories and products.
- Preservation of exactly three successful inventory movements.
- Reconciliation of current stock with movement history.

The confirmed movement sequence was an entry of 10 units, an exit of 4 units and an exit of 6 units.

Final stock: zero.

Rejected movements were not persisted.

These results are candidate-reported manual verification, not additional automated test cases.

### Automated test progression

| Milestone | Total passing tests |
|---|---:|
| Phase 6 baseline | 355 |
| HTTP error handling | 385 |
| Category endpoints | 410 |
| Product endpoints | 444 |
| Inventory movement endpoint | 469 |
| JWT authentication | 488 |
| Swagger documentation | 500 |
| Full API host integration | 513 |
| Complementary security and Swagger tests | 530 |

Phase 7 added 175 distinct automated test cases.

### Final local verification

Commands:

```powershell
dotnet build --configuration Release --no-incremental
dotnet test --configuration Release --no-build

dotnet test tests/InventoryManagement.IntegrationTests/InventoryManagement.IntegrationTests.csproj --configuration Release --no-build --collect:"XPlat Code Coverage"
```

The final supplied terminal output confirmed:

| Metric | Result |
|---|---:|
| Domain unit tests | 95 passed |
| Application unit tests | 142 passed |
| Integration project tests | 293 passed |
| Total tests | 530 passed |
| Failed | 0 |
| Skipped | 0 |
| Complete regression duration | 83.9 seconds |
| Coverage test execution | 293 passed |
| Coverage test duration | 74.0 seconds |

A preceding 513-case regression was explicitly confirmed without build errors or warnings.

The final supplied output did not include the separate clean-build transcript. Therefore, its warning count is not independently established by that transcript.

### Coverage improvement

The initial Phase 7 integration coverage run included 276 passing tests.

After reviewing uncovered configuration and security paths, 17 additional tests were introduced.

| Component | Initial lines | Final lines | Initial branches | Final branches |
|---|---:|---:|---:|---:|
| API | 95.26% | 99.43% | 78.46% | 89.23% |
| AuthenticationServiceRegistration | 94.36% | 100.00% | 50.00% | 100.00% |
| SwaggerServiceRegistration | 71.87% | 100.00% | 50.00% | 100.00% |
| Integration aggregate | 96.17% | 97.60% | 84.47% | 87.67% |

Final measured integration coverage:

| Assembly | Lines | Branches |
|---|---:|---:|
| API | 525/528 | 58/65 |
| Application | 364/364 | 23/26 |
| Domain | 141/163 | 24/32 |
| Infrastructure | 476/488 | 87/96 |
| Aggregate | 1506/1543 | 192/219 |

All controller implementations and both authentication and Swagger registration classes reported complete instrumented line coverage.

Three API lines remain uncovered: one Development-only host branch and two generic HTTP error mapping lines.

These coverage results were obtained by inspecting the candidate-supplied Cobertura XML.

They represent integration-test coverage, not merged coverage of all 530 automated tests.

### Remaining limitations

The assistant did not independently compile or execute the Phase 7 implementation.

The reported results originate from candidate-executed local tests, explicit confirmations and supplied terminal and coverage output.

Phase 7 source changes were subsequently statically reviewed after publication of commit afad9b292058cefbdbb9e0173d67e2ed94915767. This was not independent execution.

Automated JWT tests use controlled OpenID Connect metadata rather than querying the actual Keycloak instance.

Actual OAuth2 communication was verified separately through local smoke tests.

Complete API container deployment, clean Compose startup, container-network authentication, CI/CD and final OpenAPI contract review remained pending at this milestone.

The assessment's professional decision-making answers remain a separate requirement and must reflect actual candidate experiences.

## Session 8 — Docker deployment, persistence and recovery

Date: 2026-11-27.

Tool: ChatGPT.

### Main prompts and collaboration

The candidate requested continuation into Phase 8:

"Gracias. Comencemos con la fase 8 entonces."

The candidate implemented the proposed Docker configuration, health checks and initialization improvements locally using Visual Studio, Warp, Docker Desktop and SQL Server Management Studio.

The assistant provided implementation instructions, test scenarios, troubleshooting guidance and static analysis.

The candidate supplied terminal output, database screenshots, Swagger results and coverage data.

### Specification-first development

Phase 8 implemented the existing deployment requirements from SPEC.md v0.3.

No new business requirements were introduced.

The deployment acceptance criteria included automatic initialization, authenticated API availability, persisted data and reproducible local startup. Health endpoints support operational verification without adding business operations.

### AI proposals and implementation decisions

| Proposal | Outcome and rationale |
|---|---|
| Multi-stage API Dockerfile | Implemented to separate compilation from runtime. |
| Non-root API execution | Implemented and verified with UID 1654. |
| Native ASP.NET Core health checks | Implemented without additional NuGet packages. |
| Separate liveness and readiness endpoints | Implemented to distinguish process availability from SQL connectivity. |
| Compose startup dependencies | Implemented to prevent API startup before successful initialization. |
| Development-only HTTP exception | Implemented through explicit configuration without disabling JWT validation. |
| Restricted container credentials | Implemented and verified. |
| Dynamic Keycloak backchannel | Verified without weakening JWT validation. |
| Independent clean deployment | Implemented using a separate Compose project and volumes. |
| Bounded database availability retries | Added after the candidate discovered a startup failure. |
| Retain SQL Server CU28 | Assistant recommended retaining the tested image and documenting its limitation rather than modifying upstream startup scripts. |
| Data Protection configuration | Assistant recommended documenting the warnings instead of hiding them or adding unrelated key management. Current JWT authentication does not rely on server-side authentication cookies. |

These rationales describe technical decisions proposed and exercised during the collaboration; they are not independently stated personal reasoning from the candidate.

### TDD evidence

The candidate added four full-host integration test cases before implementing the health endpoints.

| Scenario | Expected response |
|---|---|
| Liveness with available database | 200 |
| Readiness with available database | 200 |
| Liveness with unavailable database | 200 |
| Readiness with unavailable database | 503 |

The candidate confirmed four expected RED failures followed by four GREEN successes.

The complete host test suite increased from 13 to 17 cases.

The total automated regression increased from 530 to 534 cases.

### Corrections and candidate discoveries

The candidate independently stopped and restarted containers through Docker Desktop.

This exposed a recurring database initialization failure when SQL Server was not yet available.

The initial AI-proposed startup configuration depended too heavily on Compose dependency ordering.

The database initializer was modified to perform bounded availability checks before schema initialization, with up to 30 attempts per database, three-second connection and query timeouts and two seconds between attempts.

A subsequent controlled interruption confirmed that initialization waited for SQL Server and completed successfully after recovery. The supplied output included six failed availability attempts for master and one for InventoryManagement before success.

The candidate also identified gaps in SQL Server IDENTITY values following service restart.

The assistant explained identity caching as a likely cause and why technical identifiers do not require gap-free numbering. The exact cause of each individual identifier gap was not established.

No identity reseeding or manual identifier generation was introduced.

### Container deployment verification

The API image was built and published successfully for Linux locally.

The candidate verified:

- Runtime execution without root privileges.
- Absence of the .NET SDK in the final image.
- Successful four-service Compose startup.
- SQL Server and Keycloak health checks.
- Successful one-shot database initialization.
- Anonymous API health endpoints.
- Protection of business endpoints and real authenticated requests.
- Internal and external OpenID Connect discovery.
- Matching RSA public signing keys.
- Client Credentials and Authorization Code with PKCE.
- Inventory writes, business conflicts and stock reconciliation.
- Loopback-only published ports and isolation of administrator and OAuth2 client credentials from the API container.

Client Credentials smoke checks returned the following responses:

| Scenario | HTTP status |
|---|---:|
| No token | 401 |
| Malformed token | 401 |
| Manipulated signature | 401 |
| Valid token without scope | 403 |
| Valid token with inventory.access | 200 |

Interactive Swagger verification also confirmed authorized category and product queries and HTTP 401 after removing authorization.

### Clean deployment and persistence

A separate Compose project was created using independent named volumes.

Database initialization and Keycloak realm import completed without manual configuration.

The new database was initially empty.

An authenticated category creation succeeded.

The original environment was restored, and its existing inventory records remained intact.

The category created in the isolated environment did not appear in the original database.

Container recreation subsequently preserved SQL Server records and Keycloak signing keys.

The clean deployment reused the local checkout, images, credentials and default ports. It verified initialization with new volumes, not a fresh checkout on an independent machine.

### Recovery verification

SQL Server was intentionally stopped while the API continued running.

The candidate verified:

- Liveness remained HTTP 200.
- Readiness returned HTTP 503.
- Database initialization waited for SQL availability.
- Readiness returned to HTTP 200 without restarting the API.
- Authenticated product retrieval recovered successfully.

Final inventory reconciliation confirmed that both test products had zero persisted stock, zero calculated stock and three movements each.

All four test categories and both test products were logically deleted while retaining the six historical movements.

### SQL Server shutdown investigation

Docker stop returned exit code 137 after the configured shutdown timeout expired.

Docker reported OOMKilled=false.

The assistant inspected candidate-supplied startup script and process information and identified behavior consistent with upstream issue https://github.com/microsoft/mssql-docker/issues/968.

The first DBCC CHECKDB attempt failed because the query session used the restricted inventory_app account even though Object Explorer displayed an administrative connection. The assistant clarified that the command must be run in an authorized administrative query session. No additional permissions were granted to the application account.

The candidate verified database integrity through DBCC CHECKDB, with zero allocation errors and zero consistency errors.

A native SQL SHUTDOWN request was also tested after stopping the API.

The shutdown returned exit code 255, rather than the initially anticipated code 0.

The exact reason for exit code 255 was not independently established.

The system recovered successfully, with healthy services, db-init exit code 0 and HTTP 200 readiness.

### Final local verification

| Metric | Result |
|---|---:|
| Domain tests | 95 passed |
| Application tests | 142 passed |
| Integration tests | 297 passed |
| Total tests | 534 passed |
| Failed tests | 0 |
| Skipped tests | 0 |
| Release build duration | 16.4 seconds |
| Regression duration | 91.6 seconds |
| Integration coverage execution | 80 seconds |

The local NuGet vulnerability audit reported no vulnerable packages across the seven projects using the configured audit sources. This is not a container image vulnerability audit.

### Integration coverage

| Assembly | Lines | Branches |
|---|---:|---:|
| API | 99.46% | 89.04% |
| Application | 100.00% | 88.46% |
| Domain | 86.50% | 75.00% |
| Infrastructure | 98.15% | 91.66% |
| Aggregate | 97.84% | 88.10% |

The candidate-supplied Cobertura report contains 1,544 covered lines out of 1,578 and 200 covered branches out of 227. Percentages follow the rates reported in the XML.

Both newly introduced health check components reported complete instrumented line coverage. SqlServerHealthCheck reported all four instrumented branches covered.

These measurements represent integration tests, not merged coverage of the complete 534-test suite. Manual deployment verification does not increase this measured coverage.

The assistant recommended not adding artificial tests solely to reach 100% coverage. Remaining host configuration branches and final OpenAPI validation can be evaluated during the final review.

### Remaining limitations

The assistant did not independently compile, deploy or execute the application.

Results originate from candidate-executed local tests, supplied output and explicit confirmations.

Phase 8 source changes remain subject to static review after publication.

The local deployment uses development-oriented identity-provider configuration and loopback-bound HTTP endpoints.

SQL Server CU28 has a documented shutdown limitation. Data Protection warnings remain visible; production key management has not been implemented.

Container image vulnerability scanning, production hardening, CI/CD and final OpenAPI contract validation remain pending.

The professional decision-making answers must reflect the candidate's actual experience.
