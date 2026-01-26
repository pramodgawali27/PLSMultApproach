# Orchestrator Service (A2)

Polls summary jobs, enforces entitlements, and calls the summarization service.

## Responsibilities
- Fetch queued jobs
- Validate tenant entitlement
- Call summarization-service
- Update job + document status

## Config (env)
- `ConnectionStrings__Postgres`
- `SummarizationService__BaseUrl`

## Behavior
- Polls queued jobs on a short interval
- Retries by re-queueing when failures occur (basic)
