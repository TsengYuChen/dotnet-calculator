# VehicleGo

Personal vehicle management (first version: inspection periods). Design: [`docs/designs/vehicle_management.md`](docs/designs/vehicle_management.md).

## Prerequisites

- .NET SDK pinned in `global.json` (10.0.115, roll forward to the latest feature band)
- gitleaks at the version in `.gitleaks-version` (needed by the pre-commit hook)

## Getting started

```bash
# Enable the repository hooks (secret checks + gitleaks before each commit)
git config core.hooksPath .githooks

# Local tools (dotnet-ef)
dotnet tool restore

# Build and run the unit tests
dotnet build VehicleGo.slnx
dotnet test tests/VehicleGo.UnitTests
```

## Configuration

Copy `.env.example` to `.env` (never commit `.env`) and fill in the values.
Generate `MASTER_KEY` with `openssl rand -base64 32`. Use only `@example.com` addresses in any committed file.

## Secret checks

```bash
bash scripts/check-secrets.sh            # all tracked files
bash scripts/check-secrets.sh --staged   # staged files only (used by the hook)
```

CI runs `check-secrets.sh` and a pinned, checksum-verified gitleaks over the full history.

Deployment (Docker Compose, `deploy.sh`), Google OAuth setup and migrations are documented as they are added.
