# Four-project layering with Application-owned ports and CQRS-lite use cases

The application layer had sunk into `TechDocAI.Infrastructure` — `DocumentService`, the ingestion pipeline processor and the background worker lived beside the very adapters they orchestrate — so the solution is re-organized into four projects with a one-way dependency flow: **`TechDocAI.Api`** (ASP.NET Core host: endpoints, composition root, background-worker glue; no use-case logic), **`TechDocAI.Application`** (use cases and ports), **`TechDocAI.Core`** (the domain: entities, `Result`, pure computation such as the chunker stack), and **`TechDocAI.Infrastructure`** (concrete technology adapters only: EF Core, MinIO, Qdrant, Gemini embedding, PdfPig/plain-text extractors, in-memory Channel queue). Ports live in `TechDocAI.Application/Abstractions` because a port is shaped by what its caller needs and every port consumer is an Application handler; data shapes consumed by domain logic (e.g. extraction output) stay in Core, and if Core logic ever needs a port, that port moves to Core — the rule is "ports follow consumers", not a dogma.

Use cases follow **CQRS-lite**: one folder per operation under `UseCases/` (`UploadDocument/`, `GetDocument/`, `ProcessIngestionJob/`, …), each holding a `Command`/`Query` record, a result record, and a `Handler` with a single `HandleAsync`. Handlers are the only gateway into Application — endpoints and the worker call handlers, nothing deeper. No MediatR: it is commercially licensed for production use, and reflection-based dispatch hides the call path from readers and code-navigating agents alike; constructor-injected handlers give the same structure without the indirection. Separate read models are deliberately deferred until reads stop being trivial.

## Dependency rules

Enforced by csproj references plus a `TechDocAI.ArchitectureTests` project (NetArchTest):

1. `TechDocAI.Core` references no other project.
2. `TechDocAI.Infrastructure` may not use `TechDocAI.Api.*` namespaces.
3. Only the composition root (Program.cs and its wiring) may use `TechDocAI.Infrastructure.*`; `TechDocAI.Api.Endpoints` may only reference `*Handler` types from Application.
4. EF Core namespaces appear only in `TechDocAI.Infrastructure`.
5. `HttpClient` and raw file I/O (`File`, `Directory`, `Stream`) appear only in `TechDocAI.Infrastructure`.

Because the host must reference every project for composition-root duties, the endpoint→handler seam is enforced by these tests, not by the compiler.

## Considered Options

- **Three projects with the application layer merged into the host** (the direction this ADR revises): rejected — the use-case layer would be protected only by convention and arch tests; the compiler could not stop handlers from using EF or Infrastructure types, and use-case tests would need the web project.
- **Ports in the domain project** (classic DDD repository placement): rejected for now — no domain code consumes any port; repository contracts are shaped by use cases (`FindByContentHash` for dedup, `FindById` for reads), not by the domain.
- **MediatR for dispatch and pipeline behaviors**: rejected — paid license for production and indirection that obscures navigation.
- **Full CQRS with separate read models**: deferred — reads are trivial today; revisit if search or caching needs grow.

## Consequences

- `IDocumentService` and `IDocumentChunker` disappear: upload/dedup logic is absorbed into `UploadDocumentHandler`, and the chunker becomes a concrete Core class the pipeline handler references directly.
- A service class exists only when it has at least two callers or encapsulates a reusable capability; single-caller logic lives in its handler.
- Persistence goes through task-scoped repository ports plus `IUnitOfWork.SaveChangesAsync()`: repositories stage changes, use cases own the transaction boundary (upload writes Document + IngestionJob atomically; the pipeline writes Chunks + job status atomically).
- The dequeue loop (`BackgroundService`) is host glue in `TechDocAI.Api`; it calls `ProcessIngestionJobHandler`. The enqueue/consume seam of ADR 0008 is unchanged — swapping a broker means replacing the `IIngestionJobQueue` implementation and the loop, not the pipeline.
- Unit tests reference `TechDocAI.Core` and `TechDocAI.Application` — plain class libraries; no web host needed to test use cases.
