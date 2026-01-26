# API

Base URL (local): `http://localhost:5072`

## Auth

- `POST /api/auth/dev-login`
  - Body: `{ "email": "demo@pls.local", "tenantName": "Demo Tenant" }`
  - Response: JWT and tenant/user IDs

## Documents

- `POST /api/documents/upload?lane=LaneA`
  - Multipart form-data: `file`
  - Response: `{ documentId, jobId }` (job is queued immediately)

## Summaries

- `POST /api/summaries/{documentId}/generate?lane=LaneA`
  - Response: `{ jobId }`

- `GET /api/jobs/{jobId}`
  - Response: `{ jobId, status, errorMessage }`

- `GET /api/summaries/{summaryId}`
  - Response: summary text + metadata

- `GET /api/summaries?search=&lane=&status=`
  - Response: list of summaries for current user

- `GET /api/entitlements`
  - Response: enabled lanes for tenant
