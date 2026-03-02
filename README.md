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

> **Note:** Change the default MongoDB credentials before exposing the instance to a network.

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
