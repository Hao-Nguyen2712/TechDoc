# In-process background ingestion without a message broker

The upload endpoint stores the Document binary, records the Document and its Ingestion Job in PostgreSQL, and returns immediately; the pipeline then runs in an in-process .NET `BackgroundService` consuming an in-memory `Channel`, one job at a time, updating job status in PostgreSQL. At MVP scale (~1,000 documents, sequential processing is friendly to provider rate limits) an external broker or job framework is another system to operate for no benefit, and a fire-and-forget `Task` would lose jobs on restart without backpressure.

## Considered Options

- **External message broker (RabbitMQ, cloud queue)**: rejected — extra infrastructure at a scale that doesn't need it.
- **Job framework (Hangfire)**: rejected — framework weight for a single sequential worker.
- **Fire-and-forget Task**: rejected — jobs vanish on process restart, no backpressure.
- **In-process Channel + BackgroundService**: chosen.

## Consequences

- A process crash loses the queued job from memory; the job row stays non-terminal and recovery is re-uploading the file (see ADR 0009).
- The design assumes a single API instance; running more than one requires revisiting job claiming before scaling out.
- Swapping in a broker later means replacing only the enqueue/consume seam — pipeline stages live in `TechDocAI.Core`/`TechDocAI.Infrastructure` and don't know how they are hosted.
