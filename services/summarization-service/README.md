# Summarization Service (A3)

Implements the summarization pipeline (Lane A in this starter) with Docling extraction, guardrails, prompt templates, and optional image generation.

## Responsibilities
- Extract text/markdown using Docling (preferred) or local extractors
- Apply guardrails
- Render prompt templates (Scriban)
- Call Azure OpenAI for summarization
- Parse image prompts and optionally generate images
- Save extracted/generated image metadata into `ModelInfoJson`

## Config (env)
- `AZURE_OPENAI_ENDPOINT`
- `AZURE_OPENAI_API_VERSION`
- `AZURE_OPENAI_DEPLOYMENT_PROMPT`
- `AZURE_OPENAI_KEY` (dev)
- `AZURE_OPENAI_DEPLOYMENT_IMAGE` (optional; DALL·E deployment)
- `AZURE_CLIENT_ID` (optional for managed identity)
- `DOCLING_URL` (default in compose: `http://docling-service:8080`)
- `USE_DOCLING` (default: true)
- `ENABLE_IMAGE_PLACEHOLDERS` (default: true)

## Prompts & Guardrails
- Prompts: `shared/prompts/lane-a/summary.md.sbn`
- Guardrails: `shared/policies/guardrails.default.json`

## Notes
- Lane A only in this starter.
- If DALL·E isn’t configured, placeholder images are used.
