# Fork Plan: Generic Collection Manager

## Decisione presa

Fork di TroveKeep in un nuovo progetto dedicato alla gestione di collezioni
generiche (libri, vinili, DVD, CD, ecc.), **Piano B**:

1. **Fase 1 (questo documento)**: rimuovere tutto ciò che è specifico Lego
   (oggetti, controller, servizi, repository, UI, archivio Rebrickable,
   table planner).
2. **Fase 2**: con la base "pulita" (storage, immagini, migrazioni, shell
   frontend), rivalutare l'approccio per il modello generico — verosimilmente
   un `CollectionItem` con schema dinamico per tipo (libro, vinile, ...) +
   tripla CRUD generata seguendo il pattern attuale di `LegoSet`/`BulkPiece`
   (che verrà rimosso, ma resta come riferimento nella history git di
   TroveKeep se serve).

Per la valutazione di costo completa (sessioni/token, opzioni A1/A2/B
confrontate), vedi la conversazione di origine in TroveKeep
(`alessandro.battini@gmail.com`, sessione del 2026-06-13).

## Cosa TENERE (base riusabile)

### Backend
- **Storage**: `Box.cs`, `Drawer.cs`, `DrawerContainer.cs`, `Room.cs`,
  `StorageAllocation.cs`, `StorageLocation.cs` + relativi
  controller/service/repository (`BoxesController`,
  `DrawerContainersController`, `DrawersController`, `RoomsController`)
  — il concetto di "contenitori fisici" è già agnostico rispetto al
  contenuto.
- **Immagini**: `Image.cs`, `ImageService`, pattern `ImageCached` (bool +
  endpoint `/image`) — generico per qualsiasi entità con foto.
- **Backup**: `IBackupService` / implementazione in Repositories.
- **Migrazioni**: framework `IMigration` + `MigrationRunner` +
  `meta.schema_version` (rimuovere solo `Migration_001_BaseplateTypeFields.cs`,
  specifica Lego).
- **Settings**: `SettingsController` / `ISettingsService` (se generico).
- **Documents** da tenere: `BoxDocument`, `DrawerContainerDocument`,
  `DrawerDocument`, `ImageDocument`, `RoomDocument`,
  `StorageAllocationDocument`.

### Frontend
- Shell: `AppNav.vue`, `BottomNav.vue`, `ConfirmDialog.vue`, `router/`,
  `client.js`.
- Views/api: `boxes/`, `drawercontainers/`, `drawers/`,
  `boxes.js`, `drawercontainers.js`, `drawers.js`, `settings.js`.
- `SearchView.vue` + `search.js` — da generalizzare in Fase 2 (oggi cerca
  solo LegoSet+BulkPiece).

## Cosa RIMUOVERE (specifico Lego)

### Backend — Models
- `LegoSet.cs`, `BulkPiece.cs`
- `Baseplate.cs`, `PlacedBaseplate.cs`, `PlacedTable.cs`,
  `AggregateBpLayout.cs`, `AggregateSelection.cs`, `TableTemplate.cs`
  (table planner)
- `RebrickableColor.cs`, `RebrickablePart.cs`,
  `RebrickablePartCategory.cs`, `RebrickablePartIventory.cs`,
  `RebrickableSet.cs`, `SetPhoto.cs` (archivio)
- `SearchResult.cs` — da riscrivere generico in Fase 2

### Backend — Interfaces/Services
- `ILegoSetService`, `IBulkPieceService`, `IBaseplateService`,
  `ITableTemplateService`, `IArchiveService`, `ISetPhotoService`
- `IRoomExportService` (export PDF planner — verificare se generalizzabile
  o da rimuovere)
- `ISearchService` — da riscrivere generico

### Backend — Controllers
- `SetsController`, `BulkPiecesController`, `BaseplatesController`,
  `TableTemplatesController`, `ArchivesController`
- `SearchController` — da riscrivere generico

### Backend — DTOs (Requests/Responses)
- Tutti quelli relativi a: LegoSet, BulkPiece, Baseplate, TableTemplate,
  Archive/Color/Part/PartCategory/SetArchive/SetPhoto

### Backend — Repositories/Documents
- `LegoSetDocument`, `BulkPieceDocument`, `BaseplateDocument`,
  `PlacedTableDocument`, `TableTemplateDocument`, `ArchiveMetaDocument`,
  `ColorDocument`, `PartArchiveDocument`, `PartCategoryDocument`,
  `PartInventoryArchiveDocument`, `SetArchiveDocument`, `SetPhotoDocument`

### Migrations
- `Migration_001_BaseplateTypeFields.cs`

### Frontend
- Views: `ArchivesView.vue`, `TablePlannerView.vue`, cartelle `bulkpieces/`,
  `sets/`, `tableplanner/`
- Api: `archives.js`, `bulkpieces.js`, `sets.js`, `tableplanner.js`
- Components: `ColorSelect.vue`, `PartArchiveTypeahead.vue`,
  `SetArchiveTypeahead.vue`

## Ordine consigliato di rimozione

1. Frontend: cancellare le view/api/componenti Lego-specifici, rimuovere
   relative voci da `router/` e `AppNav.vue`/`BottomNav.vue`.
2. Backend: rimuovere controller → DTO → service → repository/document →
   model, in quest'ordine, ricompilando dopo ogni livello per individuare
   subito i riferimenti rotti (es. `DrawerContainerService` che referenzia
   `IBulkPieceRepository` — vedi `_pieceRepo` in
   `src/TroveKeep.Services/DrawerContainerService.cs`, da disaccoppiare).
3. Rigenerare `SearchService`/`SearchController` come stub generico (o
   rimuovere temporaneamente, da ricostruire in Fase 2).
4. Verificare `IRoomExportService` (legato al planner — probabile rimozione).
5. Aggiornare `CLAUDE.md` del nuovo repo per riflettere l'architettura
   ridotta, prima di iniziare la Fase 2.
