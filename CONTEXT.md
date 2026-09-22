# TechDoc

TechDoc is a retrieval-augmented Q&A assistant for technical documentation: users upload documents (PDF, DOCX, TXT), ask questions in Vietnamese or English, and receive answers with exact file and page citations.

## Language

**Document (Tài liệu)**:
A technical file (PDF, DOCX, or TXT) uploaded into the system; the unit users manage and the source of all answers.
_Avoid_: file, upload

**Chunk (Trích đoạn)**:
A retrievable piece of a Document, carrying the page(s) it was extracted from.
_Avoid_: segment, passage, fragment

**Ingestion (Nạp liệu)**:
The end-to-end process that turns an uploaded Document into searchable Chunks.
_Avoid_: indexing, processing

**Ingestion Job (Công việc nạp liệu)**:
A tracked task that runs the Ingestion of one uploaded Document in the background, with a status users can check.
_Avoid_: queue, task, upload job

**Extraction (Trích xuất)**:
The Ingestion stage that pulls text out of a Document file, preserving page boundaries (line numbers for TXT).
_Avoid_: parsing, text extraction

**Chunking (Phân đoạn)**:
The Ingestion stage that groups extracted text into Chunks, following the document's heading structure where detectable.
_Avoid_: splitting, segmentation

**Embedding (Nhúng)**:
The Ingestion stage that computes the dense and sparse vectors carried by each Chunk.
_Avoid_: vectorization, indexing

**Citation (Trích dẫn)**:
A reference from an Answer to the Document and page(s) its content came from.
_Avoid_: source, reference

**Question (Câu hỏi)**:
What the user asks, in Vietnamese or English.
_Avoid_: query, prompt

**Answer (Câu trả lời)**:
The assistant's response to a Question, written in the language of the Question and backed by Citations.
_Avoid_: response, reply

**Session (Phiên hội thoại)**:
One continuous conversation: an ordered series of Questions and Answers in which follow-up Questions may rely on earlier turns.
_Avoid_: chat, thread, conversation

**Workspace (Không gian làm việc)**:
A scope that groups Documents and Sessions; the MVP runs a single default workspace.
_Avoid_: project, tenant

**Eval Runner (Bộ chạy đánh giá)**:
A development-only console tool that runs the Golden Set through the real pipeline and reports quality scores; never part of the product.
_Avoid_: CLI, test runner

**Golden Set (Bộ dữ liệu vàng)**:
A curated set of Questions with their expected Citations, drawn from real Documents, used to measure retrieval quality after pipeline changes.
_Avoid_: eval set, benchmark
