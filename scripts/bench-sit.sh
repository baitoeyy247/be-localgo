#!/usr/bin/env bash
# Measure warm API latency on SIT (QA28 L-5). Requires AUTH_TOKEN and sample IDs.
set -euo pipefail

API_URL="${API_URL:-https://be-localgo-ul36.onrender.com}"
API_URL="${API_URL%/}"
AUTH_TOKEN="${AUTH_TOKEN:-}"
REQUEST_ID="${REQUEST_ID:-}"
APPOINTMENT_ID="${APPOINTMENT_ID:-}"

if [[ -z "${AUTH_TOKEN}" ]]; then
  echo "Set AUTH_TOKEN (Bearer dev/admin token) to run benchmarks." >&2
  exit 1
fi

auth=(-H "Authorization: Bearer ${AUTH_TOKEN}")

time_curl() {
  local label="$1"
  shift
  local url="$1"
  shift
  echo "==> ${label}"
  echo "    ${url}"
  # shellcheck disable=SC2068
  curl -sS -o /dev/null -w "    warm: %{time_total}s (HTTP %{http_code})\n" \
    "${auth[@]}" "$@" "${url}"
  curl -sS -o /dev/null -w "    repeat: %{time_total}s (HTTP %{http_code})\n" \
    "${auth[@]}" "$@" "${url}"
}

echo "LocalGo SIT bench — ${API_URL}"
echo "Document warm repeat times; target <1s when DB is warm (QA28 AC)."

time_curl "GET /api/health" "${API_URL}/api/health"

if [[ -n "${REQUEST_ID}" ]]; then
  time_curl "GET service request detail" \
    "${API_URL}/api/service-requests/${REQUEST_ID}"
  time_curl "GET appointment by request" \
    "${API_URL}/api/service-requests/${REQUEST_ID}/appointment"
fi

time_curl "GET provider matching inbox" \
  "${API_URL}/api/provider/service-requests/matching"

if [[ -n "${APPOINTMENT_ID}" ]]; then
  time_curl "PATCH appointment status (dry GET by id)" \
    "${API_URL}/api/appointments/${APPOINTMENT_ID}"
fi

echo "Done. Compare repeat (2nd) curl times after deploy."
