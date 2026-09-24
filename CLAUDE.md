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
- **`Baseplate`** — business key `Type + PartNum + LegoColorId + WidthStuds + DepthStuds`; identical plates are **one document with `Quantity > 1`**, never duplicate rows. `Type` (`Standard|Road|Custom`) drives which fields are required. `RoadShape` (only for Road) is `Straight|Curve|Junction|TJunction|Crossroad`. `NeedsReview` is **computed** (`ComputeNeedsReview`): imported rows are always flagged; otherwise it flags missing data (dimensions, colour for Standard, name for Custom, `Quantity < 1`). `RoadShape` is **optional metadata**, never part of the review criteria — requiring it re-flagged legacy road rows on every edit (even a quantity bump).
- **MOC reservations are embedded in the baseplate document** (`Reservations: [{ SetId, Quantity, CreatedAt }]`) — no separate collection, no joins. Duplicate `SetId` merges (increments). An over-reservation (`ReservedQuantity > Quantity`) is **allowed and is only a warning** (`overReserved`), never a blocking error: the user reconciles the owned quantity from the baseplate library. `ReservedQuantity`/`AvailableQuantity` are computed, never stored (`AvailableQuantity` is clamped at 0); `InLayoutQuantity` counts placements across all rooms and is informational only — **layouts never consume availability** (the same plates can serve alternative layouts). Reservations are pruned when the linked set is deleted.
- **`AggregateBpLayout.ModuleGrid`** (embedded in `Room`) — optional `{ BaseplateId?, Cols, Rows, IsOverridden }` describing an aggregate as a grid of baseplates (e.g. "2×2 plates"). Absent = derived from the table bounding box; `Cols×Rows ≠ PlacedBaseplates.Count` is a **soft warning**, the fine-grained placement is never overwritten.
- **Feasibility / Build Check** — `IPlanningService` recomputes the aggregate BFS (same algorithm and 0.5 cm tolerance as `RoomPlannerView`/`AggregatePlannerListView`) and compares the combined need of the selected aggregates with `Quantity − ReservedQuantity`. Nothing is persisted: selection is volatile.
- **Baseplate rows are never auto-deduplicated at runtime** — only `Migration_005` merges existing duplicates (summing quantity, merging reservations, re-pointing `PlacedBaseplates.BaseplateId`).

## Migrations (from v1.0.0)

Breaking schema changes require a migration in `src/TroveKeep.Migrations/` (e.g. `Migration_001_Description.cs`) implementing `IMigration` (`VersionFrom`, `VersionTo`, `RunAsync(IMongoDatabase)`). Schema version tracked in `meta` collection key `"schema_version"`. Migrations run on startup in order, each preceded by an automatic full backup in `Migration__BackupDir` (fail-fast: no backup → no migration). Never suggest dropping the database.

Current chain: `001` baseplate type fields · `002` set images by set id · `003` part images by colour · `004` label targets index · **`005` baseplate quantity** (v4 → v5): backfills `Quantity=1`, `RoadShape=null`, `Reservations=[]`, `Notes=null`, `NeedsReview=true`, then **deduplicates** baseplates by business key (keeps the oldest id, sums quantity, merges reservations by `SetId`, re-points `rooms[].AggregateBpLayouts[].PlacedBaseplates[].BaseplateId` to the survivor, flags it `NeedsReview`), and finally **dissolves MOC-linked rows** (`Type == Custom && LinkedSetId != null`): derives the real module, creates reservations, converts each MOC placement into the real plates it is built on (carrying `SourceSetId`), and deletes the fake row — or flags it `Quarantined` when it cannot be converted. Idempotent and fail-soft; `ModuleGrid` needs no backfill (absent = derived).

## Frontend routes

- `/baseplates` — baseplate library (`views/baseplates/BaseplateListView.vue`). **Top-level nav entry, always visible** — *not* gated by `tablePlannerEnabled`.
- `/table-planner` — rooms · `/table-planner/rooms/:id` — room canvas · `/table-planner/baseplates` — aggregate list · `/table-planner/baseplates/:roomId/:repId` — baseplate placement canvas · `/table-planner/build-check` — multi-configuration feasibility. All `/table-planner/*` are gated by `tablePlannerEnabled`.
- Baseplate API client: `ui/src/api/baseplates.js` (not `tableplanner.js`).
