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
API="https://api.render.com/v1/services/${SERVICE_ID}"

LOCAL="$(git rev-parse HEAD)"
REMOTE="$(git rev-parse origin/main 2>/dev/null || true)"
if [[ -n "$REMOTE" && "$LOCAL" != "$REMOTE" ]]; then
  echo "WARNING: HEAD ($LOCAL) is not origin/main ($REMOTE)."
  echo "Render will build whatever is already on GitHub main — push first if you need local commits."
fi

echo "==> Creating Render deploy for $SERVICE_ID"
deploy_id=""
deploy_out="$(render deploys create "$SERVICE_ID" --output text --confirm 2>&1)" || \
  deploy_out="$(yes | render deploys create "$SERVICE_ID" --output text 2>&1)" || true
echo "$deploy_out"
deploy_id="$(echo "$deploy_out" | grep -Eo 'dep-[a-z0-9]+' | head -1 || true)"

if [[ -z "$deploy_id" ]]; then
  echo "==> Could not parse deploy id from CLI; polling latest deploy for commit $LOCAL"
  deploy_json="$(curl -sS -H "Authorization: Bearer $RENDER_API_KEY" "${API}/deploys?limit=5")"
  deploy_id="$(python3 -c "import json,sys; d=json.load(sys.stdin); print(next((x['deploy']['id'] for x in d if x['deploy']['commit']['id'].startswith('${LOCAL:0:7}') or x['deploy']['commit']['id']=='$LOCAL'), ''))" <<<"$deploy_json")"
fi

if [[ -n "$deploy_id" ]]; then
  echo "==> Waiting for deploy $deploy_id"
  for i in $(seq 1 90); do
    status="$(curl -sS -H "Authorization: Bearer $RENDER_API_KEY" "${API}/deploys/${deploy_id}" | python3 -c "import json,sys; print(json.load(sys.stdin).get('status',''))")"
    echo "  poll $i: status=$status"
    case "$status" in
      live) break ;;
      update_failed|build_failed|canceled|deactivated)
        echo "ERROR: Render deploy $deploy_id ended with status=$status" >&2
        exit 1
        ;;
    esac
    sleep 10
  done
else
  echo "WARNING: no deploy id — falling back to health poll only"
fi

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
