# AI Usage Log

Status: Updated through local domain validation, 2026-11-25. Do not represent proposed decisions as implemented or verified. This log is a retrospective summary of the development conversation, not an independently timestamped transcript or test-run archive.

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
