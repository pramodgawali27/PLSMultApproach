# Responsible AI, Compliance, and Guardrails

This document describes the Responsible AI, compliance, and guardrail controls implemented in this PLS platform, with real runtime examples from the current codebase.

## Scope & Standards
- Microsoft Responsible AI Standard
- OWASP ASVS + OWASP API Top 10
- SOC2-style controls
- HIPAA + GDPR (data minimization, access control, audit trails)

## Data Flow (High-Level)
1. User uploads a document (tenant-scoped, user-scoped).
2. Document is stored in tenant storage; metadata saved in Postgres.
3. A job is created and orchestrated.
4. Summarization service extracts text, applies guardrails, and generates output.
5. Optional image extraction/generation is performed.
6. Summary is saved and available for download.

---

## Responsible AI Controls (Microsoft Responsible AI Standard)

### 1) Fairness & Inclusiveness
- Prompt instructs neutral, patient-friendly tone with inclusive language guidance.
- No personalized medical advice, no demographic targeting.

**Implementation**
- `shared/prompts/lane-a/summary.md.sbn` defines neutral tone, avoids advice, expands abbreviations.

**Example**
- “Do not provide medical advice. Avoid overclaiming.”

### 2) Reliability & Safety
- Guardrails block unsafe outputs (PII/PHI, toxicity, jailbreak)
- Summarization refuses outputs that violate safety checks.

**Implementation**
- Guardrail configuration: `shared/policies/guardrails.default.json`
- Enforced in summarization: `services/summarization-service/Program.cs`

**Runtime behavior**
- If guardrails fail: job returns HTTP 400 with “Guardrail policy blocked summarization.”

### 3) Transparency
- Model info and guardrail results stored in `ModelInfoJson`.
- Summaries include a source note + medical disclaimer.

**Implementation**
- Model info stored with guardrail summary in `ModelInfoJson`.
- Prompt includes “Source note” + “Medical disclaimer” sections.

### 4) Privacy & Security
- No document content logged.
- Tenant and user scoping on all endpoints.
- Audit logs record actions without raw document content.

**Implementation**
- API endpoints always filter by `tenantId` and `userId`.
- Audit logs in `AuditLogs` table.
- Metadata only (filename, content type, size).

### 5) Accountability
- All uploads, requests, and downloads are auditable.
- Correlation IDs for tracing.

**Implementation**
- Correlation middleware: `shared/building-blocks/Correlation/CorrelationIdMiddleware.cs`
- Audit logging: `services/document-service/Program.cs` (DbAuditLogger)

---

## Guardrails (Runtime Checks + Audit)

### Guardrail Pipeline
1. Extract text/markdown (Docling or fallback)
2. Evaluate guardrails on extracted content
3. If blocked → fail job; otherwise continue

**Implementation**
- Guardrail evaluator: `services/summarization-service/Guardrails/GuardrailEvaluator.cs`
- Policy config: `shared/policies/guardrails.default.json`

**Example (Blocked Output)**
If extracted text contains direct personal identifiers or unsafe instructions, the service returns:
```
HTTP 400: Guardrail policy blocked summarization.
```

### Audit Logs (Runtime + Compliance)
Actions logged:
- `DocumentUploaded`
- `SummaryRequested`
- `SummaryDownloaded`

**Implementation**
- `services/document-service/Program.cs` (DbAuditLogger)
- Table: `AuditLogs`

---

## Compliance Controls

### OWASP API Top 10 / ASVS
- Auth required on protected endpoints
- Tenant/user scoping on every request
- Input validation for file upload and lane selection

**Automated checks**
- `scripts/compliance/run.sh` verifies authentication required + job lifecycle endpoints.

### SOC2-Style Controls
- Audit logging (who did what, when)
- Correlation IDs for tracing across services
- Storage is scoped by tenant ID

### HIPAA / GDPR
- Minimal data retention (no raw document content in logs)
- Explicit disclaimer and source note
- Auditability for access and downloads

---

## Automated Compliance Tests

Run:
```bash
bash scripts/compliance/run.sh
```

Checks include:
- Auth required on protected endpoints
- Entitlements endpoint responds with valid token
- Upload creates job and job status endpoint responds
- Audit log entry exists for `DocumentUploaded`

Docs: `docs/compliance-tests.md`

---

## Real-Time Examples in Current Code

### Example 1: Guardrails Blocking Output
- Trigger: Document contains disallowed content (PII/PHI, toxicity)
- Behavior: Summarization service returns 400; job marked failed

### Example 2: Audit Log Record
- Trigger: Upload a document
- Entry:
  - `Action`: `DocumentUploaded`
  - `EntityType`: `Document`
  - `EntityId`: `<documentId>`
  - `MetadataJson`: filename, content type, size, jobId

### Example 3: Tenant Isolation
- All queries include `TenantId` + `UserId`
- Unauthorized tenant/user access returns `404` or `403`

---

## Gaps / Next Improvements
- Add GDPR data deletion workflow (right-to-erasure)
- Add PHI classification scanning (pre-LLM)
- Add DAST/SAST automation in CI
- Add per-tenant guardrail overrides with approval
