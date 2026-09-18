# Docker Compose (not Aspire) runs the dev infrastructure

All three backing services for local development — PostgreSQL, Qdrant, and MinIO — are brought up with a single `docker compose up` from the repo's `docker-compose.yml`; the API (`TechDocAI.Api`) runs separately (`dotnet run` / F5) and reaches them over localhost ports. The team is .NET (see ADR 0002) and the .NET Aspire AppHost was seriously considered for one-click orchestration with automatic connection-string injection, but was rejected: for three containers it adds an extra project and dependency set, ties infrastructure bring-up to the .NET toolchain (agents and non-.NET tooling can no longer start the stack), and Docker Compose is already the language of ADR 0001. Revisiting is cheap — Aspire can wrap this same compose file (`AddDockerComposeEnvironment`) without replacing it.

## Considered Options

- **Aspire AppHost (Aspire.Hosting.PostgreSQL / Qdrant)**: F5-everything, service discovery, dashboard. Rejected — orchestration becomes .NET-only, extra project and moving API surface, overkill for 2+1 containers.
- **AppHost wrapping the compose file**: dashboard without losing compose. Rejected for now — same extra project; least complexity wins until the missing F5-one-button is actually felt.
- **Docker Compose only**: chosen.

## Consequences

Connection strings and endpoints are wired by hand in `appsettings.Development.json` with dev-only credentials that are committed (real secrets such as the Gemini API key live in user-secrets). There is no service dashboard; `docker compose logs` and each service's own UI (MinIO Console at :9001) cover diagnostics. All services carry healthchecks so CI can reuse the same file for integration tests later.
