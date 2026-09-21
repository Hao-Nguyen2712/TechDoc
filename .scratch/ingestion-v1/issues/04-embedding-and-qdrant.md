# 04: Embedding and Qdrant — Chunks become searchable

**What to build:** After Chunking, the pipeline embeds every Chunk (dense vectors from the configured Gemini embedding model at 1536 dimensions, sparse vectors as app-supplied token IDs with Qdrant computing BM25) and upserts them into the single Qdrant collection with the ADR 0005 payload. Persistence stays all-or-nothing per Document: Qdrant is written first, chunk rows commit in one PostgreSQL transaction only after every embedding succeeds, and a mid-pipeline failure removes the Document's points before the job ends `failed`. From the user's perspective: once the job says `done`, the Document's Chunks are queryable vectors ready for retrieval.

**Blocked by:** 03 — Structure-aware Chunking and TXT support.

**Status:** ready-for-human

- [x] Chunks are embedded in batches with `gemini-embedding-001` at 1536 dimensions through the .NET AI abstractions; model identity and dimension are recorded on every Chunk
- [x] Named dense + sparse vectors are upserted into a single collection whose payload carries workspace, document, page/line span, heading path, and chunk index (ADR 0005)
- [x] Sparse vectors use Qdrant-side BM25 over app-supplied token IDs (lowercase, whitespace, punctuation-stripped)
- [x] The collection is auto-created in dev when missing
- [x] All-or-nothing holds: chunk rows commit only after all embeddings succeed; any failure leaves no Qdrant points and no chunk rows, with the error visible on the job
- [x] Integration tests stub the embedding provider at the AI abstraction seam and assert end-state across PostgreSQL, Qdrant, and MinIO
