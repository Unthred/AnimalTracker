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

### Container
From the repo root:

```bash
docker compose up -d --build
```

Persisted paths (from `docker-compose.yml`):
- `./data/Data` → `/app/Data` (SQLite DB)
- `./data/App_Data` → `/app/App_Data` (photos, backgrounds, data protection keys)

### Admin account
Set these environment variables (container) for automatic admin creation on startup:
- `AnimalTracker__AdminEmail`
- `AnimalTracker__AdminPassword`

Then sign in and visit `/admin/users`.

### Reverse proxy
Create `animaltracker.yeradonkey.com` in HAProxy and forward to the host port you map (default `8085`).
Ensure HAProxy forwards:
- `X-Forwarded-Proto: https`
- `X-Forwarded-For`


