# CLAUDE.md

## Commands

**Backend:** `dotnet build` · `cd src/TroveKeep.Api && dotnet run` (http://localhost:5221, OpenAPI at `/openapi/v1.json`)

**Frontend:** `cd ui && npm run dev` (http://localhost:5173, proxies /api → 5221) · `npm run build` · `npm run preview`

No test projects.

## Architecture

Layered .NET 10 backend + Vue 3/Vite SPA. All projects: `net10.0`, `Nullable=enable`, `ImplicitUsings=enable`.

```
Api → Services → Core
Api → Repositories → Core
```

- **Core** — domain models + interfaces; no external deps
- **Repositories** — MongoDB (`MongoDB.Driver`); implements Core interfaces
- **Services** — business logic; depends only on Core interfaces
- **Api** — controllers, DTOs, DI wiring (`Microsoft.AspNetCore.OpenApi`)

**MongoDB:** `appsettings.json` → `MongoDb.ConnectionString/DatabaseName`; client+db as singletons; BSON Guid = Standard.

**Pattern:** Each entity has a controller/service/repository triple, all `Scoped`. Controllers: `InvalidOperationException` → 400, `KeyNotFoundException` → 404.

**Frontend:** `ui/src/api/<entity>.js` wraps `client.js`. Vue Router 4, no Pinia.

## Domain

- Primary entities: `LegoSet`, `BulkPiece` — storage tracked in `storage_allocations` collection (separate, not embedded).
- `StorageAllocation`: `{ StorageId, Type (Box|Drawer), Quantity }`
- Storage: `Box`, `DrawerContainer`, `Drawer` (embedded in container, keyed by position — no own Id).
- LegoSets → Boxes only; BulkPieces → Boxes or Drawers.
- Allocation quantity sum ≤ item `Quantity`; duplicate location merges (increments).
- `UpdateAsync` preserves existing allocations and fields like `ImageCached`, `CreatedAt`, `Layout`.
- Business keys: `SetNumber` (LegoSet); `LegoId + LegoColorId` (BulkPiece).
- `SetNumber` optional for MOCs (stored as empty string); `IsMoc: bool` on LegoSet.
- `ImageCached: bool` flag — clients fetch `/api/{entity}/{id}/image` only when true.

## Migrations (from v1.0.0)

Breaking schema changes require a migration in `src/TroveKeep.Migrations/` (e.g. `Migration_001_Description.cs`) implementing `IMigration` (`VersionFrom`, `VersionTo`, `RunAsync(IMongoDatabase)`). Schema version tracked in `meta` collection key `"schema_version"`. Migrations run on startup in order. Never suggest dropping the database.
