# CLAUDE.md

## Commands

**Backend:** `dotnet build` · `cd src/TroveKeep.Api && dotnet run` (http://localhost:5221, OpenAPI at `/openapi/v1.json`)

**Frontend:** `cd ui && npm run dev` (http://localhost:5173, proxies /api → 5221) · `npm run build` · `npm run preview` · `npm test` (dependency-free `node:test` unit tests in `ui/tests/`: the planner marquee, selection and rigid-group-drag maths, plus the sticker-sheet packing, the four artwork fits, the per-item artwork orientation and the PDF renderer)

**Tests:** `dotnet test` (xUnit, `tests/TroveKeep.Tests` — the collection-scope write rules, the global-write filter and the sticker image/project business rules) · `cd ui && npm test`.

## Conventions

- **Commit messages and code comments are in English.** Releases are tagged on `master` as
  `vMAJOR.MINOR.PATCH` (minor for a feature, patch for a fix) and pushed to both remotes,
  `origin` (Forgejo) and `github`.

## Architecture

Layered .NET 10 backend + Vue 3/Vite SPA. All projects: `net10.0`, `Nullable=enable`, `ImplicitUsings=enable`.

```
Api → Services → Core
Api → Repositories → Core
Tests → Api, Services, Core
```

- **Core** — domain models + interfaces; no external deps
- **Repositories** — MongoDB (`MongoDB.Driver`); implements Core interfaces
- **Services** — business logic; depends only on Core interfaces
- **Api** — controllers, DTOs, DI wiring (`Microsoft.AspNetCore.OpenApi`)
- **Tests** — xUnit (`tests/TroveKeep.Tests`); pure-logic tests with hand-written fakes, no database and no HTTP server

**MongoDB:** `appsettings.json` → `MongoDb.ConnectionString/DatabaseName`; client+db as singletons; BSON Guid = Standard.

**Pattern:** Each entity has a controller/service/repository triple, all `Scoped`. Controllers: `InvalidOperationException` → 400, `KeyNotFoundException` → 404.

**Frontend:** `ui/src/api/<entity>.js` wraps `client.js`. Vue Router 4, no Pinia.

**Collection access (multi-user):** every domain row carries a `CollectionId`; `ICollectionScope`
is the request-scoped answer to "what may I read/write here". Reads are scoped by the repository
filter (`ScopedTo` for listings, `ScopedToAccessible` for fetch-by-id) and admins bypass it.
Writes are authorized by membership **level**: `Viewer` is read-only, `Editor`/`Owner` may write,
an admin may write everywhere. It is enforced in one place — the global `CollectionWriteGuardFilter`
(`IAsyncActionFilter`) answers **403** for every non-GET request whose target collection the caller
cannot write; `ICollectionWriteTargetResolver` derives that collection from the route id (loaded
through the scoped repository), else from `DefaultCollectionId` (creates), else the endpoint is a
declared exclusion (Auth, Setup, Mode, Users, Settings, Archives, Labels, Scanner, Search,
StickerImages, StickerProjects) — plus `CollectionsController`, which checks `CanManageMembers` per action (admin or
Owner, never an Editor). Levels come from `collection_members` via `ICollectionMembershipCache`,
primed once per request by `CollectionMembershipMiddleware` — not from a cookie claim, so a demotion
takes effect immediately. The SPA mirrors this (`useActiveCollection().canWrite`,
`canManageMembers`) to hide controls, but the UI is not the boundary.

**Instance-wide data has no target collection**, so it cannot be authorized per collection:
`ICollectionScope.CanWriteAnywhere` (admin, or Editor/Owner of **at least one** collection) is the
rule, and `CollectionWriteAnywhereFilter` answers **403** for the non-GET requests,
`[ServiceFilter(typeof(CollectionWriteAnywhereFilter))]` per action. Today only the sticker library
uses it (`StickerImagesController`, `StickerProjectsController`), which is also why both are in
`ExcludedControllers` of the resolver: the rows are global, the dedicated filter authorizes them.
A Viewer (or a user with no collection) still reads and prints stickers.

## Domain

- Primary entities: `LegoSet`, `BulkPiece` — storage tracked in `storage_allocations` collection (separate, not embedded).
- **Sticker feature — three levels, all global**: `StickerImage` (`sticker_images`), `StickerProject`
  (`sticker_projects`) and `StickerItem` (**embedded** in the project, no own collection). Neither an
  image nor a project carries a `CollectionId`, and neither uses an `ICollectionScope` filter.
  - `StickerImage` = `{ Name (trimmed, 1..80), ImageCached, CreatedAt, UpdatedAt, Version }`: the
    artwork shelf, **no stud fields**. Bytes live in `set_images` as
    `ImageReferenceType.StickerImage` keyed by the image id, deleted with the row (best effort).
  - `StickerProject` = `{ Name (trimmed, 1..80), Items, CreatedAt, UpdatedAt, Version }`; the item
    list is replaced wholesale on update (supplied item `Id`s are kept, new ones get a fresh Guid)
    and may be empty — a project is created first, items are added later.
  - `StickerItem` = `{ Id, ImageId, WidthStuds, DepthStuds (1..32 each), Copies (1..999), Fit,
    Orientation, Notes? }`. One stud is 8 mm (`StickerItem.StudMm`), so a 2×4 tile is a 16×32 mm
    label; the response DTOs send `WidthMm`/`DepthMm` so the SPA never repeats the constant.
  - `StickerFit` enum `{ Contain (default), Cover, Fill, Center }` is serialised as its **name** and is
    **orthogonal to rotation**: the footprint is always in studs, `Fit` only says how the artwork
    fills the label graphic area (the sheet engine decides the page rotation).
  - `StickerOrientation` enum `{ Auto (default), Landscape, Portrait }` is the **per-item artwork
    orientation**, also serialised as its name (unknown/missing → `Auto`; no migration, the missing
    field deserialises to the default). It turns the **artwork inside the label**, never the label:
    a 2×4 tile stays 16 × 32 mm. `Auto` means "as uploaded", not "never turned". It is **orthogonal
    to the sheet option `allowRotate`** (the packer turning a whole label by 90° when that is the
    only way it fits the page): the two rotations act on different things and compose. Ordering is
    fixed — **rotate first, then fit** — so `Contain` on a landscape image placed `Portrait`
    letterboxes on the correct axis and `Cover` crops the already-turned image.
  - Rules (`StickerProjectService`, `InvalidOperationException` → 400): an item's `ImageId` must
    exist, and a second item with the same `ImageId + WidthStuds + DepthStuds` in one project is
    refused (two rows are indistinguishable to the user). Deleting a `StickerImage` used by project
    items is refused with 400 naming the usage count (`IStickerProjectRepository.CountUsagesAsync`).
  - Endpoints: `/api/sticker-images` (CRUD + `{id}/image` GET/POST/DELETE) and `/api/sticker-projects`
    (CRUD). Writes need `CanWriteAnywhere` (admin, or Editor/Owner anywhere), reads are open to every
    regular account (`User,Admin`).
  - **No migration**: this redesign landed on an unpublished branch, so the old one-level `stickers`
    collection and `ImageReferenceType.Sticker` were removed outright and old sticker data is
    discarded on purpose.
- `StorageAllocation`: `{ StorageId, Type (Box|Drawer), Quantity }`
- Storage: `Box`, `DrawerContainer`, `Drawer` (embedded in container, keyed by position — no own Id).
- LegoSets → Boxes only; BulkPieces → Boxes or Drawers.
- Allocation quantity sum ≤ item `Quantity`; duplicate location merges (increments).
- `UpdateAsync` preserves existing allocations and fields like `ImageCached`, `CreatedAt`, `Layout`.
- Business keys: `SetNumber` (LegoSet); `LegoId + LegoColorId` (BulkPiece).
- `SetNumber` optional for MOCs (stored as empty string); `IsMoc: bool` on LegoSet.
- `ImageCached: bool` flag — clients fetch `/api/{entity}/{id}/image` only when true.
- **`Baseplate`** — business key `Type + PartNum + LegoColorId + WidthStuds + DepthStuds`; identical plates are **one document with `Quantity > 1`**, never duplicate rows. `Type` (`Standard|Road`) drives which fields are required. **`Custom` is legacy and not creatable**: it meant "a MOC stored as a fake baseplate row", replaced by reservations on real plates. The enum value survives only so `Migration_005` and the quarantine wizard can still recognise and convert such rows on an unmigrated installation; `ParseType` rejects it on create/update. `RoadShape` (only for Road) is `Straight|Curve|Junction|TJunction|Crossroad`. `NeedsReview` is **computed** (`ComputeNeedsReview`): imported rows are always flagged; otherwise it flags missing data (dimensions, colour for Standard, `Quantity < 1`). `RoadShape` is **optional metadata**, never part of the review criteria — requiring it re-flagged legacy road rows on every edit (even a quantity bump).
- **MOC reservations are embedded in the baseplate document** (`Reservations: [{ SetId, Quantity, CreatedAt }]`) — no separate collection, no joins. Duplicate `SetId` merges (increments). An over-reservation (`ReservedQuantity > Quantity`) is **allowed and is only a warning** (`overReserved`), never a blocking error: the user reconciles the owned quantity from the baseplate library. `ReservedQuantity`/`AvailableQuantity` are computed, never stored (`AvailableQuantity` is clamped at 0); `InLayoutQuantity` counts placements across all rooms and is informational only — **layouts never consume availability** (the same plates can serve alternative layouts). Reservations are pruned when the linked set is deleted.
- **`AggregateBpLayout.ModuleGrid`** (embedded in `Room`) — optional `{ BaseplateId?, Cols, Rows, IsOverridden }` describing an aggregate as a grid of baseplates (e.g. "2×2 plates"). Absent = derived from the table bounding box; `Cols×Rows ≠ PlacedBaseplates.Count` is a **soft warning**, the fine-grained placement is never overwritten.
- **Feasibility / Build Check** — `IPlanningService` recomputes the aggregate BFS (same algorithm and 0.5 cm tolerance as `RoomPlannerView`/`AggregatePlannerListView`) and compares the combined need of the selected aggregates with `Quantity − ReservedQuantity`. Nothing is persisted: selection is volatile. **Archived rooms are excluded** (both here and in the aggregate list).
- **`Room.Obsolete`** — archives a room: hidden from the room list and excluded from planning maths. Not a version: a replacement is made with **Duplicate** (copies dimensions, layout, aggregate selections and baseplate layouts; placement ids are regenerated, reservations untouched). No migration needed — the missing field deserializes to `false`.
- **Room resize** — editable in the planner, but floored at the layout's bounding box (`IRoomService.ComputeMinimumAsync`, returned as `minWidthCm`/`minDepthCm` so the UI blocks a smaller input). `UpdateAsync` enforces the same rule server-side (`InvalidOperationException` → 400). **Fit to tables** shifts the layout to the origin and resizes the room to the exact footprint (no margin).
- **Baseplate rows are never auto-deduplicated at runtime** — only `Migration_005` merges existing duplicates (summing quantity, merging reservations, re-pointing `PlacedBaseplates.BaseplateId`).

## Migrations (from v1.0.0)

Breaking schema changes require a migration in `src/TroveKeep.Migrations/` (e.g. `Migration_001_Description.cs`) implementing `IMigration` (`VersionFrom`, `VersionTo`, `RunAsync(IMongoDatabase)`). Schema version tracked in `meta` collection key `"schema_version"`. Migrations run on startup in order, each preceded by an automatic full backup in `Migration__BackupDir` (fail-fast: no backup → no migration). Never suggest dropping the database.

Current chain: `001` baseplate type fields · `002` set images by set id · `003` part images by colour · `004` label targets index · **`005` baseplate quantity** (v4 → v5): backfills `Quantity=1`, `RoadShape=null`, `Reservations=[]`, `Notes=null`, `NeedsReview=true`, then **deduplicates** baseplates by business key (keeps the oldest id, sums quantity, merges reservations by `SetId`, re-points `rooms[].AggregateBpLayouts[].PlacedBaseplates[].BaseplateId` to the survivor, flags it `NeedsReview`), and finally **dissolves MOC-linked rows** (`Type == Custom && LinkedSetId != null`): derives the real module, creates reservations, converts each MOC placement into the real plates it is built on (carrying `SourceSetId`), and deletes the fake row — or flags it `Quarantined` when it cannot be converted. Idempotent and fail-soft; `ModuleGrid` needs no backfill (absent = derived).

## Frontend routes

- `/baseplates` — baseplate library (`views/baseplates/BaseplateListView.vue`). **Top-level nav entry, always visible** — *not* gated by `tablePlannerEnabled`.
- `/table-planner` — rooms · `/table-planner/rooms/:id` — room canvas · `/table-planner/baseplates` — aggregate list · `/table-planner/baseplates/:roomId/:repId` — baseplate placement canvas · `/table-planner/build-check` — multi-configuration feasibility. All `/table-planner/*` are gated by `tablePlannerEnabled`.
- Baseplate API client: `ui/src/api/baseplates.js` (not `tableplanner.js`).
- `/stickers` — global sticker **image library** (`views/stickers/StickerLibraryView.vue`): thumbnail
  grid, inline rename, delete, multi-file upload (name from the filename), replace image. No studs,
  no copies, no print controls. `/sticker-projects` — project list
  (`views/stickers/StickerProjectListView.vue`): name, item count, total labels, created/updated,
  create (name only), delete. `/sticker-projects/:id` — the project editor
  (`views/stickers/StickerProjectView.vue`): rename, add an item (artwork picker
  `components/stickers/StickerImagePicker.vue`, footprint presets 1×1..2×8 or custom studs, copies,
  fit, artwork orientation, notes), item list with in-place copies/fit/orientation/notes, remove, and
  the A4 print panel (`components/stickers/StickerPrintPanel.vue`). All three are hidden to a Guest
  (who only reaches `/table-planner`) and read-only to a Viewer: mutating controls need
  `canWriteAnywhere`. API clients `ui/src/api/stickerImages.js` and `ui/src/api/stickerProjects.js`.
  The editor persists the **whole item list** with one `PUT` (debounced, item ids included, so rows
  survive), shows a save state and handles a 409 by reloading; the server's 400 for a duplicate
  `image + footprint` and for deleting a still-used image is surfaced verbatim.
  The sheet is composed **client-side at 1:1** (`ui/src/utils/stickerSheets.js` = pure packing and
  geometry in mm, `stickerPdf.js` = jsPDF drawing, `stickerImages.js` = browser canvas resampling at
  600 dpi; `composables/useStickerSheet.js` = layout options in `localStorage` only — the copies are a
  persisted field of the project item, not volatile state). Layout options: sheet margin, cut
  border (stroke inset by half so the outer edge is the cut line), **inner border** (artwork inset =
  border + padding), gap between labels (`max(gapMm, 2 × borderMm)`), a size trim, and an optional
  90° rotation used only as a fallback. A label larger than the printable area is a
  `StickerFitError` naming it, never a silently dropped copy.
  **Packing** (`planStickerSheet`) is a **2D bottom-left skyline**, not shelves: the free edge of the
  page is a list of `{ x, w, y }` plateaus starting as one segment over the content width, and each
  copy (in the deterministic order `sortUnits`: height desc, width desc, name, id, copy) goes to the
  segment start that keeps the skyline lowest, then leftmost, with `y + h <= bottom`; the height is
  read over the **whole foot print including the gap** (`[x, x + w + step)`), so a label dropped just
  left of an already placed neighbour goes above it instead of breaking the cut gap (and the
  skyline stays monotone). The placed
  label reserves `[x, x + w + step) × [y, y + h + step)`, so `step` stays the distance between cut
  lines and a short label's leftover space is reused by the next one; equal-height segments are
  merged. Rotation is tried only after the upright variant has no candidate, a page that cannot take
  the label is closed and a fresh skyline retried, and only a label that fits no empty page is unfit.
  The plan exposes `pages: [{ index, cells }]` (no `rows`/shelves any more) plus the flat `cells`,
  `count`, `perSticker`, `content`, `step`, `options`; `stickerPdf.js` draws `page.cells`.
  Deterministic: two runs on the same input produce an identical plan.
  **Fits** (`Contain` default / `Cover` / `Fill` / `Center`) are baked into the raster, because
  `doc.addImage` cannot crop: `computeFitGeometry` returns the source and destination rectangles and
  the loader draws them into a canvas whose pixel size comes from the plan's `graphic` rectangle
  (`stickerGraphicMm`, shared with the packer), keyed per **item + orientation** so jsPDF still
  aliases repeated copies. The item's `Orientation` is resolved **before** the fit
  (`resolveEffectiveSource` swaps the source axes when it disagrees with the raster's natural one)
  and the raster applies the 90° turn with a canvas transform around the label rectangle; the
  packer's label turn is then `rotateStickerRaster` on the finished upright raster, so the two
  compose. The loader's canvas factory and artwork loader are injectable, so the four fits, the two
  artwork orientations and the rotated case are unit-tested headless (`tests/stickerFits.test.mjs`).
- **Multi-selection in both planners** (`RoomPlannerView`, `AggregateBpPlannerView`) is **volatile**: it
  lives in a `Set` in component state and is never serialised into `layout`/`placedBaseplates`.
  Ctrl/Cmd+click toggles, Shift+click adds, dragging on empty canvas draws a marquee
  (`ui/src/utils/marquee.js` + `ui/src/utils/selection.js` — canvas-px rectangles, strict
  intersection, 3 px threshold, `planRigidDrag` = one snapped/clamped delta for the group), Esc or a
  click on empty canvas clears. Selection granularity is the whole unit: a table selects its entire
  aggregate, a plate selects its entire MOC/set instance. **Only movement is multi** — a rigid drag
  with one uniform snap/clamp delta, overlaps checked against the non-selected units; rotation and
  removal stay single-unit (their buttons/keys are hidden or inactive for a multi-selection).
