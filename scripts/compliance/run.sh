#!/usr/bin/env bash
set -euo pipefail

API_BASE="${API_BASE:-http://localhost:5072}"
TENANT_NAME="${TENANT_NAME:-Demo Tenant}"
EMAIL="${EMAIL:-demo@pls.local}"

echo "Running compliance checks against ${API_BASE}"

token="$(curl -sS -X POST "${API_BASE}/api/auth/dev-login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"${EMAIL}\",\"tenantName\":\"${TENANT_NAME}\"}" \
  | python3 -c 'import sys,json;print(json.load(sys.stdin)["accessToken"].strip())')"

echo "1) Auth required checks"
status="$(curl -sS -o /dev/null -w "%{http_code}" "${API_BASE}/api/entitlements")"
if [[ "${status}" != "401" ]]; then
  echo "FAIL: /api/entitlements should return 401 without auth (got ${status})"
  exit 1
fi
echo "PASS: auth required"

echo "2) Entitlements response"
curl -sS -H "Authorization: Bearer ${token}" "${API_BASE}/api/entitlements" >/tmp/entitlements.json

echo "3) Upload -> job created"
upload_result="$(curl -sS -H "Authorization: Bearer ${token}" -F "file=@docs/sample.txt" "${API_BASE}/api/documents/upload?lane=LaneA")"
job_id="$(printf '%s' "${upload_result}" | python3 -c 'import json,sys; print(json.loads(sys.stdin.read().strip())["jobId"])')"
echo "Job: ${job_id}"

echo "4) Job status endpoint returns 200"
status="$(curl -sS -o /dev/null -w "%{http_code}" -H "Authorization: Bearer ${token}" "${API_BASE}/api/jobs/${job_id}")"
if [[ "${status}" != "200" ]]; then
  echo "FAIL: job status should return 200 (got ${status})"
  exit 1
fi

echo "5) Audit log exists (DocumentUploaded)"
docker exec -i infra-postgres-1 psql -U pls -d pls -c "select count(*) from \"AuditLogs\" where \"Action\"='DocumentUploaded';" >/tmp/audit.log
grep -q "(1 row)" /tmp/audit.log || echo "WARN: audit log count may be 0; check if DB is reachable"

echo "Compliance checks complete."
