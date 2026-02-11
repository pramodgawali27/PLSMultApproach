# Production Configuration Reference

This file lists environment variables required for production deployments and where they are used.

## Frontend (web)
| Variable | Description | Example |
|---|---|---|
| `API_BASE_URL` | Runtime API base URL used to generate `/config.json` at container start | `https://pls-document-service.<region>.azurecontainerapps.io` |

Runtime override (no rebuild):
- `apps/web/public/config.json` with:
  ```json
  { "apiBaseUrl": "https://api.yourdomain.com" }
  ```

## Document Service
| Variable | Description | Example |
|---|---|---|
| `ConnectionStrings__Postgres` | Postgres connection string | `Host=...;Database=pls;Username=pls;Password=...` |
| `SummarizationService__BaseUrl` | Internal URL for summarization service | `http://pls-summarization-service` |
| `Storage__Provider` | `Local` or `Blob` | `Blob` |
| `Storage__Blob__ConnectionString` | Azure Blob connection string | `DefaultEndpointsProtocol=...` |
| `Storage__Blob__Container` | Blob container name | `plsfiles` |

## Orchestrator Service
| Variable | Description | Example |
|---|---|---|
| `ConnectionStrings__Postgres` | Postgres connection string | `Host=...;Database=pls;Username=pls;Password=...` |
| `SummarizationService__BaseUrl` | Internal URL for summarization service | `http://pls-summarization-service` |

## Summarization Service (Azure OpenAI)
| Variable | Description | Example |
|---|---|---|
| `AZURE_OPENAI_ENDPOINT` | Azure OpenAI endpoint | `https://<resource>.openai.azure.com/` |
| `AZURE_OPENAI_KEY` | Azure OpenAI API key | `****` |
| `AZURE_OPENAI_DEPLOYMENT_PROMPT` | Prompt deployment name | `pls-prompt` |
| `AZURE_OPENAI_DEPLOYMENT_FINETUNE` | Fine-tuned deployment name | `pls-finetune` |
| `AZURE_OPENAI_DEPLOYMENT_AGENT` | Agent deployment name | `pls-agent` |
| `AZURE_OPENAI_DEPLOYMENT_IMAGE` | Image deployment name | `pls-image` |

## Optional: Ollama
| Variable | Description | Example |
|---|---|---|
| `LLM_PROVIDER` | `azure` or `ollama` | `ollama` |
| `OLLAMA_BASE_URL` | Ollama API base URL | `http://host.docker.internal:11434` |
| `OLLAMA_MODEL` | Ollama model name | `llama3.2:latest` |

## GitHub Secrets (recommended)
Store all production values in GitHub Secrets and reference them in workflows.

- `AZURE_CREDENTIALS`
- `ACR_LOGIN_SERVER`
- `ACR_USERNAME`
- `ACR_PASSWORD`
- `POSTGRES_CONN`
- `SUMMARIZATION_URL`
- `API_BASE_URL`
- `AZURE_OPENAI_ENDPOINT`
- `AZURE_OPENAI_KEY`
- `AZURE_OPENAI_DEPLOYMENT_PROMPT`
- `AZURE_OPENAI_DEPLOYMENT_FINETUNE`
- `AZURE_OPENAI_DEPLOYMENT_AGENT`
- `AZURE_OPENAI_DEPLOYMENT_IMAGE`
