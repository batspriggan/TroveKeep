# TroveKeep

A self-hosted inventory manager for Lego collections. Track sets and bulk pieces, organise them across boxes and drawer units, import Rebrickable colour and set data, and back up / restore your collection as a single JSON file.

## Features

- **Sets** — catalogue Lego sets with set number, description, photo URL, and quantity; download and cache box-art images from Rebrickable
- **Bulk pieces** — catalogue loose parts by Lego part ID, colour (resolved from the Rebrickable colour archive), and quantity
- **Storage** — assign sets and pieces to boxes or individual drawers; one item can span multiple storage locations
- **Search** — full-text search across sets and bulk pieces
- **Archives** — import the Rebrickable colours, sets, parts, and part-categories CSV archives for colour resolution, set typeahead, and part search
- **Table Planner** — drag-and-drop room layout editor; define table templates, place them on a canvas with snap-to-grid and edge magnetism, and calculate how many LEGO baseplates cover a selected table group
- **Baseplate Library** — manage LEGO baseplate parts (linked to the Rebrickable parts archive) with their stud dimensions; used by the plate calculator in the room planner
- **Backup / Restore** — export the full inventory to a JSON file and restore it on any instance; individual rooms can also be exported and imported as ZIP files

## Tech stack

| Layer | Technology |
| --- | --- |
| Backend | .NET 10, ASP.NET Core, MongoDB |
| Frontend | Vue 3, Vite, Vue Router 4 |
| Database | MongoDB |

## Running locally

### Prerequisites

- .NET 10 SDK
- Node.js 20+
- A running MongoDB instance (default connection configured in `src/TroveKeep.Api/appsettings.json`)

### Backend

```bash
cd src/TroveKeep.Api
dotnet run
# API available at http://localhost:5221
# OpenAPI spec at http://localhost:5221/openapi/v1.json
```

### Frontend

```bash
cd ui
npm install   # first time only
npm run dev   # Vite dev server at http://localhost:5173
```

The Vite dev server proxies all `/api` requests to the backend automatically.

### Production build

```bash
cd ui
npm run build   # output in ui/dist/
```

Serve `ui/dist/` as static files alongside the API, or configure ASP.NET Core to serve it directly.

## Deployment

Compose files for Docker and Podman are provided in the `deploy/` directory. Two variants exist for each runtime:

| File | Description |
| --- | --- |
| `docker-compose.image.yml` | Pull the pre-built image from the registry |
| `docker-compose.build.yml` | Build the image locally from source |
| `podman-compose.image.yml` | Pull the pre-built image (Podman) |
| `podman-compose.build.yml` | Build locally (Podman) |

Both variants include an optional MongoDB service. If you already have a MongoDB instance running, remove the `mongo` service block and update `MongoDb__ConnectionString` accordingly.

Note : the prebuild images is on the way, I am still setting up the pipeline.

### Using the pre-built image (recommended)

```bash
# Docker
docker compose -f deploy/docker-compose.image.yml up -d

# Podman
podman-compose -f deploy/podman-compose.image.yml up -d
```

The app is available at `http://localhost:8080`.

### Building from source

```bash
# Docker
docker compose -f deploy/docker-compose.build.yml up -d --build

# Podman
podman-compose -f deploy/podman-compose.build.yml up -d --build
```

### Environment variables

| Variable | Default | Description |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` | ASP.NET Core environment |
| `MongoDb__ConnectionString` | `mongodb://admin:password@mongo:27017` | MongoDB connection string |
| `MongoDb__DatabaseName` | `trovekeep` | MongoDB database name |
| `Migration__BackupDir` | *(empty)* | Directory (host-visible) where an automatic full backup is written before any pending migration runs. **Required** when there are pending migrations — if unset or the backup fails, startup aborts and no migration runs. Must align with the backup volume mount (e.g. `./data:/data` + `Migration__BackupDir=/data/migrations`). |
| `LabelTool__PublicBaseUrl` | *(empty)* | Public base URL of the API (no trailing slash) used to build the absolute image URL embedded in a label (label-tool downloads the image from this URL, v1.0.0+). Labels fall back to QR-only when this is unset. |

> **Note:** Change the default MongoDB credentials before exposing the instance to a network.

## Migrations & pre-migration backup

Schema changes are applied as ordered **migrations** in `src/TroveKeep.Migrations/`. The schema version is tracked in the `meta` collection (`schema_version`) and migrations run **on startup**, in order. Never drop the database.

**Automatic backup (fail-fast):** before any pending migration, the runner writes a **full gzip-JSON snapshot** of every collection to `Migration__BackupDir` (filename `auto-backup-v{currentVersion}-{timestamp}.json.gz`). This safety net lets you roll back a destructive migration (e.g. re-keying) to the exact pre-migration state.

Fail-fast guarantees:
- If `Migration__BackupDir` is **not configured** or the backup write **fails** → startup **aborts** and **no migration runs** (the database is untouched).
- If a **migration fails** → startup stops immediately: that migration is **not** marked as applied (`schema_version` unchanged) and no subsequent migration runs.

**Deploying with pending migrations:**

```yaml
services:
  api:
    volumes:
      - ./data:/data          # makes the backups visible/hosted on disk
    environment:
      - Migration__BackupDir=/data/migrations
```

On startup the runner writes `auto-backup-v{currentVersion}-*.json.gz` into `./data/migrations/` (host), then applies the pending migration(s).

## Rollback

A backup written by the migration runner is stored as **gzip-compressed MongoDB extended JSON** and is **not** restored automatically. Each top-level key is a **collection name** and holds an array of documents (with `_id`); MongoDB-specific types are serialized in extended-JSON form (e.g. GUID → `{"$binary": {"base64": ..., "subType": "04"}}`, string keys such as `set_images._id` → plain string).

To roll back a collection to its pre-migration state from the host shell:

```bash
# 1. Locate the snapshot you want (pre-migration).
ls -la ./data/migrations/                    # e.g. auto-backup-v1-2026-08-25_15-54-30.json.gz

# 2. Unpack it on the host (or in the container) to a plain .json.
gunzip -c auto-backup-v1-2026-08-25_15-54-30.json.gz > backup.json

# 3. Copy it into the API container and restore the collection(s) you need with mongosh.
docker cp backup.json trovekeep-api-1:/tmp/backup.json

docker exec -i trovekeep-mongo-1 mongosh -u admin -p password --authenticationDatabase admin trovekeep \
  --eval '
    const fs = require("fs");
    const data = EJSON.parse(fs.readFileSync("/tmp/backup.json", "utf8"));
    // Restore per collection, e.g. re-keyed set_images back to their original _id:
    for (const doc of data.set_images) {
      const id = doc._id;                 // original pre-migration _id (e.g. set number)
      db.getCollection("set_images").replaceOne({ _id: id }, doc, { upsert: true });
    }
  '
```

`EJSON.parse` converts the extended-JSON values (GUID `$binary`, etc.) into real BSON on import, so the documents can be written back as-is keyed by their original `_id`.

> 💡 Always keep the **pre-migration** snapshot — it is your exact rollback point. Stop trusting the app (or run on a stopped instance) while restoring, and double-check `meta.schema_version` afterwards so the runner does not re-apply migrations on the next startup after a full rollback.

## Project structure

```text
src/
├── TroveKeep.Core/          Domain models and interfaces
├── TroveKeep.Services/      Business logic
├── TroveKeep.Repositories/  MongoDB implementations
└── TroveKeep.Api/           ASP.NET Core controllers and DTOs
ui/
└── src/
    ├── api/                 Fetch wrappers per entity
    ├── components/          Shared Vue components
    ├── router/              Vue Router configuration
    └── views/               Page-level Vue components
src/archives/                Rebrickable CSV archives (not committed)
```

## License

TroveKeep is free software released under the [GNU General Public License v3.0](LICENSE.txt).
You are free to use, modify, and distribute it under the terms of that licence.
