#!/usr/bin/env bash
# Trigger a Render deploy of be-localgo from GitHub main (does not upload local tree).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

export PATH="$HOME/.local/bin:$HOME/.local/node_modules/.bin:$PATH"

if [[ -f "$HOME/.config/localgo/deploy.env" ]]; then
  # shellcheck disable=SC1091
  source "$HOME/.config/localgo/deploy.env"
fi

: "${RENDER_API_KEY:?RENDER_API_KEY missing — set in ~/.config/localgo/deploy.env}"
export RENDER_API_KEY

SERVICE_ID="${RENDER_SERVICE_ID:-srv-dbbp7io473hc738auofg}"
HEALTH_URL="${HEALTH_URL:-https://be-localgo-ul36.onrender.com/api/health}"

LOCAL="$(git rev-parse HEAD)"
REMOTE="$(git rev-parse origin/main 2>/dev/null || true)"
if [[ -n "$REMOTE" && "$LOCAL" != "$REMOTE" ]]; then
  echo "WARNING: HEAD ($LOCAL) is not origin/main ($REMOTE)."
  echo "Render will build whatever is already on GitHub main — push first if you need local commits."
fi

echo "==> Creating Render deploy for $SERVICE_ID"
# Non-interactive: confirm via yes pipe if CLI prompts
render deploys create "$SERVICE_ID" --output text --confirm 2>&1 || \
  yes | render deploys create "$SERVICE_ID" --output text 2>&1

echo "==> Waiting for health: $HEALTH_URL"
ok=0
for i in $(seq 1 60); do
  body="$(curl -sS -m 20 "$HEALTH_URL" || true)"
  if echo "$body" | grep -qiE '"status"\s*:\s*"(Healthy|Degraded)"|Healthy|Degraded'; then
    echo "$body"
    ok=1
    break
  fi
  echo "  poll $i: not ready yet"
  sleep 10
done

if [[ "$ok" -ne 1 ]]; then
  echo "ERROR: health check did not become Healthy/Degraded" >&2
  exit 1
fi

echo "OK: backend deploy healthy (or degraded with Redis optional)"
