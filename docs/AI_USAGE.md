# AI Usage and Review Evidence

Technical assessment: Redarbor — Senior Developer.
Tool: ChatGPT. Development period: November 23–27, 2026.

This document is a summary of the collaboration, not a complete conversation transcript or an independent test-run archive. The candidate implemented the changes locally, executed the tests, investigated failures and published the repository. AI proposed specifications, design alternatives, code, tests and troubleshooting steps; it also statically reviewed published changes. It did not independently build, test or deploy the application.

## 1. Main prompts and development workflow

The following table summarizes the principal requests in English. These are paraphrases of the development conversation, not verbatim prompts.

| Stage | Main request to AI | Result |
|---|---|---|
| Assessment | Analyze the assessment, identify risks and establish a specification before writing code. | Functional scope, business rules and acceptance criteria. |
| Architecture | Propose a design that satisfies the specification without unnecessary complexity. | Clean Architecture, CQRS, transactional persistence and OAuth2 design. |
| Project setup | Adapt setup to Visual Studio, review actual project files and resolve the OpenAPI package conflict. | Solution, project references and centralized package configuration. |
| Domain | Implement entities and stock invariants incrementally using RED/GREEN/refactor. | Encapsulated domain behavior and unit tests. |
| Persistence | Implement EF Core reads, Dapper writes, SQL initialization, rollback and concurrent inventory operations. | Real SQL Server repositories and integration tests. |
| Application | Implement commands, queries, validation and dependency composition; review coverage. | Eleven typed handlers and application unit tests. |
| API and security | Implement REST contracts, centralized errors, OAuth2, Swagger and full-host integration tests. | Protected business endpoints and HTTP verification. |
| Deployment | Containerize the API and dependencies; verify startup, persistence, health and recovery. | Four-service Compose deployment and operational tests. |
| Final review | Review OpenAPI and Clean Code, simplify evaluator onboarding and reduce excessive documentation. | Contract refinements, secure Windows setup script and revised documentation. |

## 2. Specification-first development

- The initial specification was drafted before application code, distinguishing explicit assessment requirements from proposed business rules. The candidate approved the scope before moving to architecture and implementation.
- The first published commit contains the specification and architecture before business functionality. The approved functional baseline is [SPEC.md](SPEC.md), v0.3. The technical design is recorded in [ARCHITECTURE.md](ARCHITECTURE.md).
- Use cases were derived from the approved specification. Inputs, rules, expected outcomes and tests were defined before the corresponding implementations. No unrelated business endpoints were added.

Example: concurrent exits of four units against five units of stock must yield one success, one conflict, stock of one and exactly one exit record. This criterion was specified before the transactional repository was implemented. The design uses domain validation and SQL Server locking within an explicit transaction; an integration test verifies the result. A separate test forces a real database constraint failure to verify rollback of the stock update.

The specification establishes expected behavior, architecture describes the technical solution, and tests verify the implementation. This provides traceability rather than relying on generated code as the specification itself.

## 3. Architecture proposals and decisions

The candidate approved the architecture baseline and continued with incremental implementation and verification. The technical rationales below summarize the resulting collaborative design; they are not presented as independently stated personal motivations unless explicitly recorded.

| AI proposal | Outcome | Technical rationale |
|---|---|---|
| .NET 8 initially; subsequently .NET 10 LTS | Revised and approved | The assessment allows .NET 8 or higher. Use a supported LTS runtime and matching EF Core major version. |
| Clean Architecture and a modular monolith | Accepted | Preserve dependency boundaries without unnecessary distributed infrastructure. |
| Explicit CQRS handlers, no MediatR | Accepted | Separate reads and writes using built-in DI without a mediator dependency. |
| EF Core reads and Dapper writes | Accepted | Meet the assessment requirements; use projections for reads and explicit SQL for writes. |
| Narrow repositories and explicit transactions | Accepted | Keep persistence responsibilities focused and make stock consistency and rollback explicit. |
| Logical deletion and immutable movements | Accepted | Preserve historical records and prevent invalid inventory state. |
| Additional conditional stock update | Not adopted | Locked state, domain checks, affected-row verification and database constraints implement the selected consistency protocol. |
| General deadlock retries | Deferred | Do not retry arbitrary database errors or an uncertain commit that might duplicate a movement. The bounded category-reassignment retry is a separate mechanism. |
| Versioned SQL initialization scripts | Accepted | Maintain a single explicit schema initialization mechanism instead of competing EF migrations. |
| Keycloak, Authorization Code with PKCE and Client Credentials | Accepted | Use a standards-based OAuth2 provider and separate public interactive and confidential service clients. |
| Separate public issuer and internal metadata address | Accepted | Support container networking without weakening token validation. |
| FluentValidation, native exception handling and TimeProvider | Accepted | Keep input validation, HTTP error translation and timestamp generation in the appropriate layers. |
| Multi-stage Docker build, non-root API and health checks | Accepted | Separate build/runtime responsibilities and support reproducible local startup and dependency recovery. |
| Unit tests, isolated SQL integration tests and full-host tests | Accepted | Verify domain rules, real database behavior and actual HTTP composition at complementary levels. |
| CI/CD and advanced container scanning | Excluded from final scope | Candidate directed the work toward the assessment requirements rather than optional infrastructure. No completed CI or image scan is claimed. |

## 4. Candidate oversight and corrections

These examples demonstrate how proposals and instructions were reviewed, challenged and corrected during development.

| Issue or proposal | Candidate intervention and resulting action |
|---|---|
| Terminal-based project creation | Candidate requested Visual Studio as the primary workflow. Setup and debugging instructions were adapted to the actual tools used. |
| Two incompatible OpenAPI generators | Candidate reported the dependency conflict. AI corrected its initial proposal by removing Microsoft.AspNetCore.OpenApi and retaining Swashbuckle. The candidate reported successful build, tests and Swagger verification. |
| Generated files appeared in Git Changes | Candidate investigated ignore behavior and amended the ignore rules before committing. The initial 922 entries were reduced to 22. |
| Analyzer warnings and test-count discrepancy | Candidate supplied the output. Missing tests were identified; CA1707 was scoped to test files and repeated constant arrays were replaced rather than suppressing CA1861. |
| Coverage extraction commands failed | Candidate reported PowerShell/XML errors. AI corrected variable handling and the case-sensitive Cobertura XPath expression. Coverage was read from supplied reports rather than estimated. |
| HTTP 500 on unauthenticated request | Candidate exercised the real host and exposed missing authentication composition. Registration and middleware ordering were corrected; full-host integration tests were added. |
| Swagger UI test inspected the wrong file | Candidate reported a failing test. The assertion was corrected to inspect the generated index.js resource as well as HTML. |
| Database initialization failed on container restart | Candidate independently restarted services through Docker Desktop. The initializer was strengthened with bounded SQL availability checks before executing schema scripts. |
| SQL Server shutdown and IDENTITY gaps | Candidate investigated actual restart behavior and verified persisted data and DBCC CHECKDB. The tested image was retained; the shutdown limitation and unresolved cause of exit code 255 remain documented. No identity reseeding was introduced. |
| Final Clean Code and request contract review | OpenAPI filters communicated request restrictions more clearly; CategoryDetails reduced loose parameters in the update flow; authentication registration was refactored without changing security policy. Candidate confirmed the regression run. |
| Evaluator credential setup | A clone using a blank environment template failed Compose configuration. AI initially suggested public demo defaults; the candidate rejected exposing credentials. The selected solution generates five independent cryptographically random credentials locally and preserves existing .env files. |
| Excessive setup steps | Candidate requested Windows-only setup, automatic opening of .env in Notepad and a real evaluator simulation from a published GitHub clone instead of copying local files. setup.ps1 and the revised Quick Start were published. The candidate subsequently confirmed completion of the fresh-clone evaluator workflow in Phase 9. |
| Excessive documentation | Candidate challenged the length and duplication of README and architecture documents, requested concise practical documentation and retained the existing functional specification. This log was also consolidated to focus on evidence required by the assessment. |

The last three decisions are particularly relevant to delivery: secure local configuration, a realistic evaluator workflow and documentation proportional to the task. They are not new business requirements.

## 5. TDD and verification evidence

Tests were proposed before their GREEN implementations where RED/GREEN cycles are documented. The candidate ran tests locally and reported results or supplied output. Additional regression and coverage tests against existing behavior are not represented as new TDD cycles.

Representative confirmed RED/GREEN executions:

| Component | RED passed | RED failed | GREEN passed | GREEN failed |
|---|---:|---:|---:|---:|
| Category constructor | 1 | 9 | 10 | 0 |
| Product inventory operations | 50 | 17 | 67 | 0 |
| EF Core read repositories | 92 | 10 | 102 | 0 |
| Dapper inventory movement repository | 154 | 12 | 166 | 0 |
| RegisterInventoryMovement handler | 125 | 17 | 142 | 0 |
| Dependency composition (filtered) | 0 | 22 | 22 | 0 |
| CategoriesController (filtered) | 0 | 25 | 25 | 0 |
| Health endpoints (filtered) | 0 | 4 | 4 | 0 |

Counts refer to the respective test runs and are not all cumulative across the entire solution. The detailed historical log records seven Domain, six Persistence, twelve Application/composition, five initial HTTP and one health-check RED/GREEN cycle. No exact JWT RED count or Phase 9 OpenAPI RED execution is claimed without retained evidence.

### Reported regression milestones

| Milestone | Passing tests |
|---|---:|
| Domain | 84 |
| Persistence | 191 |
| Application | 355 |
| REST API and OAuth2 | 530 |
| Docker and health checks | 534 |
| Final contract and Clean Code review | 551 |

Latest reported complete regression: 551 passed, zero failed, zero skipped, 86.2 seconds. This is a candidate-executed result, not an independent assistant run.

### Coverage evidence

| Measured run | Line coverage | Branch coverage |
|---|---:|---:|
| Phase 5 Domain unit tests (Domain assembly) | 96.31% | 100.00% |
| Phase 6 Application unit tests (Application assembly) | 96.70% | 88.46% |
| Phase 8 integration tests (aggregate) | 97.84% | 88.10% |

The integration report contains 1,544 of 1,578 covered lines and 200 of 227 covered branches. Percentages are from the supplied Cobertura XML. These are separate historical measurements, not merged coverage of the final 551-test regression. Artificial tests solely to inflate coverage were not pursued.

### Additional verification

- Seven concurrency scenarios were repeated ten times (70 successful executions, not 70 distinct tests or a load test).
- Actual local Keycloak smoke checks returned 401 for missing, malformed or tampered tokens, 403 without the required scope and 200 with the scope. Interactive Swagger PKCE was verified separately.
- Twenty-six manual HTTP checks and four SQL checks verified CRUD, validation, stock reconciliation, movement history and logical deletion.
- A separate Compose project with new volumes verified automatic initialization and realm import. This earlier check reused the local checkout and credentials; it was not a fresh GitHub checkout.
- Controlled SQL interruption verified liveness 200, readiness 503 and recovery to 200 without an API restart. DBCC CHECKDB reported zero allocation and consistency errors after the tested shutdown incident.
- NuGet auditing reported no vulnerable packages from configured sources at the measured milestone. Container image scanning and production hardening were not performed.

## 6. Evidence and traceability

| Evidence | Location |
|---|---|
| Approved functional scope and acceptance criteria | [SPEC.md](SPEC.md) |
| Implemented technical decisions and limitations | [ARCHITECTURE.md](ARCHITECTURE.md) |
| Setup, execution, debugging and tests | [README.md](../README.md) |
| Specification and initial project before business implementation | [c0669d1](https://github.com/josechristianjl/redarbor-inventory-api/commit/c0669d1b128dc645189bcfda17b6cf0d95eaaa1f) |
| Domain, persistence and application implementation | [5edb10a](https://github.com/josechristianjl/redarbor-inventory-api/commit/5edb10a60db26118a35447f4734d349a7162e646), [3c553e3](https://github.com/josechristianjl/redarbor-inventory-api/commit/3c553e35e5e32848c8bb17915089a82b4ad0e777), [cc6b32f](https://github.com/josechristianjl/redarbor-inventory-api/commit/cc6b32f7c57db6793b86584f04f11ba62acb030b) |
| REST API, OAuth2 and deployment | [afad9b2](https://github.com/josechristianjl/redarbor-inventory-api/commit/afad9b292058cefbdbb9e0173d67e2ed94915767), [97951e8](https://github.com/josechristianjl/redarbor-inventory-api/commit/97951e83aaaa2be670232e4a12be38f6543fa2a5) |
| Contract and Clean Code refinements | [68e9d0e](https://github.com/josechristianjl/redarbor-inventory-api/commit/68e9d0e9701251650b38672125d64c13bb4e3624) |
| Credential setup and initial Quick Start revision | [4462c54](https://github.com/josechristianjl/redarbor-inventory-api/commit/4462c54e3893e7e3230938fa6acc6eb49b5d4219) |
| Detailed historical AI usage log, including incremental test results | [Previous published version](https://github.com/josechristianjl/redarbor-inventory-api/blob/4462c54e3893e7e3230938fa6acc6eb49b5d4219/docs/AI_USAGE.md) |

Git history provides traceability of published changes. All reported test results come from local executions performed by the candidate. AI assistance was used for design, implementation guidance, troubleshooting and static code review.
