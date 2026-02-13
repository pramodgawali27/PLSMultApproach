# Run Local

## Prereqs

- .NET 8 SDK
- Node.js 20+
- Docker (optional for infra)

## Start infra

```bash
cd infra
docker compose up -d
```

## Run services

```bash
dotnet run --project services/document-service

dotnet run --project services/orchestrator-service

dotnet run --project services/summarization-service
```

## Run web

```bash
cd apps/web
npm install
npm run dev
```

## Notes

- The Document Service seeds a demo tenant and user on startup in Development.
- Dev login issues a local JWT for UI use.
- Lane A and Lane C are implemented.
- For Azure OpenAI, export env vars like `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_DEPLOYMENT_PROMPT`, `AZURE_OPENAI_DEPLOYMENT_IMAGE`, `AZURE_OPENAI_EMBEDDING_DEPLOYMENT`, and `AZURE_OPENAI_KEY`.
- To use Ollama locally, set `LLM_PROVIDER=ollama` and optionally `OLLAMA_BASE_URL` (default `http://host.docker.internal:11434`) and `OLLAMA_MODEL` (default `llama3.1`).
- Lane C (RAG) config:
  - `RAG_VECTORSTORE=memory` for dev, `RAG_VECTORSTORE=azure` for Azure AI Search
  - `RAG_EMBEDDINGS=local` for dev (deterministic embeddings) or `RAG_EMBEDDINGS=azure` for Azure OpenAI embeddings
  - `AZURE_SEARCH_ENDPOINT`, `AZURE_SEARCH_INDEX_NAME`, `AZURE_SEARCH_KEY` (required when `RAG_VECTORSTORE=azure`)
  - `AZURE_OPENAI_EMBEDDING_DEPLOYMENT` (required when `RAG_EMBEDDINGS=azure`)
  - `RAG_CHUNK_SIZE` (default 1000), `RAG_CHUNK_OVERLAP` (default 150)
  - `RAG_STORE_TEXT=false` to avoid storing raw chunks in Postgres
