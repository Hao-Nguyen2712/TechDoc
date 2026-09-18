# Spec: Ingestion Pipeline v1

Status: ready-for-agent

## Problem Statement

TechDoc promises Q&A over technical documents with exact file and page Citations, but today nothing can get a Document into the system: there is no upload path, no Chunks, nothing to retrieve. Until Ingestion exists, every later capability — retrieval, Answers, measuring quality with the Golden Set — has no data to work on.

## Solution

A user uploads a Document (PDF or TXT) over the HTTP API and immediately gets an Ingestion Job reference back. In the background, the pipeline runs Extraction (text with page or line provenance), Chunking (structure-aware, coherent pieces), and Embedding (dense + sparse vectors), leaving the Document's Chunks searchable with the page/line provenance future Citations need. Re-uploading the same file is idempotent; re-uploading after a failure retries it; scanned documents are diagnosed (`needs-ocr`) instead of silently swallowed.

## User Stories

1. As a TechDoc user, I want to upload a PDF Document over the API, so that its content becomes part of what the assistant can answer from.
2. As a TechDoc user, I want to upload a TXT Document, so that plain-text notes and specs are also searchable.
3. As a TechDoc user, I want the upload to return an Ingestion Job reference immediately, so that I don't have to wait for processing to finish.
4. As a TechDoc user, I want to check my Ingestion Job's status, so that I know when my Document is searchable, or why it failed.
5. As a TechDoc user, I want oversized or non-PDF/TXT uploads rejected with a clear error, so that I learn immediately instead of hitting a mysterious failure deep in the pipeline.
6. As a TechDoc user, I want uploading the same file twice to return the existing Document, so that accidental re-uploads don't create duplicates.
7. As a TechDoc user, I want re-uploading a file whose Ingestion failed to retry the Ingestion, so that recovery is simply "try again".
8. As a TechDoc user, I want a scanned PDF to end in a clear failure explaining no extractable text was found, so that I know OCR isn't supported yet rather than getting an empty result.
9. As a TechDoc user, I want each Chunk to remember the page (or lines) it came from, so that future Answers can cite the exact file and page.
10. As a TechDoc user, I want document headings respected when my Document is split into Chunks, so that retrieved Chunks are coherent sections rather than mid-sentence fragments.
11. As a TechDoc user with a mixed Vietnamese/English corpus, I want both languages embedded well, so that Questions in either language find my Documents.
12. As a TechDoc developer, I want the pipeline stages to be host-agnostic (usable from the API now and the Eval Runner later), so that the measured pipeline is always the real pipeline.
13. As a TechDoc developer, I want Ingestion to run in a background worker sequentially, so that uploads stay fast and processing is gentle on provider rate limits.
14. As a TechDoc developer, I want all-or-nothing persistence per Document, so that a half-ingested Document never pollutes retrieval.
15. As a TechDoc developer, I want failed Ingestion Jobs to record their error, so that I can diagnose pipeline problems from the job record alone.
16. As a TechDoc developer, I want the embedding model identity and dimension stored on each Chunk, so that a future model switch is detectable and re-ingestion can be planned.
17. As a TechDoc developer, I want the Qdrant collection created automatically in dev when missing, so that a fresh compose stack plus app start just works.
18. As a developer building the future Q&A flow, I want Chunks stored as named dense + sparse vectors in a single collection with payload filters, so that hybrid retrieval works from day one without a migration.

## Implementation Decisions

- **Projects**: renamed to `TechDocAI.Api` (web host), `TechDocAI.Core`, `TechDocAI.Infrastructure`. All pipeline stages and orchestration live in `Core`/`Infrastructure` and are host-agnostic; the API merely composes them.
- **Entry point**: `POST /documents` (multipart; PDF or TXT only; ≤ 50 MB; extension + magic-byte validation) → binary stored in MinIO under `documents/{documentId}/original.{ext}` → Document and Ingestion Job rows created → `202 {documentId, jobId}` returned.
- **Background processing**: in-process `Channel` consumed by a `BackgroundService`, one job at a time, status in PostgreSQL — no message broker (ADR 0008).
- **Job lifecycle**: statuses `pending → extracting → chunking → embedding → done | failed`; visible via `GET /ingestion-jobs/{id}`.
- **Dedup**: every upload is sha256-hashed. Duplicate content whose previous Ingestion Job succeeded returns the existing Document; duplicate content whose previous Ingestion Job failed enqueues a new Ingestion Job over the stored binary (re-upload is the recovery path) — ADR 0009.
- **Extraction**: PdfPig for PDFs (text, font info, page boundaries); TXT read as UTF-8 with line numbers as the citation basis (ADR 0004). Pages under a text threshold flag `needs-ocr` (recorded on Chunk and Document). A Document yielding zero Chunks ends its job as `failed` with a clear error.
- **Chunking**: heading-aware — font heuristic (dominant size = body; short, markedly larger lines = headings); sections merged up to ~800–1,000 tokens; no overlap; documents without detectable structure fall back to block cutting with an empty heading path. Token budget estimated locally. A naive fixed-size chunker is a placeholder in the first slice and is replaced by this.
- **Embedding**: `gemini-embedding-001` at 1536 dimensions through the .NET AI abstractions (ADR 0003); embedding input is heading path + Chunk text; batched per Document; model identity and dimension recorded on every Chunk. Sparse vectors are app-supplied token IDs (lowercase, whitespace, punctuation-stripped) with Qdrant computing BM25 (idf modifier).
- **Persistence**: EF Core with migrations; tables `documents`, `chunks`, `ingestion_jobs` (chunk carries heading path, page/line span, text, embedding model + dimension, needs-ocr; document carries content hash, storage key, needs-ocr). Single Qdrant collection with named dense (1536) + sparse vectors, payload per ADR 0005, auto-created in dev. All-or-nothing per Document: Qdrant upserts first, chunk rows commit in one transaction after all embeddings succeed; on failure the Document's points are removed and the job ends `failed`.
- **Workspace**: the single default Workspace only (MVP, per glossary).
- **Defaults**: single-file uploads; no auth in dev.

## Testing Decisions

- A good test asserts external behavior only: integration tests enter at the HTTP API (highest existing seam), upload a small fixture Document, poll the Ingestion Job to a terminal status, then assert end-state across PostgreSQL, Qdrant, and MinIO. No tests against internals.
- Exactly one fake boundary: the Gemini/embedding provider, stubbed at the .NET AI abstraction seam (the seam ADR 0003 mandates for all provider calls). Everything else is real, from the dev compose stack.
- Pure logic (chunker, heading heuristic, tokenizer) is tested directly as pure functions — that adds no seam.
- No prior art exists (greenfield repo); the harness is established by the first ticket that needs it and reused thereafter.

## Out of Scope

- DOCX support and LibreOffice normalization (ADR 0004 follow-up slice).
- OCR execution — v1 only flags `needs-ocr`; Gemini-vision OCR comes later (ADR 0003).
- `DELETE /documents` and the three-store cleanup it implies (ADR 0001/0007) — its own slice.
- Retrieval/search, Questions, Answers, Sessions — the Q&A flow.
- The Eval Runner implementation (later slice; it will run the Golden Set through this exact pipeline).
- Auth, multiple Workspaces, a message broker, auto-retry of failed jobs.

## Further Notes

- During the grill session, `CONTEXT.md` gained the terms **Extraction**, **Chunking**, **Embedding**, and **Eval Runner** (which retires the "CLI" confusion — the tools directory hosts dev-only console projects, never product surfaces).
- ADR 0008 (in-process background ingestion) and ADR 0009 (content-hash Document identity) were written during the session and are binding here.
- The future Eval Runner's `eval-set.json` is the realization of the **Golden Set** glossary term; it is unrelated to the Document corpus this pipeline ingests.
