# Hybrid search in a single Qdrant collection

All Chunks live in one Qdrant collection regardless of workspace; tenancy and provenance are payload filters (`workspace_id`, `document_id`, page numbers, heading path, chunk index, embedding model version). Retrieval is hybrid: named dense + sparse (BM25-style) vectors fused with RRF, because exact identifiers (error codes, part numbers, accented Vietnamese terms) are missed by dense-only search. A cross-encoder reranker is deliberately deferred until the Golden Set shows it is needed.

## Considered Options

- **One collection per workspace**: stronger isolation, not worth the management overhead at MVP scale.
- **Dense-only search**: simpler; rejected for the exact-term recall loss on a mixed EN/VI corpus.

## Consequences

The collection must be created with named dense+sparse vectors from day one; retrofitting sparse vectors later means a migration.
