# PLS UI Guide

## Route Map
- `/login` — Dev login
- `/app` — Upload + generate
- `/app/dashboard` — Summary dashboard
- `/app/summaries/:id` — Summary detail
- `/app/settings` — Tenant plan & usage

## Screens (placeholders)
- `Login`: tenant + environment labels
- `Home`: left upload panel, right job status
- `Dashboard`: searchable table with filters
- `Summary detail`: summary + evidence tabs
- `Settings`: plan + usage

## Component Inventory
- `LaneCard`
- `UploadDropzone`
- `StatusBadge`
- `SummaryRenderer`
- `EvidencePanel`

## Notes
- Lanes are gated by `/api/entitlements`.
- Job status uses polling (2.5s).
- Summary viewer renders text blocks safely.
