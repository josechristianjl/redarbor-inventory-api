# AI Usage Log

Status: Initial record; update throughout development. Do not represent proposed decisions as implemented or verified.

## Session 1 — Assessment review and specification (2026-11-23)
Tool: ChatGPT.

### Prompt summary
- Review the supplied Redarbor technical assessment and evaluate difficulty, risks and an appropriate solution approach (original user prompt: "Como lo vez?").
- Begin with the analysis and specification phase, prioritizing prompt delivery while noting there is no fixed deadline (original user prompt: "Me parece bien, empecemos con primero. Para aclarar, la prueba en si no tienen un fecha especifica de entrega, pero si lo mas pronto posible.").

### Evidence of specification-first development
The assistant drafted SPEC.md v0.1 before application code was generated. It distinguishes explicit assessment requirements from proposed business rules and defines acceptance criteria. Candidate subsequently approved the specification in Session 2.

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
Candidate explicitly approved the preceding specification and scope: "Estoy de acuerdo con todo esto. Pasemos a lo siguiente". SPEC.md is now v0.2. No individual rationale beyond agreement was supplied; do not attribute invented reasons to the candidate.

AI proposed ARCHITECTURE.md v0.1, including .NET 10 LTS (superseding initial .NET 8 suggestion after checking Microsoft support dates), explicit CQRS handlers without MediatR, narrow persistence ports, Dapper transactional locking, SQL initialization scripts, Keycloak OAuth2 with PKCE and client credentials, and focused unit/integration tests. Candidate subsequently approved the architecture: "Estoy de acuerdo. Pasemos a la fase 3 entonces." No code or tests were executed during architecture design.

## Session 3 — Project setup (2026-11-24 to 2026-11-25)
Tool: ChatGPT.

### Prompt summary
- Continue with phase 3 and configure the development environment, solution and project references.
- Explain why setup was being performed through the terminal and adapt the instructions to Visual Studio.
- Review the six project files supplied by the candidate and centralize the actual NuGet versions.
- Diagnose the reported Microsoft.OpenApi version conflict.

### Candidate decisions and AI proposals
- Candidate chose to create the GitHub repository manually and create the solution again from Visual Studio 2026 rather than use the CLI. The assistant adapted the procedure and proposed retaining SLNX.
- Assistant proposed shared build settings, EditorConfig, environment variable template and Central Package Management. Candidate followed setup instructions and provided project files.
- Assistant initially proposed keeping both OpenAPI generators. Candidate reported a build failure: Swashbuckle 11.0.1 required Microsoft.OpenApi >= 3.11.0, while Microsoft.AspNetCore.OpenApi 10.0.15 required >= 2.13.0 and < 3.0.0. Assistant corrected its proposal by removing Microsoft.AspNetCore.OpenApi from the API project and central versions, and replacing AddOpenApi/MapOpenApi with Swagger registration and middleware. Candidate reported successful build and tests after the change. No forced transitive version override was used in the proposed fix.

### Verification and limitations
- Candidate reported Docker validation, .NET 10 SDK 10.0.404, successful build and successful template tests after the dependency correction. These are candidate-reported results, not independently executed by the assistant.
- Template tests are not evidence of business-rule coverage or TDD. No functional implementation, OAuth2 verification, database integration tests or coverage measurements have been completed.
- First commit and Swagger runtime verification are pending.
