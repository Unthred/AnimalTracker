#!/usr/bin/env bash
set -Eeuo pipefail

# Update and restart AnimalTracker on UnRaid.
#
# Usage:
#   bash ./unraid-update.sh
#   bash ./unraid-update.sh main
#
# Optional env vars:
#   ALLOW_DIRTY=1        Allow running when repo has local changes.
#   TAIL_LOG_LINES=80    How many log lines to show after startup.

BRANCH="${1:-main}"
TAIL_LOG_LINES="${TAIL_LOG_LINES:-80}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "==> AnimalTracker update starting"
echo "    Repo: $SCRIPT_DIR"
echo "    Branch: $BRANCH"

if ! command -v git >/dev/null 2>&1; then
  echo "ERROR: git is not installed or not in PATH." >&2
  exit 1
fi

if ! command -v docker >/dev/null 2>&1; then
  echo "ERROR: docker is not installed or not in PATH." >&2
  exit 1
fi

if [ ! -f "docker-compose.yml" ]; then
  echo "ERROR: docker-compose.yml not found. Run this script from repo root." >&2
  exit 1
fi

if [ "${ALLOW_DIRTY:-0}" != "1" ] && [ -n "$(git status --porcelain)" ]; then
  echo "ERROR: Working tree has local changes."
  echo "       Commit/stash first, or run with ALLOW_DIRTY=1."
  exit 1
fi

echo "==> Fetching latest code"
git fetch origin "$BRANCH"

CURRENT_BRANCH="$(git rev-parse --abbrev-ref HEAD)"
if [ "$CURRENT_BRANCH" != "$BRANCH" ]; then
  echo "==> Switching branch: $CURRENT_BRANCH -> $BRANCH"
  git checkout "$BRANCH"
fi

echo "==> Pulling latest commits"
git pull --ff-only origin "$BRANCH"

echo "==> Pulling latest image"
docker compose pull animaltracker

echo "==> Recreating app container (short downtime cutover)"
docker compose up -d --no-deps --force-recreate animaltracker

echo "==> Container status"
docker compose ps

echo "==> Recent container logs"
docker compose logs --tail "$TAIL_LOG_LINES" animaltracker

echo "==> Update completed"
