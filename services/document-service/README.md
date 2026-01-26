# Document Service (A1)

Handles uploads, metadata, job creation, entitlements, and summary retrieval.

## Responsibilities
- Upload file to local storage or blob storage
- Create `Documents` + `SummaryJobs` records
- Enforce tenant + user scoping
- Provide list/search endpoints for summaries
- Render downloadable PDFs/DOCX

## Key Endpoints
- `POST /api/auth/dev-login` (dev only)
- `POST /api/documents/upload` (multipart form)
- `POST /api/summaries/{documentId}/generate?lane=X`
- `GET /api/jobs/{jobId}`
- `GET /api/summaries/{summaryId}`
- `GET /api/summaries` (search/filter)
- `GET /api/summaries/{summaryId}/download?format=pdf|docx`
- `GET /api/prompts/{lane}`

## Config (env)
- `ConnectionStrings__Postgres`
- `LocalStorage__BasePath`
- `DevJwt__SigningKey`

## Notes
- Migrations run on startup in Development.
- PDF export uses QuestPDF with styled sections + images.
- Images in PDF are pulled from `ModelInfoJson` (generated or extracted).
