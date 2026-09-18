# 02: Tracer bullet — upload a PDF and watch it become a Document with Chunks

**What to build:** The first complete path a Document travels: a user uploads a PDF over the API and gets an Ingestion Job reference back; a background worker picks the job up, extracts the text (pages identified, `needs-ocr` flagged where text is missing), splits it with a deliberately naive placeholder chunker, and persists the Chunks to PostgreSQL — the job reaches `done`. A PDF with no extractable text ends `failed` with a clear error. This slice also establishes the integration-test harness (per the spec's Testing Decisions) and stores the content hash that ticket 05's dedup will rely on.

**Blocked by:** 01 — Rename projects to TechDocAI.* and clean the scaffold.

**Status:** resolved

- [x] `POST /documents` with a PDF returns `202` with `documentId` and `jobId`; the binary is stored in MinIO; Document and Ingestion Job rows exist in PostgreSQL
- [x] Uploads that are not PDF/TXT or exceed 50 MB are rejected with clear errors (extension + magic-byte check)
- [x] The background worker processes jobs sequentially; the job moves through `extracting → chunking` statuses and ends `done` with Chunks persisted
- [x] Pages with insufficient text are flagged `needs-ocr` on the Chunk and Document levels
- [x] A PDF yielding zero Chunks ends the job `failed` with a clear "no extractable text" error
- [x] `GET /ingestion-jobs/{id}` reports status and, for failures, the error
- [x] The sha256 content hash is stored on the Document
- [x] Integration tests drive through the HTTP API against the real dev compose services (embedding provider stubbed at the AI abstraction seam once it exists)
