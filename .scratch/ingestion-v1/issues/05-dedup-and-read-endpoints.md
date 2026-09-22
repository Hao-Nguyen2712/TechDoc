# 05: Content-hash dedup and Document read endpoints

**What to build:** Re-uploading becomes meaningful (ADR 0009): content whose sha256 matches a Document whose Ingestion succeeded returns that existing Document — accidental re-uploads are idempotent; content matching a Document whose Ingestion failed enqueues a fresh Ingestion Job over the already-stored binary — re-upload is the recovery path. Users can also list and inspect their Documents. Runs in parallel with tickets 03–04.

**Blocked by:** 02 — Tracer bullet: upload a PDF and watch it become a Document with Chunks.

**Status:** ready-for-human

- [x] Uploading content whose hash matches a successfully ingested Document returns that existing Document (`200`) — no new row, no new job
- [x] Uploading content whose hash matches a Document with a failed Ingestion Job enqueues a new Ingestion Job over the stored binary (`202`, same Document)
- [x] `GET /documents` lists Documents; `GET /documents/{id}` shows one with its Ingestion Job references
- [x] Tests cover both dedup branches through the HTTP API
