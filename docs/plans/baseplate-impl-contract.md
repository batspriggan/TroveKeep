# Contratto di implementazione — sezione Baseplates a tab

Questo file è la **fonte di verità dei contratti** per i task paralleli. I riferimenti
di piano (non vincolanti per il codice, ma per le decisioni) sono
`docs/plans/baseplate-tab-plan.md` e `docs/plans/baseplate-addendum.md`.

## Regole generali

- .NET 10, `Nullable=enable`, `ImplicitUsings=enable`. **MongoDB, non SQL**: niente join,
  niente collection per le riserve (embedded), niente foreign key.
- BSON Guid = `Standard` (`[BsonGuidRepresentation(GuidRepresentation.Standard)]` su ogni Guid).
- Pattern del progetto: ogni entità ha controller/service/repository `Scoped`.
  `InvalidOperationException → 400`, `KeyNotFoundException → 404`.
- `[BsonIgnoreExtraElements]` sui documenti.
- Non toccare: `Room.Layout`, `PlacedTable`, il calcolo `bpNaturalW/bpNaturalH` in
  `AggregateBpPlannerView.vue`, `RoomExportService` esistente (solo estensione se serve).
- Non eseguire migrazioni contro un DB reale. Nessun `dotnet run`.
- Verifica obbligatoria: `dotnet build` (backend) e `npm run build` (frontend) devono passare.

## A. Modello dominio (`TroveKeep.Core`)

```csharp
public enum RoadShape { Straight, Curve, Junction, TJunction, Crossroad }

public class BaseplateReservation
{
    public Guid SetId { get; set; }
    public int Quantity { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class Baseplate
{
    // ... campi esistenti INVARIATI ...
    public RoadShape? RoadShape { get; set; }          // solo Type == Road
    public int Quantity { get; set; } = 1;             // giacenza per tipo+colore
    public List<BaseplateReservation> Reservations { get; set; } = [];
    public string? Notes { get; set; }
    public bool NeedsReview { get; set; }
}

public class BaseplateModuleGrid                       // NUOVO
{
    public Guid? BaseplateId { get; set; }
    public int Cols { get; set; }
    public int Rows { get; set; }
    public bool IsOverridden { get; set; }
}
```

`AggregateBpLayout` (in `Room`) guadagna:
```csharp
public BaseplateModuleGrid? ModuleGrid { get; set; }
```
`AggregateBpLayoutDocument` guadagna lo stesso campo (documento embedded `ModuleGridDocument`).

## B. Documento Mongo (`BaseplateDocument`)

```csharp
public RoadShape? RoadShape { get; set; }
public int Quantity { get; set; } = 1;
public List<BaseplateReservationDocument> Reservations { get; set; } = [];
public string? Notes { get; set; }
public bool NeedsReview { get; set; }
```

```csharp
public class BaseplateReservationDocument
{
    [BsonGuidRepresentation(GuidRepresentation.Standard)] public Guid SetId { get; set; }
    public int Quantity { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

`RoomDocument`: `List<AggregateBpLayoutDocument>` guadagna `ModuleGridDocument? ModuleGrid`,
con:
```csharp
public class ModuleGridDocument
{
    [BsonGuidRepresentation(GuidRepresentation.Standard)] public Guid? BaseplateId { get; set; }
    public int Cols { get; set; }
    public int Rows { get; set; }
    public bool IsOverridden { get; set; }
}
```

## C. Interfacce backend (firma ESATTA — non cambiare)

```csharp
public interface IBaseplateRepository
{
    // esistenti
    Task<IEnumerable<Baseplate>> GetAllAsync();
    Task<Baseplate?> GetByIdAsync(Guid id);
    Task<Baseplate> CreateAsync(Baseplate baseplate);
    Task UpdateImageCachedAsync(Guid id);
    Task DeleteAsync(Guid id);
    Task DeleteByLinkedSetIdAsync(Guid setId);
    // nuovi
    Task<Baseplate?> UpdateAsync(Baseplate baseplate);                 // null se non trovato
    Task<Baseplate?> AddOrIncrementReservationAsync(Guid baseplateId, Guid setId, int quantity);
    Task<bool> RemoveReservationAsync(Guid baseplateId, Guid setId);
    Task RemoveReservationsBySetIdAsync(Guid setId);
}

public interface IBaseplateService
{
    // esistenti
    Task<IEnumerable<Baseplate>> GetAllAsync();
    Task<Baseplate?> GetByIdAsync(Guid id);
    Task<Baseplate> CreateAsync(Baseplate baseplate);
    Task UpdateImageCachedAsync(Guid id);
    Task DeleteAsync(Guid id);
    (int studX, int studY) GuessStudDimensions(string partDescription);
    // nuovi
    Task<Baseplate> UpdateAsync(Guid id, Baseplate updated);           // preserva CreatedAt/ImageCached/LinkedSetId
    Task<Baseplate> AddReservationAsync(Guid id, Guid setId, int quantity);
    Task<Baseplate> RemoveReservationAsync(Guid id, Guid setId);
    Task RemoveReservationsBySetIdAsync(Guid setId);
    bool ComputeNeedsReview(Baseplate bp, bool isImported);
}
```

Regole `ComputeNeedsReview` (addendum §A):
- Standard: `WidthStuds > 0 && DepthStuds > 0 && LegoColorId > 0 && Quantity >= 1`
- Road: come Standard ma `LegoColorId` non richiesto. **`RoadShape` NON entra nei criteri**: è metadato opzionale, non un requisito di completezza.
- Custom: `WidthStuds > 0 && DepthStuds > 0 && Quantity >= 1 && !string.IsNullOrWhiteSpace(Name)`
- `isImported == true` (part-search o dedup migrazione) → sempre `true` alla creazione,
  finché l'utente non salva un update con dati completi.

## D. DTO / API (firma ESATTA)

```
GET    /api/baseplates                      -> BaseplateResponse[]
POST   /api/baseplates                      body CreateBaseplateRequest -> 201 BaseplateResponse
PUT    /api/baseplates/{id:guid}            body UpdateBaseplateRequest -> 200 BaseplateResponse
POST   /api/baseplates/{id:guid}/confirm                            -> 200 BaseplateResponse
POST   /api/baseplates/{id:guid}/reservations  body { setId, quantity } -> 200 BaseplateResponse
DELETE /api/baseplates/{id:guid}/reservations/{setId:guid}          -> 200 BaseplateResponse
POST   /api/baseplates/feasibility           body { aggregates:[{roomId,representativeId}] } -> FeasibilityResponse
GET    /api/baseplates/{id:guid}/image       (invariato)
POST   /api/baseplates/{id:guid}/image       (invariato)
DELETE /api/baseplates/{id:guid}             (invariato)
```

`BaseplateResponse` diventa (ordine campi ESATTO, i worker frontend si basano su questo):

```csharp
public record BaseplateResponse(
    Guid Id, string Type, string PartNum, string Name,
    int WidthStuds, int DepthStuds,
    int LegoColorId, string? LegoColorName, string? LegoColorRgb,
    bool ImageCached, Guid? LinkedSetId,
    string? RoadShape,          // "Straight"|"Curve"|"Junction"|"TJunction"|"Crossroad"|null
    int Quantity,
    int ReservedQuantity,
    int AvailableQuantity,
    int InLayoutQuantity,
    bool NeedsReview,
    string? Notes,
    IEnumerable<ReservationResponse> Reservations,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int Version);

public record ReservationResponse(Guid SetId, string? SetDescription, int Quantity);

public record CreateBaseplateRequest(
    string Type, string PartNum, string Name,
    int WidthStuds, int DepthStuds, int LegoColorId,
    Guid? LinkedSetId, string? RoadShape, int Quantity = 1, string? Notes = null);

public record UpdateBaseplateRequest(
    string Type, string PartNum, string Name,
    int WidthStuds, int DepthStuds, int LegoColorId,
    Guid? LinkedSetId, string? RoadShape, int? Quantity, string? Notes);

public record CreateReservationRequest(Guid SetId, int Quantity);

public record FeasibilityRequest(IEnumerable<FeasibilityAggregate> Aggregates);
public record FeasibilityAggregate(Guid RoomId, string RepresentativeId);

public record FeasibilityLine(
    Guid BaseplateId, string Name, string Type,
    int Need, int Quantity, int Reserved, int Available, int Deficit, string Status); // "ok" | "short"
public record FeasibilityResponse(IEnumerable<FeasibilityLine> Lines, int TotalDeficit, bool HasShortage);
```

`ReservedQuantity = Σ Reservations.Quantity`
`AvailableQuantity = Quantity - ReservedQuantity` (mai sotto 0)
`SetDescription` risolta in-memory dal controller/service (dal repository dei set).

### Algoritmo `feasibility`
Per ogni aggregato selezionato:
1. trova la `Room`, calcola gli aggregati con la stessa logica BFS già nelle view
   (`areAdjacent`/`computeAggregates`), individua il gruppo con `repId` = `representativeId`;
2. fabbisogno = `PlacedBaseplates` dell'`AggregateBpLayout` corrispondente, raggruppato per `BaseplateId`;
3. se `PlacedBaseplates` è vuoto e `ModuleGrid` esiste:
   `need[ModuleGrid.BaseplateId] += Cols * Rows` (se `BaseplateId` null → usa la base risolta da
   `AggregateSelection.BpKey`, formato `"<w>x<d>"`, cercando nel catalogo una base con
   `WidthStuds`/`DepthStuds` corrispondenti);
4. somma su tutti gli aggregati; confronta con `Quantity - ReservedQuantity`.
   `Deficit = max(0, Need - (Quantity - ReservedQuantity))`.
5. le basi referenziate ma non più presenti in catalogo si saltano.

## E. Regole di business (service)

- `AddReservation`: se `SetId` già presente → **incrementa** la voce (non duplica).
  Se `ReservedQuantity + quantity > Quantity` → `InvalidOperationException` (→400).
  `quantity` deve essere `>= 1`.
- `UpdateAsync`: preserva `CreatedAt`, `ImageCached`, `LinkedSetId` se non passati; incrementa `Version`; ricalcola `NeedsReview`.
- `RemoveReservationsBySetIdAsync` va chiamata quando un set viene cancellato
  (`LegoSetService.DeleteAsync` già chiama `DeleteByLinkedSetIdAsync`: aggiungere la chiamata).
- Il campo `Reservations` è sempre presente (array, eventualmente vuoto).

## F. Migrazione

`src/TroveKeep.Migrations/Migration_005_BaseplateQuantity.cs` (`IMigration`, `VersionFrom=4`, `VersionTo=5`).
Registrare in `MigrationRunner._migrations`. `"baseplates"` e `"rooms"` sono già in `BackupCollections`.

Passi (tutti idempotenti, backup automatico già gestito dal runner):
1. backfill `Quantity=1`, `RoadShape=null`, `Reservations=[]`, `Notes=null`, `NeedsReview=true`
   dove assenti;
2. **dedup** (D1): raggruppa per chiave di business
   `Type|PartNum|LegoColorId|WidthStuds|DepthStuds`, somma `Quantity`, merge `Reservations`
   per `SetId`, tieni un id canonico (es. il più vecchio per `CreatedAt`), rimappa
   `rooms[].AggregateBpLayouts[].PlacedBaseplates[].BaseplateId` dai duplicati al canonico,
   elimina i duplicati;
3. `rooms` non richiede backfill (`ModuleGrid` assente = derivato). Non toccare i documenti room.

## G. Frontend

### Nuovi file
- `ui/src/api/baseplates.js` — client della nuova API (firma sotto).
- `ui/src/views/baseplates/BaseplateListView.vue` — library.
- `ui/src/views/baseplates/BaseplateForm.vue` — form add/edit (se utile).
- `ui/src/views/tableplanner/BuildCheckView.vue` — tab Build Check.
- `ui/src/views/tableplanner/BaseplatePlannerListView.vue` — *opzionale*: la tab
  "Baseplate Planner" resta `AggregatePlannerListView.vue`, non rinominare.

### API frontend (firma ESATTA)
```js
export const getAllBaseplates = () => get('/api/baseplates')
export const createBaseplate = (body) => post('/api/baseplates', body)
export const updateBaseplate = (id, body) => put(`/api/baseplates/${id}`, body)
export const confirmBaseplate = (id) => post(`/api/baseplates/${id}/confirm`, {})
export const deleteBaseplate = (id) => del(`/api/baseplates/${id}`)
export const getBaseplateImageUrl = (id) => `/api/baseplates/${id}/image`
export const uploadBaseplateImage = (id, file) => /* FormData POST */
export const addReservation = (id, setId, quantity) => post(`/api/baseplates/${id}/reservations`, { setId, quantity })
export const removeReservation = (id, setId) => del(`/api/baseplates/${id}/reservations/${setId}`)
export const checkFeasibility = (aggregates) => post('/api/baseplates/feasibility', { aggregates })
```

### Route (contratto ESATTO)
- `/baseplates` → `BaseplateListView` (**fuori** dal guard `tablePlannerEnabled`).
- `/table-planner/build-check` → `BuildCheckView` (`meta: { }`, dentro il guard `tablePlannerEnabled`).
- Restano: `/table-planner`, `/table-planner/rooms/:id`, `/table-planner/baseplates`,
  `/table-planner/baseplates/:roomId/:repId`.

### Nav
- `AppNav.vue`: `<RouterLink to="/baseplates">Baseplates</RouterLink>`, **sempre visibile**.
- `BottomNav.vue`: voce analoga con icona.
- `TablePlannerView.vue` e `AggregatePlannerListView.vue`: tab-bar a
  `Rooms | Baseplate Planner | Build Check`.
- `SettingsView.vue`: aggiornare il testo del toggle Table Planner (non menziona più le basi).

### Pulizia
- `ArchivesView.vue`: rimuovere sezione Baseplates (UI ~259–353) e relativo script
  (`baseplates`, `newBp*`, `bpPendingFile`, `addBaseplate`, `removeBaseplate`,
  `onBpFileChange`, `saveBpImage`, onMounted ~845–847). **Table Templates restano.**
- `ui/src/api/tableplanner.js`: rimuovere le funzioni baseplate spostate; **non** toccare template/rooms.

## H. Ordine di integrazione

- Task 1 (backend) e Task 2 (migrazione) sono sequenziali tra loro (la migrazione usa i
  documenti aggiornati). Il **Task 2 parte solo dopo il Task 1**.
- Task 3 (frontend library + nav) può partire in parallelo al Task 1: usa il **contratto D**
  come verità. Non deve aspettare il backend compilato.
- Task 4 (planner: module grid + build check) dipende dai contratti A/B/C/D, non dal Task 3.

## I. Definition of Done per ogni task

- `dotnet build` (se tocca `.cs`) e/o `npm run build` (se tocca `.vue`/`.js`) passano.
- Nessun file di piano modificato (i piani sono congelati).
- Nessun test scritto (il progetto non ne ha).
- Il worker scrive `.pi/tasks/<name>/result.md` con: esito, file toccati, scostamenti dal
  contratto, dubbi aperti.
