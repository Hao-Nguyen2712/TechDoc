# 01: Four-project layering refactor (S0–S8)

**What to build:** Re-organize the solution into `Api` / `Application` / `Core` / `Infrastructure` per ADR 0010, with CQRS-lite use-case handlers, repository + `IUnitOfWork` seams, key-based document storage, and NetArchTest dependency rules. Build and all test suites stay green after every step.

**Blocked by:** None.

**Status:** resolved

- [x] `TechDocAI.Application` created (ports in `Abstractions/`, use cases in `UseCases/`, `AddApplication` + convention scanning)
- [x] Ports moved from `TechDocAI.Core.Interfaces` to `Application/Abstractions`; extraction/chunk shapes moved to `Core/Extraction`, `Core/Chunking`
- [x] Chunker stack + sparse-vector builders moved to `TechDocAI.Core`; `StructureAwareChunker` referenced as a concrete Core type (no port)
- [x] Repository ports + `IUnitOfWork` implemented in `Infrastructure/Persistence/Repositories/`
- [x] Read use cases (`ListDocuments`, `GetDocument`, `GetJobStatus`), `UploadDocument` (absorbed `DocumentService`), `ProcessIngestionJob` (absorbed `IngestionPipelineProcessor` + extractor dispatcher); `DocumentService`, `IDocumentService`, `IDocumentChunker` deleted
- [x] `IDocumentStorage` key-based; MinIO `original.pdf` hardcode removed (bug fix)
- [x] Background worker glue moved to `Api/Workers`; hosted-service registration in `Program.cs`
- [x] `TechDocAI.ArchitectureTests` (NetArchTest) with 6 passing dependency rules
- [x] Startup uses `Migrate()` instead of `EnsureCreated()` (non-Testing)
- [x] Integration test factory: per-context SQLite connections (`mode=memory&cache=shared` + anchor) eliminating the SQLITE_BUSY flake; poll budget raised to 10s

## Comments

- Implementation commits: `d86ba92` (baseline) → `be8722e` (S2) → `99d41d8` (S3) → `aed8050` (S4) → `4cb11af` (S5a) → `cd37479` (S5b) → `140be85` (S5c) → `7d4142c` (S6) → `89e49f9` (S7) → final S8 commit.
