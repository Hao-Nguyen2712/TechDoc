# S3-compatible object storage for Document binaries

Uploaded Document files — and their normalized PDF forms (see ADR 0004) — live in S3-compatible object storage, separate from PostgreSQL (relational data) and Qdrant (embeddings). The storage must speak the S3 API so the production backend can be swapped (self-hosted MinIO or AWS S3) without touching application code; MinIO in the dev compose file is the first implementation.

## Considered Options

- **Local filesystem via config path**: simplest, but no lifecycle semantics and it blocks the production path. Rejected.
- **PostgreSQL bytea**: one system fewer to operate, but multi-MB binaries bloat backups and relational queries. Rejected.
- **S3-compatible object storage (MinIO for dev)**: chosen.

## Consequences

The application owns the bucket and object-path layout (dev bucket: `documents`, provisioned by an init container in the compose file). Cross-store consistency now spans three systems: deleting a Document must remove its PostgreSQL rows, its Qdrant points, and its stored binaries — application-owned, as in ADR 0001.
