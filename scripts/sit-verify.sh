#!/usr/bin/env bash
set -euo pipefail

API_URL="${1:-${SIT_API_URL:-https://be-localgo.onrender.com}}"
FE_URL="${2:-${SIT_FE_URL:-}}"

API_URL="${API_URL%/}"
HEALTH_URL="${API_URL}/api/health"

echo "==> Health: ${HEALTH_URL}"
curl -fsS "${HEALTH_URL}" | tee /tmp/localgo-sit-health.json
echo

status="$(node -pe "JSON.parse(require('fs').readFileSync('/tmp/localgo-sit-health.json','utf8')).status")"
if [[ "${status}" != "Healthy" && "${status}" != "Degraded" ]]; then
  echo "Unexpected health status: ${status}" >&2
  exit 1
fi

if [[ -n "${FE_URL}" ]]; then
  FE_URL="${FE_URL%/}/"
  echo "==> Frontend: ${FE_URL}"
  curl -fsSI "${FE_URL}" | head -n 1
fi

echo "SIT verify OK (${status})"
