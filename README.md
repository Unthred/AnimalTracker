# AnimalTracker

Blazor Web App (server-side interactivity) for tracking animal sightings.

## Prereqs
- .NET SDK 10
- Node.js (for Tailwind build)

## Run (dev)
In one terminal:

```powershell
cd C:\src\AnimalTracker
npm run watch:css
```

In another terminal:

```powershell
cd C:\src\AnimalTracker
dotnet run --project .\src\AnimalTracker\AnimalTracker.csproj
```

## Deploy (UnRaid + OPNsense HAProxy)

### Build on UnRaid (no Docker required on this PC)
1. Copy or clone this repo onto UnRaid (for example under `/mnt/user/appdata/animaltracker-src`).
2. In the repo root, create your environment file:

```bash
cp .env.example .env
```

3. Edit `.env` and set:
- `APP_PORT` (default `8085`)
- `DATA_ROOT` (default `./data`, or absolute `/mnt/user/appdata/animaltracker`)
- optional logging levels:
  - `LOG_LEVEL_DEFAULT`
  - `LOG_LEVEL_ASPNETCORE`
  - `LOG_LEVEL_EF_SQL` (set `Information` temporarily to see SQL)
  - `LOG_LEVEL_EF_MIGRATIONS`
- optional first-run admin bootstrap:
  - `ANIMALTRACKER_ADMIN_EMAIL`
  - `ANIMALTRACKER_ADMIN_PASSWORD`

4. Start the container from repo root:

```bash
docker compose up -d --build
```

Persisted paths (from `docker-compose.yml`):
- `${DATA_ROOT}/Data` → `/app/Data` (SQLite DB)
- `${DATA_ROOT}/App_Data` → `/app/App_Data` (photos, backgrounds, data protection keys)

### Admin account
- If `ANIMALTRACKER_ADMIN_EMAIL` and `ANIMALTRACKER_ADMIN_PASSWORD` are set in `.env`, the admin user is created/ensured on startup.
- If left blank, register the first account in the UI; it becomes admin automatically.
- Admin dashboard route: `/admin`.

### Reverse proxy
Create `animaltracker.yeradonkey.com` in HAProxy and forward to the host port from `.env` (`APP_PORT`, default `8085`).
Ensure HAProxy forwards:
- `X-Forwarded-Proto: https`
- `X-Forwarded-For`


