# VehicleGo

Personal vehicle management (first version: inspection periods). Design: [`docs/designs/vehicle_management.md`](docs/designs/vehicle_management.md).

## Prerequisites

- Docker with the Compose plugin (`docker compose`)
- .NET SDK pinned in `global.json` (10.0.115, roll forward to the latest feature band) — for development and tests
- Node.js at the version in `web/.nvmrc` (22) — for frontend development and tests
- gitleaks at the version in `.gitleaks-version` (needed by the pre-commit hook)

## 1. Enable the repository hooks

```bash
git config core.hooksPath .githooks
```

The pre-commit hook runs `scripts/check-secrets.sh --staged` and gitleaks; it fails when gitleaks is
missing or not the version in `.gitleaks-version`.

## 2. Google OAuth client

In Google Cloud Console > APIs & Services > Credentials, create an OAuth client of type
"Web application" and register **both** authorized redirect URIs:

- `http://localhost:8080/signin-oidc` (the Docker Compose deployment)
- `http://localhost:5173/signin-oidc` (frontend development with `npm run dev`)

The app only requests the `openid email` scopes.

## 3. Configuration (`.env`)

```bash
cp .env.example .env
```

Fill in every value (`.env` is git-ignored; never commit it, and use only `@example.com` addresses in
any committed file):

| Variable | Purpose | How to set |
| --- | --- | --- |
| `ALLOWED_EMAILS` | Login allowlist, comma separated (trimmed, lower-cased) | your Google account e-mails |
| `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET` | Google OIDC | from step 2 |
| `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB` | Database account | password: `openssl rand -hex 24` |
| `MASTER_KEY` | Derives the keys that encrypt e-mails and Data Protection keys (Base64, 32 bytes) | `openssl rand -base64 32` |
| `ConnectionStrings__Default` | Database connection string | composed by `docker-compose.yml`; not in `.env` |
| `GIT_SHA` | Version shown by the health endpoints | exported by `deploy.sh` |
| `POSTGRES_IMAGE` | PostgreSQL image tag | exported by `deploy.sh` from `postgres-image.txt` |

Keep `MASTER_KEY` safe: if it is lost, the encrypted data cannot be read and the database has to be
reset (see below). Changing `MASTER_KEY` is not supported in the first version.

## 4. Deploy (Docker Compose)

Run **every** Compose operation through `deploy.sh` (the only exception is the reset below):

```bash
./deploy.sh
```

It exports `GIT_SHA` (with `-dirty` when there are uncommitted changes) and `POSTGRES_IMAGE`, then runs
`docker compose up -d --build`. It builds the current working tree and never runs `git pull`.
Running `docker compose` directly fails with `請用 deploy.sh 執行`.

- `db`: PostgreSQL (`postgres-image.txt`), data in the `vehiclego_db-data` volume, `pg_isready` health check.
- `api`: the ASP.NET Core app with the Vue build, published on <http://localhost:8080>. It starts after
  `db` is healthy, applies the database migrations at start-up, runs as a non-root user and restarts
  unless stopped. Health: `GET /health/live` (process) and `GET /health/ready` (database), both return
  `{ "status", "gitSha" }`.

After changing `.env` (for example removing someone from `ALLOWED_EMAILS`), run `./deploy.sh` again to
recreate the `api` container; the removed user's next request gets 401.

Logs (JSON, rotated by Docker; they are lost when the container is recreated, so check them before
deploying again):

```bash
docker logs -f vehiclego-api-1
```

**Connect only through `localhost`.** Opening the app over plain http on a LAN IP (for example
`http://192.168.x.x:8080`) does not work: the browser rejects the OIDC cookies on a non-localhost http
origin, so sign-in fails. HTTPS is planned for a later VPS deployment.

### Reset (empty the database)

```bash
POSTGRES_IMAGE="$(cat postgres-image.txt)" docker compose down -v
./deploy.sh
```

(`docker-compose.yml` requires `POSTGRES_IMAGE`, which `deploy.sh` normally exports, so the reset sets it
inline.) `down -v` deletes the database volume (all vehicles, users and Data Protection keys; everyone signs in
again). Use it when `MASTER_KEY` is lost or when applying migrations at start-up fails.

### Smoke test

```bash
bash scripts/smoke-test.sh          # ./deploy.sh, wait for healthy, check the endpoints
bash scripts/smoke-test.sh --down   # same, then docker compose down -v (deletes the database!)
```

It checks `/health/live`, `/health/ready`, the SPA (`/`, `/login`, a built asset), `/api/me` → 401
ProblemDetails, `/api/auth/login` → 302 to Google, and that the container does not run as root.
Fake Google credentials are enough for it.

## 5. Development

Backend:

```bash
dotnet tool restore                         # local tools (dotnet-ef)
dotnet build VehicleGo.slnx
```

Frontend with hot reload: start the backend with `./deploy.sh` (it serves the API on port 8080), then

```bash
cd web
npm ci
npm run dev                                 # http://localhost:5173
```

Vite proxies `/api` and `/signin-oidc` to `http://localhost:8080` (override with `VEHICLEGO_API_URL`)
and keeps the `localhost:5173` host, so Google sends you back to `http://localhost:5173/signin-oidc`.
Backend changes need `./deploy.sh` again.

## 6. Tests

```bash
dotnet test tests/VehicleGo.UnitTests          # rules engine, handlers, crypto, architecture
dotnet test tests/VehicleGo.IntegrationTests   # API + real PostgreSQL via Testcontainers (needs Docker)
cd web && npm run typecheck && npm run test:unit
```

The integration tests use the image in `postgres-image.txt`.

## 7. Database migrations

Migrations are applied automatically when `api` starts. To add one after changing the model, run once
per DbContext:

```bash
dotnet tool restore
dotnet ef migrations add <Name> --project src/VehicleGo.Infrastructure --startup-project src/VehicleGo.Infrastructure --context AppDbContext --output-dir Persistence/Migrations
dotnet ef migrations add <Name> --project src/VehicleGo.Infrastructure --startup-project src/VehicleGo.Infrastructure --context DataProtectionKeyContext --output-dir DataProtection/Migrations
```

The design-time factories use a fake connection string; generating a migration does not need a database.
Then run `./deploy.sh`. If the migration fails at start-up, reset the database (above) and deploy again.

## Secret checks

```bash
bash scripts/check-secrets.sh            # all tracked files
bash scripts/check-secrets.sh --staged   # staged files only (used by the hook)
```

CI runs `check-secrets.sh` and a pinned, checksum-verified gitleaks over the full history.
