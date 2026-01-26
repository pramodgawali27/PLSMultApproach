# Threat Model (Starter)

## Assets

- Medical documents in storage
- Plain-language summaries
- Tenant and user identity data

## Key risks

- Broken tenant isolation (cross-tenant data access)
- Document content leakage via logs
- Unauthorized lane access
- Malicious file uploads

## Mitigations

- Tenant and user claims enforced on every query
- No logging of document content; log IDs only
- Entitlement checks on job creation and orchestration
- Malware scanning hook present (replace with production scanner)

## Future enhancements

- Add WAF and rate limiting
- Encrypt storage at rest with tenant-specific keys
- Move secrets to managed vault
