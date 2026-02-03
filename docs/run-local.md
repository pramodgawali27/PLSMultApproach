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
- Lane A is implemented; other lanes return "not implemented".
- For Azure OpenAI, export env vars like `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_DEPLOYMENT_PROMPT`, and `AZURE_OPENAI_KEY`.
- To use Ollama locally, set `LLM_PROVIDER=ollama` and optionally `OLLAMA_BASE_URL` (default `http://host.docker.internal:11434`) and `OLLAMA_MODEL` (default `llama3.1`).
