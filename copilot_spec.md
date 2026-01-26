You are GitHub Copilot acting as a senior full-stack engineer. Generate a production-ready starter web application for a “Plain Language Summary (PLS)” platform.

GOALS

1. A “base free UI” web app:
   - Login required
   - Upload a medical document (PDF/DOCX/TXT) from the front page
   - Click “Generate Summary”
   - Show job status (Queued/Processing/Completed/Failed)
   - After completion, show the summary
   - Provide a dashboard that lists all summaries for the logged-in user
   - Provide search/filter (by filename, date, lane, status)
   - Must be multi-tenant: tenant context is carried from login token/claims

2. Four-lane summary generation, gated by subscription:
   Lane A: Prompt Engineering (prompt-only, single call)
   Lane B: Fine-tuned model (use a separate deployed model)
   Lane C: RAG (vector retrieval grounding)
   Lane D: Agentic (multi-step orchestration)

   Tenants have a subscription plan that enables 1+ lanes. The UI must only show lanes that the tenant is entitled to.

3. Architecture (microservice-friendly, but keep it runnable locally):
   - API Gateway (optional in local compose)
   - A1 Document Service: upload file to storage + store metadata in DB + publish “DocumentUploaded” event
   - A2 Orchestrator: consumes events, validates entitlement, starts summarization job, updates status, retries on transient errors
   - A3 Summarization Service: implements the 4 lanes behind a strategy interface
   - Supporting: Blob storage (Azurite locally), DB (Postgres), message bus (Azure Service Bus in prod, local RabbitMQ for dev)
   - Observability: structured logs, correlation IDs, OpenTelemetry tracing

TECH STACK (choose these unless impossible)

- Frontend: React + TypeScript + Vite + Tailwind. Use a clean, simple UI.
- Backend: .NET 8 Web API (C#), minimal APIs or controllers OK. Use clean architecture-ish layering.
- Auth: Azure AD B2C in production; for local dev also support “local JWT dev auth”.
- DB: Postgres + EF Core migrations.
- Storage: Azure Blob in production; Azurite in local docker-compose.
- Messaging: Azure Service Bus in prod; RabbitMQ in dev.
- RAG: Azure AI Search in prod (or a pluggable interface); for local dev, use a simple in-memory vector store fallback.
- Agentic: Semantic Kernel agents (pluggable). In local dev simulate agent steps; keep the interface compatible with Azure AI Foundry agent service later.

REPO STRUCTURE (generate folders & projects exactly)
/
apps/
web/ (React UI)
services/
gateway/ (optional; can be a simple YARP reverse proxy)
document-service/ (A1)
orchestrator-service/ (A2)
summarization-service/ (A3)
shared/
contracts/ (events + DTOs)
building-blocks/ (logging, auth helpers, correlation, results)
infra/
docker-compose.yml
azurite/
postgres/
rabbitmq/
docs/
architecture.md
api.md
threat-model.md
run-local.md

DOMAIN MODEL (minimum required tables)

- Tenants: Id, Name, Status, CreatedAt
- Users: Id, TenantId, ExternalId (AAD B2C sub), Email, DisplayName, CreatedAt
- Subscriptions: Id, TenantId, PlanName, EnabledLanes (bitmask or join table), TokenQuotaMonthly, ActiveFrom, ActiveTo
- Documents: Id, TenantId, UserId, FileName, ContentType, SizeBytes, StorageUri, UploadedAt, Status
- SummaryJobs: Id, TenantId, UserId, DocumentId, Lane, Status, RequestedAt, StartedAt, CompletedAt, ErrorMessage, CorrelationId
- Summaries: Id, TenantId, UserId, DocumentId, JobId, Lane, SummaryText, ModelInfoJson, CreatedAt
- AuditLogs: Id, TenantId, UserId, Action, EntityType, EntityId, Timestamp, MetadataJson

SECURITY REQUIREMENTS

- Every request is tenant-scoped and user-scoped.
- Enforce entitlement checks in backend (do NOT trust UI).
- Store files in tenant-specific containers or prefix path by tenantId.
- Do not log document content. Only log documentId/jobId and small metadata.
- Add basic malware scanning hook placeholder (interface, not full implementation).

API REQUIREMENTS (minimum)
POST /api/auth/dev-login (dev only) -> returns JWT with tenantId, userId claims
POST /api/documents/upload -> returns documentId + jobId
POST /api/summaries/{documentId}/generate?lane=X -> returns jobId
GET /api/jobs/{jobId} -> status
GET /api/summaries/{summaryId} -> summary text + metadata
GET /api/summaries?search=...&lane=...&status=... -> list
GET /api/entitlements -> lanes allowed for current tenant

LANE IMPLEMENTATION DETAILS
Lane A Prompt-only:

- Extract text from document (PDF/DOCX/TXT). Keep it simple: use a text extraction library and fallback to “not supported”.
- Call an LLM provider through an interface ILLMClient. For now, stub with a fake provider for local run.
- Use a strong system prompt for plain-language medical summaries: explain jargon, short sentences, disclaimers.

Lane B Fine-tuned:

- Same as lane A but uses a different model deployment name (config). Keep it behind ILLMClient.

Lane C RAG:

- Chunk extracted text
- Generate embeddings (interface IEmbeddingClient)
- Store/search embeddings (interface IVectorStore)
- Retrieve top-K chunks and ground the prompt (“Answer only using provided context; cite chunk ids”)
- Save retrieval metadata in ModelInfoJson

Lane D Agentic:

- Use Semantic Kernel style “skills” interfaces:
  SummarizerSkill, KnowledgeSkill (RAG wrapper), EvaluatorSkill, CoordinatorSkill
- Pipeline: preprocess -> retrieve (optional) -> summarize -> evaluate readability/factuality -> revise -> final
- Save agent trace summary (NOT raw content) into ModelInfoJson
- Keep the agent runner pluggable so Azure AI Foundry Agent Service can replace local runner later.

UI REQUIREMENTS

- Pages: Login, Upload+Generate (home), Dashboard (list/search), Summary detail page
- On home: upload control, lane dropdown (only entitled lanes), “Generate” button, progress indicator
- On dashboard: table + search box + filters + click to open summary detail
- Make it responsive and minimal.
- Handle errors and display friendly messages.

DELIVERABLES

- Generate full code with README docs in /docs
- Provide docker-compose that starts web + services + postgres + azurite + rabbitmq
- Provide seed data for tenants/subscriptions/users in dev
- Ensure migrations run on startup in dev (or provide a script)
- Add unit tests for entitlement gating + job state transitions
- Add integration test skeleton for upload->job->summary

When you write code:

- Use correlation IDs and structured logging
- Use dependency injection throughout
- Keep config in appsettings + env vars
- Implement clean interfaces so we can swap Azure services later
