# architecture-v2: Four-project layering (ADR 0010)

Restructure the solution so the application layer lives in its own project and Infrastructure holds only technology adapters. Settled in the grill session of 2026-09-28; full decision record in `docs/adr/0010-four-project-layering-application-ports-cqrs-lite.md` (amendments: ADR 0002, ADR 0008).

## Target shape

- `TechDocAI.Api` — ASP.NET Core host: minimal-API endpoints, composition root, background-worker glue. Endpoints call only Application handlers.
- `TechDocAI.Application` — use cases (CQRS-lite: one folder per operation, `Command`/`Query` + `Handler`) and ports (`Abstractions/`). References Core only.
- `TechDocAI.Core` — domain only: entities, `Result`, pure computation (chunker stack, sparse vector, extraction/chunk shapes).
- `TechDocAI.Infrastructure` — technology adapters only: EF Core (DbContext, repositories, migrations), MinIO, Qdrant, Gemini embedding, PdfPig/plain-text extractors, Channel queue.

## Key rules

1. Ports follow consumers: every port lives in `Application/Abstractions` because every consumer is an Application handler. Data shapes consumed by domain logic stay in Core.
2. Handlers are the only gateway into Application; a service class exists only with ≥2 callers or a reusable capability (`DocumentService` and `IDocumentChunker` were deleted under this rule).
3. Repositories stage changes; `IUnitOfWork.SaveChangesAsync` commits — use cases own transaction boundaries.
4. No MediatR (paid for production use; dispatch indirection hurts navigation).
5. Six NetArchTest rules in `TechDocAI.ArchitectureTests` enforce the layering (composition-root exception: Program.cs only).

## Behavior changes approved

- `IDocumentStorage` is key-based (`SaveAsync(storageKey, …)` / `OpenReadAsync(storageKey, …)`) — fixes the hardcoded `original.pdf` key bug.
- Startup applies `Migrate()` instead of `EnsureCreated()` (non-Testing only).
