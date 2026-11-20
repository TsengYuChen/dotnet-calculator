#!/usr/bin/env bash
# End-to-end smoke test of the Docker Compose deployment (design §8, Verification Plan).
#
#   scripts/smoke-test.sh          deploy with ./deploy.sh, then check the running api
#   scripts/smoke-test.sh --down   same, then remove the containers and volumes (docker compose down -v)
#
# Needs a filled-in .env (fake Google credentials are enough: the login check only follows
# the redirect to Google, it does not sign in).
set -euo pipefail

down=0
case "${1:-}" in
  "") ;;
  --down) down=1 ;;
  -h|--help) sed -n '2,8p' "$0"; exit 0 ;;
  *) echo "usage: $0 [--down]" >&2; exit 2 ;;
esac

cd "$(git rev-parse --show-toplevel)"
# docker compose refuses to run without it (see docker-compose.yml); deploy.sh exports the same value.
export POSTGRES_IMAGE="${POSTGRES_IMAGE:-$(tr -d "[:space:]" < postgres-image.txt)}"

base="${SMOKE_BASE_URL:-http://localhost:8080}"
timeout_s="${SMOKE_TIMEOUT:-180}"
failures=0
tmp="$(mktemp -d)"

cleanup() {
  rm -rf "$tmp"
  if [[ "$down" == 1 ]]; then
    echo "==> docker compose down -v"
    docker compose down -v || true
  fi
}
trap cleanup EXIT

pass() { echo "PASS: $*"; }
fail() { echo "FAIL: $*" >&2; failures=$((failures + 1)); }

echo "==> ./deploy.sh"
./deploy.sh

echo "==> waiting for the api container to be healthy (up to ${timeout_s}s)"
deadline=$((SECONDS + timeout_s))
status=""
while (( SECONDS < deadline )); do
  cid="$(docker compose ps -q api 2>/dev/null || true)"
  if [[ -n "$cid" ]]; then
    status="$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$cid" 2>/dev/null || true)"
    [[ "$status" == "healthy" ]] && break
  fi
  sleep 3
done
if [[ "$status" != "healthy" ]]; then
  echo "api did not become healthy (last status: '${status:-none}'); recent logs:" >&2
  docker compose logs --tail 60 api >&2 || true
  exit 1
fi
pass "api container is healthy"

# request <name> <path> → writes $tmp/<name>.{status,headers,body}
request() {
  curl -sS --noproxy '*' -o "$tmp/$1.body" -D "$tmp/$1.headers" -w '%{http_code}' "$base$2" > "$tmp/$1.status" || echo 000 > "$tmp/$1.status"
}
status_of() { cat "$tmp/$1.status"; }
header_of() { grep -i "^$2:" "$tmp/$1.headers" | head -n1 | cut -d' ' -f2- | tr -d '\r'; }

# 1. Liveness: 200 JSON with gitSha.
request live /health/live
if [[ "$(status_of live)" == 200 ]] && grep -q '"gitSha":"[^"]\+"' "$tmp/live.body"; then
  pass "/health/live → 200 $(cat "$tmp/live.body")"
else
  fail "/health/live → $(status_of live) $(cat "$tmp/live.body")"
fi

# 2. Readiness (database reachable, migrations applied).
request ready /health/ready
if [[ "$(status_of ready)" == 200 ]] && grep -q '"status":"Healthy"' "$tmp/ready.body"; then
  pass "/health/ready → 200 $(cat "$tmp/ready.body")"
else
  fail "/health/ready → $(status_of ready) $(cat "$tmp/ready.body")"
fi

# 3. The SPA's index.html (static files from wwwroot) at / and at a client-side route.
for path in / /login; do
  name="spa$(echo "$path" | tr -c 'a-z' '_')"
  request "$name" "$path"
  if [[ "$(status_of "$name")" == 200 ]] && grep -q '<div id="app"></div>' "$tmp/$name.body" \
     && grep -q '<title>車輛檢驗期間</title>' "$tmp/$name.body"; then
    pass "$path → 200 SPA index.html"
  else
    fail "$path → $(status_of "$name") (expected the SPA index.html)"
  fi
done

# 3b. A built asset referenced by index.html is served.
asset="$(grep -o '/assets/[^"]*\.js' "$tmp/spa__.body" | head -n1 || true)"
if [[ -n "$asset" ]]; then
  request asset "$asset"
  if [[ "$(status_of asset)" == 200 ]] && [[ "$(header_of asset Content-Type)" == *javascript* ]]; then
    pass "$asset → 200 $(header_of asset Content-Type)"
  else
    fail "$asset → $(status_of asset) $(header_of asset Content-Type)"
  fi
else
  fail "no /assets/*.js reference in index.html"
fi

# 4. API without a cookie: 401 ProblemDetails, not a redirect to Google.
request me /api/me
if [[ "$(status_of me)" == 401 ]] && [[ "$(header_of me Content-Type)" == application/problem+json* ]]; then
  pass "/api/me → 401 $(header_of me Content-Type)"
else
  fail "/api/me → $(status_of me) $(header_of me Content-Type) $(header_of me Location)"
fi

# 5. Login: 302 to Google.
request login /api/auth/login
location="$(header_of login Location)"
if [[ "$(status_of login)" == 302 ]] && [[ "$location" == https://accounts.google.com/* ]]; then
  pass "/api/auth/login → 302 ${location%%\?*}"
else
  fail "/api/auth/login → $(status_of login) Location='${location}'"
fi

# 6. The container runs as the non-root app user.
uid="$(docker exec "$cid" id -u)"
if [[ "$uid" != 0 ]]; then
  pass "api runs as uid $uid (non-root)"
else
  fail "api runs as root"
fi

if (( failures > 0 )); then
  echo "==> $failures check(s) failed" >&2
  exit 1
fi
echo "==> all smoke checks passed"
