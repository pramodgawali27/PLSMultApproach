# Plain Language Summary (PLS) Platform

Production-ready starter for a multi-tenant Plain Language Summary (PLS) platform. It ingests medical documents, runs a summarization pipeline, and serves summaries with PDFs, prompt customization, and image extraction/generation support.

## What’s Included
- React + Vite + Tailwind frontend
- .NET 8 microservices: document, orchestrator, summarization
- Docling service for Markdown extraction + images
- Postgres, Azurite, RabbitMQ
- Guardrails + Scriban prompt templates
- PDF export with styled sections and images

## Repo Structure
```
apps/
  web/
services/
  docling-service/
  document-service/
  orchestrator-service/
  summarization-service/
  gateway/
shared/
  contracts/
  building-blocks/
infra/
  docker-compose.yml
  azurite/
  postgres/
  rabbitmq/
docs/
```

## Services (README links)
- Document Service: `services/document-service/README.md`
- Orchestrator Service: `services/orchestrator-service/README.md`
- Summarization Service: `services/summarization-service/README.md`
- Docling Service: `services/docling-service/README.md`
- Gateway (optional): `services/gateway/README.md`

## Core Flow
1. User logs in (dev JWT).
2. Upload a document -> document-service stores file + metadata and creates a job.
3. Orchestrator polls queued jobs and calls summarization-service.
4. Summarization-service extracts Markdown, runs prompt, and optionally generates images.
5. Summary is saved; PDF export renders styled sections + images.

## Local Run
1. Add `infra/.env` with at least:
   - `AZURE_OPENAI_ENDPOINT`
   - `AZURE_OPENAI_DEPLOYMENT_PROMPT`
   - `AZURE_OPENAI_API_VERSION`
   - `AZURE_OPENAI_KEY` (dev)
   - Optional: `AZURE_OPENAI_DEPLOYMENT_IMAGE`
2. Start services:
   ```bash
   docker compose -f infra/docker-compose.yml up -d --build
   ```
3. Open UI: `http://localhost:5173`

## Dev Login
Use the login page (dev auth). The JWT is stored in local storage as `pls_token`.

## Prompt Templates & Guardrails
- Prompts: `shared/prompts/`
- Guardrails: `shared/policies/guardrails.default.json`

## Docs
- Architecture: `docs/architecture.md`
- API: `docs/api.md`
- Threat model: `docs/threat-model.md`
- Run local: `docs/run-local.md`

## Troubleshooting
- 401 errors: re-login and ensure token is valid.
- 500 upload errors: ensure DB migrations ran (document-service). If needed, restart the service.
- Docling 500 errors: container needs OCR dependencies (see docling README).

## Notes
- Lane A is implemented; other lanes are stubbed for future expansion.
- Images are sourced from Docling extraction and/or generated prompts when configured.
