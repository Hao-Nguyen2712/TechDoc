# Normalize DOCX to PDF at ingestion

The product promises citations with exact file and page, but DOCX has no stable page concept (pages only exist at render time) and TXT has none at all. Decision: the ingestion pipeline converts every DOCX to PDF via LibreOffice headless (run as a container) right after upload; PDF becomes the only format the parser handles, so citation logic exists once and page numbers are real. TXT chunks cite line numbers instead of pages.

## Considered Options

- **Heading-based citations for DOCX**: avoids the LibreOffice dependency but citations become non-uniform across file types.
- **Estimated page numbers**: rejected — inaccurate citations are worse than none.

## Consequences

- A non-.NET container (LibreOffice) joins the stack purely for format normalization.
- Page numbers match LibreOffice's rendering; a user opening the original DOCX in Word may see slightly different pagination.
