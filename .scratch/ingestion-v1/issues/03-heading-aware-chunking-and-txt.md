# 03: Structure-aware Chunking and TXT support

**What to build:** Replace the placeholder chunker with the real one: Chunks follow the document's heading structure (detected via font heuristics), are merged up to ~800–1,000 tokens with no overlap, and carry their heading path; documents with no detectable structure fall back to block cutting with an empty heading path. TXT Documents ingest end-to-end with line numbers as their citation basis. From the user's perspective: a well-structured PDF produces coherent section-sized Chunks, and plain-text files are just as ingitable.

**Blocked by:** 02 — Tracer bullet: upload a PDF and watch it become a Document with Chunks.

**Status:** ready-for-agent

- [ ] PDFs with detectable headings are chunked by section, merged to the token budget, no overlap; every Chunk records its heading path
- [ ] The font heuristic treats the dominant size as body text and short, markedly larger lines as headings
- [ ] Documents without detectable structure fall back to block cutting with an empty heading path
- [ ] TXT Documents ingest end-to-end; Chunks record line numbers instead of pages (per ADR 0004)
- [ ] The token budget is estimated locally (no provider round-trip per Chunk)
- [ ] The chunker, heading heuristic, and tokenizer are covered by direct unit tests (pure logic, no new seam)
