# 02: Reset the dev database once; re-upload legacy TXT documents

**What to do:** Two approved behavior changes invalidate existing dev data:

1. Startup now runs `Migrate()`. A dev database created earlier by `EnsureCreated()` has no `__EFMigrationsHistory` table, so `Migrate()` cannot apply — reset the volume once.
2. Documents stored before the key-based storage fix have TXT objects under the MinIO key `documents/{id}/original.pdf` while new rows read `StorageKey` — legacy TXT uploads must be re-uploaded.

**Blocked by:** 01.

**Status:** ready-for-agent

- [ ] Run `docker compose down -v` (or drop the `techdoc` database) and start the stack once so `Migrate()` creates the schema
- [ ] Re-upload any pre-refactor TXT documents (recovery re-upload path per ADR 0009)
- [ ] Verify one PDF and one TXT upload reach `done` against the docker-compose stack
