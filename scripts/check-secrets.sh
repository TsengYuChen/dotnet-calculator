#!/usr/bin/env bash
# Repository secret checks (run by the pre-commit hook and CI).
#
#   scripts/check-secrets.sh            check all tracked files
#   scripts/check-secrets.sh --staged   check only staged files (pre-commit hook)
#
# Fails when:
#   1. a `.env` file (in any folder) is tracked / staged;
#   2. a *.cs, *.ts, *.vue, *.md, *.yml or *.yaml file contains an e-mail address
#      outside the RFC 2606 / RFC 6761 reserved domains
#      (example.com, example.net, example.org, *.example, *.test, *.invalid, *.localhost).
set -euo pipefail

mode="all"
case "${1:-}" in
  "") ;;
  --staged) mode="staged" ;;
  -h|--help) sed -n '2,12p' "$0"; exit 0 ;;
  *) echo "usage: $0 [--staged]" >&2; exit 2 ;;
esac

cd "$(git rev-parse --show-toplevel)"

if [[ "$mode" == "staged" ]]; then
  # Added, copied, modified or renamed files in the index.
  mapfile -d '' files < <(git diff --cached --name-only --diff-filter=ACMR -z)
else
  mapfile -d '' files < <(git ls-files -z)
fi

read_file() {
  if [[ "$mode" == "staged" ]]; then
    git show ":$1"
  else
    cat -- "$1"
  fi
}

failed=0

# 1. .env files must never be committed.
for f in "${files[@]}"; do
  if [[ "$f" =~ (^|/)\.env$ ]]; then
    echo "ERROR: '$f' is tracked by git. Remove it with: git rm --cached '$f'" >&2
    failed=1
  fi
done

# 2. Real e-mail addresses in source and docs.
email_re='[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}'
reserved_re='@([A-Za-z0-9-]+\.)*(example\.(com|net|org)|example|test|invalid|localhost)$'

for f in "${files[@]}"; do
  case "$f" in
    *.cs|*.ts|*.vue|*.md|*.yml|*.yaml) ;;
    *) continue ;;
  esac
  [[ "$mode" == "all" && ! -f "$f" ]] && continue
  while IFS= read -r hit; do
    [[ -z "$hit" ]] && continue
    line="${hit%%:*}"
    addr="${hit#*:}"
    addr_lc="$(printf '%s' "$addr" | tr '[:upper:]' '[:lower:]')"
    if [[ ! "$addr_lc" =~ $reserved_re ]]; then
      echo "ERROR: $f:$line contains a non-reserved e-mail address (use @example.com)." >&2
      failed=1
    fi
  done < <(read_file "$f" | grep -noE "$email_re" || true)
done

if [[ "$failed" -ne 0 ]]; then
  echo "check-secrets: FAILED" >&2
  exit 1
fi
echo "check-secrets: OK (${#files[@]} files checked, mode=$mode)"
