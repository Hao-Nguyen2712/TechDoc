# TechDoc

RAG Q&A pipeline over technical documents: upload (PDF/DOCX/TXT), ask in Vietnamese or English, get answers with file+page citations. See `CONTEXT.md` for the domain glossary and `docs/adr/` for decisions.

## Dev infrastructure

One command brings up all backing services:

```
docker compose up -d
```

Data lives in `./data/` (gitignored, bind mounts). The Application runs separately and reads endpoints from `src/Application/appsettings.Development.json` (dev-only credentials, committed; real secrets — e.g. the Gemini API key — go in `dotnet user-secrets`).

| Service | Endpoint | Notes |
|---|---|---|
| PostgreSQL | `localhost:5432` | db `techdoc`, user `techdoc`, pass `techdoc` |
| Qdrant | `localhost:6333` HTTP · `6334` gRPC | no API key in dev |
| MinIO | `localhost:9000` API · `localhost:9001` Console | S3 API; bucket `documents` auto-created; user `techdoc`, pass `techdocdev` |

Reset all local data: `docker compose down` then delete `./data/`.
