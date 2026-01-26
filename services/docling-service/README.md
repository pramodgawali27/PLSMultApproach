# Docling Service

Python service that converts input documents to Markdown and extracts images for downstream summarization and PDF rendering.

## Endpoints
- `GET /health`
- `POST /extract` (multipart form; returns markdown + images)

## Config
- No required env vars

## Notes
- If OCR errors appear ("No OCR engine found"), add OCR deps to the container (e.g., tesseract). The pipeline will fall back to the built-in extractor if Docling fails.
