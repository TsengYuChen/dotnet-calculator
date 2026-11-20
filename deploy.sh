#!/usr/bin/env bash
# Builds and (re)starts the local deployment from the current working tree (design §8).
# Does not run git pull: this machine is also the development machine (ALT-G1).
set -euo pipefail

cd "$(dirname "$0")"

if [[ ! -f .env ]]; then
  echo "ERROR: .env not found. Copy .env.example to .env and fill in the values (see README.md)." >&2
  exit 1
fi

# Version shown by /health/live and /health/ready; -dirty when the working tree has uncommitted changes.
GIT_SHA="$(git rev-parse --short=12 HEAD 2>/dev/null || echo unknown)"
if [[ "$GIT_SHA" != unknown && -n "$(git status --porcelain 2>/dev/null)" ]]; then
  GIT_SHA="${GIT_SHA}-dirty"
fi
export GIT_SHA

POSTGRES_IMAGE="$(tr -d '[:space:]' < postgres-image.txt)"
if [[ -z "$POSTGRES_IMAGE" ]]; then
  echo "ERROR: postgres-image.txt is empty." >&2
  exit 1
fi
export POSTGRES_IMAGE

echo "Deploying GIT_SHA=${GIT_SHA} with ${POSTGRES_IMAGE}"
docker compose up -d --build
docker compose ps
