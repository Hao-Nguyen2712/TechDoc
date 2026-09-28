# 03: Run the architecture tests in CI

**What to do:** `TechDocAI.ArchitectureTests` (NetArchTest) enforces the ADR 0010 layering locally, but the repo has no CI workflows yet. The layering rules only protect the codebase if they run on every push/PR.

**Blocked by:** 01.

**Status:** resolved

- [x] `.github/workflows/ci.yml` added: .NET 10 on ubuntu-latest, `dotnet restore` → `dotnet build src.slnx` → `dotnet test src.slnx` (all four test suites, including architecture tests)
- [x] Triggers: every `push` (any branch — a run exists before a PR is opened), `pull_request` to `main` (merge gate), and `workflow_dispatch` (manual)
- [x] NuGet package cache keyed on `**/*.csproj`; concurrency group cancels superseded runs on the same ref

## Comments

- Integration tests need no external services (in-memory SQLite, in-memory storage/vector store, stub embeddings), so CI runs without docker.
- A branch with an open PR triggers both `push` and `pull_request` runs — duplicate but harmless at this scale; restrict `push` to `main` if minutes ever matter.
- Verify the first real run on github.com/Hao-Nguyen2712/TechDoc after pushing this file (the workflow itself is exercised only once it's on the remote).
