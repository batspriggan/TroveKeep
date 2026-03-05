# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

### Backend (.NET 10)
```bash
# Build entire solution
dotnet build

# Run the API
cd src/TroveKeep.Api && dotnet run
# API listens on http://localhost:5221 (or https://localhost:7120)
# OpenAPI spec available at /openapi/v1.json in Development mode
```

### Frontend (Vue 3 + Vite)
```bash
cd ui/
npm install       # first time only
npm run dev       # Vite dev server on http://localhost:5173 (proxies /api → localhost:5221)
npm run build     # production build → ui/dist/
npm run preview   # preview production build
```

No test projects exist in this repository.

## Architecture

**Layered .NET backend + Vue 3 SPA frontend.**

### Backend Layer Dependencies
```
TroveKeep.Api → TroveKeep.Services → TroveKeep.Core
TroveKeep.Api → TroveKeep.Repositories → TroveKeep.Core
```
- **Core** — domain models and repository/service interfaces; no external dependencies
- **Repositories** — MongoDB implementations; depends only on `MongoDB.Driver`
- **Services** — business logic; depends only on Core interfaces
- **Api** — ASP.NET Core controllers, DTOs, DI wiring; `Microsoft.AspNetCore.OpenApi` is the only non-project dependency

All projects target `net10.0` with `Nullable=enable` and `ImplicitUsings=enable`.

### MongoDB
Connection configured in `appsettings.json` under key `MongoDb` (`ConnectionString`, `DatabaseName`). The client and database are registered as singletons. BSON Guid representation is set to Standard in `Program.cs`.

### Domain Model Relationships
- `LegoSet` and `BulkPiece` are the primary inventory entities; each embeds `List<StorageAllocation>`.
- `StorageAllocation` = `{ StorageId (Guid), Type (StorageType: Box|Drawer), Quantity (int) }`.
- `Box`, `DrawerContainer`, and `Drawer` are storage entities. Drawers belong to a DrawerContainer.
- LegoSets can only be stored in Boxes; BulkPieces can be stored in Boxes or Drawers.

### Key Business Rules (enforced in Services)
- Sum of `StorageAllocation.Quantity` across all allocations ≤ item's `Quantity`; violation throws `InvalidOperationException` → 400.
- Allocating to an already-used location increments the existing entry (merge semantics).
- `UpdateAsync` preserves existing `StorageAllocations` — never overwritten by a model update.
- Business keys: `SetNumber` for LegoSet; `LegoId + LegoColor` for BulkPiece.

## Database Versioning & Migrations (from v1.0.0)

The project was tagged at **v1.0.0**. From this point forward:

- Any breaking change to the MongoDB schema (renamed fields, removed fields, changed types, restructured documents, new required indexes) **must** be accompanied by a migration.
- Migrations live in `src/TroveKeep.Migrations/` as numbered scripts (e.g., `Migration_001_Description.cs`) implementing a common `IMigration` interface with `VersionFrom`, `VersionTo`, and `RunAsync(IMongoDatabase)`.
- The current database schema version is tracked in the `meta` collection under key `"schema_version"`.
- On startup, `Program.cs` runs pending migrations in order before the app accepts requests.
- Never tell the user to "drop the database and restart" as a migration strategy — always write a real migration.

### Controller → Service → Repository Pattern
Each domain entity has a matching controller, service, and repository triple, all registered as `Scoped` in `Program.cs`. Controllers catch `InvalidOperationException` and return 400, `KeyNotFoundException` and return 404.

### Frontend API Module Pattern
Each entity has a corresponding module in `ui/src/api/` (e.g., `sets.js`, `boxes.js`) that wraps `client.js` fetch helpers. Views use Vue Router 4 with no global state store (no Pinia).
