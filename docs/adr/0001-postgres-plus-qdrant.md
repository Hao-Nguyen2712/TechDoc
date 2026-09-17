# PostgreSQL and Qdrant as the two data stores

Relational data (Documents, Chunks metadata, Sessions, ingestion job state) lives in PostgreSQL; embeddings live in Qdrant, a purpose-built vector database. pgvector inside a single PostgreSQL was considered and rejected: the team prefers a dedicated vector engine with native dense/sparse (hybrid) search, keeping retrieval infrastructure separate from relational storage.

## Considered Options

- **pgvector (single PostgreSQL)**: simplest operations, sufficient at the expected scale (~1,000 documents). Rejected in favour of a dedicated vector DB.
- **Qdrant (self-hosted via Docker Compose for dev; cloud later)**: chosen.

## Consequences

Two systems to operate. Cross-store consistency (e.g. deleting a Document must remove both its PostgreSQL rows and its Qdrant points) is owned by the application, not the database.
