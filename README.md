# TroveKeep

> ⚠️ **Upgrading to v4.0.0 — read this first.**
> Baseplates have been **moved out of Archives** into their own top-level section, and the
> baseplate data model has changed: rows now carry a **quantity** per type/colour, MOC
> reservations, road shape and a review flag. The startup migration (`Migration_005`)
> backfills those fields, **merges duplicate baseplate documents** (same type + part +
> colour + stud dimensions) and **dissolves MOC-linked "baseplate" rows** into reservations,
> re-pointing the room layouts at real plate types.
> **This is destructive:** only the oldest document of each duplicate group is kept, and its
> metadata (`ImageCached`, `LinkedSetId`, `Notes`) wins — metadata that existed only on the
> discarded duplicates is lost. Quantities are summed and MOC reservations are merged, so no
> inventory count is lost. Placements of MOC rows in room layouts are **preserved** (each MOC
> block becomes the real plates it is built on); anything that cannot be converted
> automatically is left in a **quarantine** state to reconcile by hand, and planning is
> blocked until it is resolved. Treat the merge as irreversible.
>
> You are protected by the **automatic pre-migration backup**: the runner writes a full
> gzip-JSON snapshot of every collection to `Migration__BackupDir`
> (`auto-backup-v{currentVersion}-{timestamp}.json.gz`) **before** any pending migration runs,
> and refuses to start if that backup cannot be written. Keep that snapshot — see
> [Rollback](#rollback) for how to restore it. Back up your production database before
> upgrading.
>
> **Post-upgrade clean-up is expected — the migration cannot know your real inventory.**
> Every baseplate row is backfilled with **`Quantity = 1`**, then corrected as follows:
> - **Set the real owned quantities by hand.** A type you own 100 of will still say `1` until
>   you edit it in the **Baseplates** library. Rows flagged **⚠ To review** are the ones the
>   migration could not verify (imported from the part archive, merged, or dissolved from a MOC);
>   review and confirm them.
> - **Reservations may exceed the owned quantity.** Dissolving a MOC reserves the plates it is
>   built on (`Fattoria` = `4× 32×32`), so a type with `Quantity = 1` shows a red
>   **⚠ Over-reserved** badge. This is a warning, not an error: fix the owned quantity in the
>   library and both the badge and the planner numbers settle. Availability is clamped at 0
>   meanwhile.
> - **Fix the “odd” cases by hand.** Anything the migration could not convert automatically
>   (no matching plate module, linked set missing, ambiguous geometry) is left in a
>   **quarantine** state and listed under **Unresolved MOC plates** in the Baseplates library.
>   Each row offers three actions: pick the real plate type and grid (`Reconcile`), declare it a
>   physical plate after all (`Not a MOC`), or delete it. **The planner refuses to open while any
>   quarantined row exists** — reconcile them and it unlocks automatically.
> - **Mixed-size MOC footprints are approximate.** The layout rectangle is derived from the
>   reserved plates (compact square arrangement, e.g. `4 plates → 2×2`). A MOC mixing plate
>   sizes has no single correct rectangle: the UI labels it *mixed sizes* and you reconcile the
>   layout yourself. Existing placements are always preserved either way.

A self-hosted inventory manager for Lego collections. Track sets and bulk pieces, organise them across boxes and drawer units, import Rebrickable colour and set data, and back up / restore your collection as a single JSON file.

## Scope, audience and security

This project is built for **personal use on a trusted local network** (LAN/VPN). It was designed primarily as a **single-user** application — one person managing their own collection — but nothing prevents a few people on the same network from sharing one instance.

There is **no authentication and no authorisation** of any kind:

- every API endpoint is open to anyone who can reach the server;
- there are no user accounts, sessions, or permissions;
- the UI never prompts for credentials.

Because of this, **do not expose TroveKeep to the public internet** (no port-forwarding, no public reverse proxy, no cloud hosting without putting your own authentication layer and TLS in front of it). Anyone who can reach the port can read, modify, and delete the entire inventory. Treat it like a household appliance on the home network, not like a multi-tenant web service.

## Features

- **Sets** — catalogue Lego sets with set number, description, photo URL, and quantity; download and cache box-art images from Rebrickable
- **Bulk pieces** — catalogue loose parts by Lego part ID, colour (resolved from the Rebrickable colour archive), and quantity
- **Storage** — assign sets and pieces to boxes or individual drawers; one item can span multiple storage locations
- **Search** — full-text search across sets and bulk pieces, with client-side filtering in list views
- **QR scanner** — scan the QR codes printed on storage labels with the device camera to jump straight to a box, drawer, or piece
- **Label printing** — generate QR labels for boxes, drawer containers, drawers, and bulk pieces (single or as a `.zip` batch) and hand them to a `label-tool` watch folder for printing; parts can carry a per-colour image next to the QR
- **Archives** — import the Rebrickable colours, sets, parts, and part-categories CSV archives for colour resolution, set typeahead, and part search
- **Table Planner** — drag-and-drop room layout editor; define table templates, place them on a canvas with snap-to-grid and edge magnetism, and calculate how many LEGO baseplates cover a selected table group
- **Baseplates** — a top-level library for the baseplates that underpin a Lego city layout: type (Standard / Road / Custom), stud dimensions, colour, **quantity owned per type and colour**, road shape, image, and **MOC reservations** (a MOC or set can hold N baseplates of a type, removing them from the freely usable pool). Rows imported from the Rebrickable part archive, merged by a migration, or awaiting reconciliation are flagged with a warning badge in the navigation
- **Build Check** — inside the Table Planner, select any set of table aggregates (configurations) and check the **combined baseplate requirement** against what you own, per baseplate type; layouts deliberately do **not** consume availability, since the same plates can serve different layouts on different occasions
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
| `docker-compose.image.yml` | Pull the pre-built image from `ghcr.io` |
| `docker-compose.build.yml` | Build the image locally from source |
| `podman-compose.image.yml` | Pull the pre-built image (Podman) |
| `podman-compose.build.yml` | Build locally (Podman) |

Both variants include an optional MongoDB service. If you already have a MongoDB instance running, remove the `mongo` service block and update `MongoDb__ConnectionString` accordingly.

### Using the pre-built image (recommended)

Pre-built images are published to the GitHub Container Registry. The package is public, so no login is required:

```bash
docker pull ghcr.io/batspriggan/trovekeep:latest
```

Or start it directly with the provided compose file:

```bash
# Docker
docker compose -f deploy/docker-compose.image.yml up -d

# Podman
podman-compose -f deploy/podman-compose.image.yml up -d
```

The app is available at `http://localhost:8080`.

#### Available tags

| Tag | Meaning |
| --- | --- |
| `latest` | Most recent stable release |
| `X.Y.Z` | A specific release (e.g. `4.0.0`) |
| `X.Y` | Latest patch of a minor line (e.g. `4.0`) |
| `dev` | Latest pre-release (version tags containing a hyphen, e.g. `4.0.0-beta1`) |

Images are built and pushed automatically by GitHub Actions whenever a `v*` tag is pushed (see `.github/workflows/docker.yml`).

### Building from source

```bash
# Docker
docker compose -f deploy/docker-compose.build.yml up -d --build

# Podman
podman-compose -f deploy/podman-compose.build.yml up -d --build
```

### Self-hosted registry (optional)

If you prefer not to pull from `ghcr.io` (e.g. an isolated LAN), `.forgejo/workflows/build.yml` builds the image and pushes it to a **local Forgejo container registry** instead. This workflow is **manual only** — it never runs on tag push. Trigger it from the Forgejo UI (Actions → the workflow → Run workflow) and provide the version tag to build.

It expects the following Forgejo configuration under *Settings → Actions*:

| Kind | Name | Example |
| --- | --- | --- |
| Variable | `REGISTRY_HOST` | `registry.example.com:5000` |
| Variable | `REGISTRY_USER` | `alex` |
| Variable | `IMAGE_NAME` | `alex/trovekeep` |
| Secret | `REGISTRY_PASSWORD` | *(registry password/token)* |

### Environment variables

| Variable | Default | Description |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` | ASP.NET Core environment |
| `MongoDb__ConnectionString` | `mongodb://admin:password@mongo:27017` | MongoDB connection string |
| `MongoDb__DatabaseName` | `trovekeep` | MongoDB database name |
| `Migration__BackupDir` | *(empty)* | Directory (host-visible) where an automatic full backup is written before any pending migration runs. **Required** when there are pending migrations — if unset or the backup fails, startup aborts and no migration runs. Must align with the backup volume mount (e.g. `./data:/data` + `Migration__BackupDir=/data/migrations`). |
| `LabelTool__PublicBaseUrl` | *(empty)* | Public base URL of the API (no trailing slash) used to build the absolute image URL embedded in a label (label-tool downloads the image from this URL). Labels fall back to QR-only when this is unset. |

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

## Versioning

TroveKeep uses a single version line: `vMAJOR.MINOR.PATCH` (semantic versioning).

**Pushing a `v*` tag is what triggers a release build.** On tag push, GitHub Actions builds the image and publishes it to `ghcr.io` (see `.github/workflows/docker.yml`).

```bash
git tag v4.0.0
git push github v4.0.0
```

Pre-releases are expressed with a hyphen (`v4.0.0-beta1`): they are published under the `dev` tag instead of `latest`. The public registry no longer receives `lv*` tags — that prefix belonged to the local build line and is retired.

## Project structure

```text
src/
├── TroveKeep.Core/          Domain models and interfaces
├── TroveKeep.Services/      Business logic
├── TroveKeep.Repositories/  MongoDB implementations
├── TroveKeep.Migrations/    Ordered startup migrations
└── TroveKeep.Api/           ASP.NET Core controllers and DTOs
ui/
└── src/
    ├── api/                 Fetch wrappers per entity
    ├── components/          Shared Vue components
    ├── composables/         Shared reactive state (e.g. settings)
    ├── router/              Vue Router configuration
    ├── utils/               PDF/print helpers
    └── views/               Page-level Vue components (baseplates/, tableplanner/, …)
src/archives/                Rebrickable CSV archives (not committed)
```

## Development

TroveKeep was developed entirely with the help of AI coding assistants: development started using **Claude**, and later continued using **DeepSeek**. Every change in this repository — backend, frontend, infrastructure and documentation — has been produced through that AI-assisted workflow, with human review and direction at each step.

## License

TroveKeep is free software released under the [GNU General Public License v3.0](LICENSE.txt).
You are free to use, modify, and distribute it under the terms of that licence.
