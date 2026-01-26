# Architecture

## Overview

PLS is a microservice-friendly starter with three core services:

- Document Service (A1): uploads files, stores metadata, issues summary jobs.
- Orchestrator Service (A2): picks queued jobs, enforces entitlements, calls summarization.
- Summarization Service (A3): implements lane strategies (Lane A in this starter).

Supporting components:

- Postgres for metadata and job state.
- Blob storage abstraction (local filesystem in dev).
- RabbitMQ/Azure Service Bus reserved for later integration.
- OpenTelemetry-ready correlation IDs.

## Lane model

- Lane A: prompt-only (implemented).
- Lane B: fine-tuned model (stubbed).
- Lane C: RAG (stubbed).
- Lane D: agentic (stubbed).

## Key flows

1. User logs in (dev JWT locally).
2. Document is uploaded, metadata stored, job created.
3. Orchestrator validates entitlements and triggers summarization.
4. Summarization returns summary; job and summary are persisted.

## Tenancy

- Every record is scoped by tenant_id and user_id claims.
- Subscriptions define lane entitlements (bitmask).

## Observability

- Correlation IDs attached to each request.
- Structured logs (default ASP.NET Core logging).
