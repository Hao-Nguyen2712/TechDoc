# 03: Run the architecture tests in CI

**What to do:** `TechDocAI.ArchitectureTests` (NetArchTest) enforces the ADR 0010 layering locally, but the repo has no CI workflows yet. The layering rules only protect the codebase if they run on every push/PR.

**Blocked by:** 01.

**Status:** ready-for-agent

- [ ] Add a CI workflow that runs `dotnet build src.slnx` and `dotnet test src.slnx` (all four test projects, including architecture tests) on push/PR
