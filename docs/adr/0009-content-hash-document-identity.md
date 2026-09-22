# Documents are identified by content hash; re-upload is the recovery path

Every upload is hashed (sha256) before a Document row is created. A duplicate upload whose previous Ingestion Job succeeded returns the existing Document — uploading the same file twice is idempotent. A duplicate whose previous Ingestion Job failed enqueues a fresh Ingestion Job over the already-stored binary: since there is no auto-retry and no delete/retry endpoint in the MVP, re-uploading the same file is the one recovery path, so duplicate detection must not turn into a dead end for failed Documents.

## Considered Options

- **Reject duplicates with 409**: pushes a duplicate check onto every client; rejected.
- **Always reuse the existing Document regardless of job status**: a failed Document could never be recovered by re-uploading; rejected.
- **No dedup (every upload is a new Document)**: duplicates silently bloat the corpus and retrieval; rejected.
- **Hash dedup with status-aware behaviour**: chosen.

## Consequences

- Same content always maps to exactly one Document row; the hash is stored from day one.
- Re-uploading a failed Document creates a new Ingestion Job, not a new Document.
- An updated file (same name, different content) is a distinct Document until a document-update flow exists.
