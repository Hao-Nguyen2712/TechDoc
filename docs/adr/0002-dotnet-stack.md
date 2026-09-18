# .NET as the single implementation stack

TechDoc is implemented in .NET (ASP.NET Core), reusing the existing `TechDocAI.Api` / `TechDocAI.Core` / `TechDocAI.Infrastructure` solution layout. Python (FastAPI + LlamaIndex/LangChain) was considered because of its richer RAG ecosystem, but rejected: the team knows .NET and prefers one language across API, ingestion pipeline, and tooling over ecosystem breadth.

## Consequences

PDF/DOCX parsing uses .NET libraries (e.g. PdfPig, OpenXML SDK) instead of the Python defaults; AI providers are swapped behind .NET abstractions rather than via Python frameworks.
