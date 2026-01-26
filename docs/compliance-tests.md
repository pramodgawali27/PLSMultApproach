# Compliance & Safety Test Suite

This project includes an automated baseline test script plus runtime audit logging.

## Standards Mapped
- Microsoft Responsible AI Standard
- OWASP ASVS + OWASP API Top 10
- SOC2-style controls (auditability, least privilege)
- HIPAA & GDPR (data minimization, access control, audit trails)

## Automated Test Script
Run:
```bash
bash scripts/compliance/run.sh
```

What it checks:
- Authentication required on protected endpoints (OWASP API1/ASVS V2)
- Entitlements endpoint responds for authorized users
- Upload creates a job and job status endpoint is reachable
- Audit log entries are created for key actions (SOC2/HIPAA/GDPR)

## Runtime Enforcement
- Guardrails apply in summarization-service; blocked output is rejected.
- Audit logs stored in DB for document upload, summary requests, downloads.

## Evidence Collection
Save outputs from:
- `scripts/compliance/run.sh`
- `docker logs infra-document-service-1`
- `docker logs infra-summarization-service-1`

## Gaps / Future Enhancements
- Add tenant data isolation tests for every endpoint
- Add static analysis (SAST/DAST) to CI
- Add DPIA checks for GDPR and PHI classification scanning
