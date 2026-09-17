# Gemini API for LLM and embeddings

Both answer generation and embeddings run on the Gemini API (key already available). Ollama self-hosting was considered and rejected for the MVP: it costs GPU setup and operations, while Gemini ships faster and has vision support needed by Ingestion. All provider calls go through .NET AI abstractions so switching to local models later is a configuration change, not a code change.

## Considered Options

- **Gemini API (LLM + embeddings)**: chosen — fast to ship, good Vietnamese, vision-capable.
- **Ollama (local models)**: rejected for MVP; kept as the swap path if data-privacy requirements change or API costs grow.

## Consequences

- Document content is sent to Google's API during Ingestion and Question answering; data-privacy is an accepted trade-off.
- The embedding model identity is versioned on every Chunk. Switching embedding models requires full re-ingestion of the corpus (~1,000 documents).
- Gemini vision doubles as the OCR engine for images and scanned pages during Ingestion (chosen over Tesseract for Vietnamese quality; no separate OCR library in the stack).
