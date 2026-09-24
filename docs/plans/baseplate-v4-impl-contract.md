# Contratto di implementazione — modello v4 "occupazione piastre" (MOC/set)

Fonte di verità dei contratti per i task paralleli. Il piano è in
`docs/plans/baseplate-v4-model.md` (decisioni A/B/C chiuse).

## Regole generali

- .NET 10, `Nullable=enable`, `ImplicitUsings=enable`. **MongoDB**: riserve embedded, nessuna
  collection nuova, nessuna join. BSON Guid = `Standard`.
- `[BsonIgnoreExtraElements]`; `InvalidOperationException → 400`, `KeyNotFoundException → 404`.
- Non toccare `bpNaturalW/bpNaturalH`, `RoomExportService`, `Room.Layout`/`PlacedTable`.
- Verifica: `dotnet build` e `npm run build` devono passare.
- Nessun `dotnet run`, nessuna migrazione su DB reale.

## A. Schema (additivo)

```csharp
// Core/Models/Baseplate.cs
public bool Quarantined { get; set; }
public string? QuarantineReason { get; set; }

// Core/Models/PlacedBaseplate.cs
public Guid? SourceSetId { get; set; }   // provenienza MOC/set; null per piastre singole
```

`BaseplateDocument` e `PlacedBaseplateDocument` rispecchiano gli stessi campi.
`PlacedBaseplateDocument.BaseplateId` e `SourceSetId` con `[BsonGuidRepresentation(Standard)]`.

## B. Servizio di riconciliazione (CRITICO, condiviso migrazione/UI)

Nuovo `Core/Interfaces/Services/IMocReconciliationService.cs`:

```csharp
public interface IMocReconciliationService
{
    // Trova il tipo di piastra reale che divide esattamente le dimensioni date.
    // Criterio: non-MOC (Type != Custom || LinkedSetId == null), WidthStuds>0, DepthStuds>0,
    // width % bp.WidthStuds == 0 && depth % bp.DepthStuds == 0.
    // Preferenza: stessa LegoColorId, poi Type Standard, poi dimensione maggiore.
    Task<Baseplate?> FindModuleAsync(int widthStuds, int depthStuds, int legoColorId, Guid excludeId);

    // Dissolve una riga MOC: crea riserve sul modulo, riscrive i piazzamenti, elimina la riga.
    Task<MocDissolveResult> DissolveAsync(Guid mocBaseplateId, Guid targetBaseplateId, int cols, int rows);

    // Marca una riga come non convertibile.
    Task QuarantineAsync(Guid baseplateId, string reason);
}

public record MocDissolveResult(bool Dissolved, int PlatesCreated, int PlacementsRewritten, string? Reason);
```

### B.1 `FindModuleAsync`
Confronta le dimensioni della riga MOC con i tipi reali. Gestisce anche l'orientamento
(`width % bp.WidthStuds == 0 && depth % bp.DepthStuds == 0` **oppure** invertito).

### B.2 `DissolveAsync(mocId, targetId, cols, rows)`
1. Carica la riga MOC. Se assente → `Dissolved=false, Reason="not found"`.
2. **Riserve**: per il target, `AddOrIncrementReservationAsync(targetId, moc.LinkedSetId, cols*rows)`
   (se `LinkedSetId` null → quarantena, `Reason="linked set not found"`).
   **Un'over-reservation è ammessa e non è un errore**: se `cols*rows > target.Quantity`
   la reservation viene comunque scritta e la riga target risulta `overReserved` (warning in
   library). La migrazione **non** mette in quarantena per quantità insufficiente: quello è un
   avviso, non un caso non risolvibile. La riconciliazione della quantità posseduta si fa dalla
   sezione Baseplates.
3. **Piazzamenti**: per **ogni** room, per ogni `AggregateBpLayouts[]`, sostituisci ogni
   `PlacedBaseplate` con `BaseplateId == mocId` con `cols*rows` entry di `targetId`:
   - coordinate: partendo da `XMm`,`YMm` della entry originale, griglia in coordinate locali
     (riga `iy`, colonna `ix`), offset `ix*targetW`, `iy*targetH` dove `targetW/H` sono le
     dimensioni in mm del target **nella stessa convenzione del canvas**:
     `w = target.WidthStuds*8` se `rotation % 180 == 0` altrimenti `DepthStuds*8`
     (usare i mm nominali pieni, non la tolleranza, per una griglia pulita).
   - `Rotation` = quella della entry originale; `SourceSetId` = `moc.LinkedSetId`;
     `InstanceId` = nuovo Guid (deterministico opzionale).
   - salva il documento room (read-modify-write del solo documento interessato).
4. **Delete** della riga MOC dal catalogo (`baseplates`) e dell'immagine associata
   (`IImageService.DeleteAsync(mocId, ImageReferenceType.Baseplate)` — best-effort, non fallire
   se assente).
5. Ritorna i conteggi.

### B.3 Quarantena
`QuarantineAsync` imposta `Quarantined=true`, `QuarantineReason=reason`, `NeedsReview=true`.
Mai cancellare. La riga **resta** nel catalogo ma non è piazzabile.

## C. `Migration_005` estesa (v4 → v5, UNICA)

Ordine interno obbligatorio:
1. **Backfill** (già fatto): `Quantity=1`, `RoadShape=null`, `Reservations=[]`, `Notes=null`,
   `NeedsReview=true`, **`Quarantined=false`**, **`QuarantineReason=null`**
   (dove assenti, con `Exists(field,false)`).
2. **Dedup** (già fatto): per chiave `Type|PartNum|LegoColorId|WidthStuds|DepthStuds`.
3. **Dissoluzione MOC** (NUOVO): per ogni riga con `Type == Custom && LinkedSetId != null`:
   - carica tutti i tipi reali (non-MOC);
   - `FindModuleAsync` → se null: `QuarantineAsync(id, "no matching module")` e continua;
   - se il set esiste? (verifica `legosets` per `_id == LinkedSetId`): se assente →
     `QuarantineAsync(id, "linked set not found")` e continua;
   - `cols = moc.WidthStuds / target.WidthStuds`, `rows = moc.DepthStuds / target.DepthStuds`
     (o invertiti se l'orientamento è scambiato);
   - `DissolveAsync(...)`; se `!Dissolved` → `QuarantineAsync(id, reason)`.
   - **Fail-soft per riga**: qualunque eccezione su una riga → try/catch, quarantena con motivo,
     si prosegue. Mai abortire l'intera migrazione.
   - Accumula gli id non risolti in `meta` con key `migration_005_unresolved` (array di
     `{ baseplateId, reason }`), **upsert**.
   - Verifica finale: query di controllo che nessun `PlacedBaseplate` punti a una riga cancellata;
     se ce n'è, logga (non abortire).

Il servizio di riconciliazione va istanziato dentro la migrazione: `Migration_005` non ha DI.
Costruttore: costruire i repository/services necessari da `IMongoDatabase` a mano
(`new BaseplateRepository(db)`, ecc.) oppure rendere il servizio costruibile da `IMongoDatabase`.
Scegli l'opzione più semplice e **documentala**.

## D. API

```
GET    /api/baseplates                        -> BaseplateResponse[]  (+ quarantined, quarantineReason)
POST   /api/baseplates/{id}/reconcile         body ReconcileRequest -> DissolveResponse
POST   /api/baseplates/{id}/unquarantine      body UnquarantineRequest -> BaseplateResponse
GET    /api/baseplates/by-set/{setId}         -> SetReservationResponse[]
GET    /api/baseplates/planner-entities       -> PlannerEntityResponse[]
DELETE /api/baseplates/{id}                   (esistente; ora deve anche rimuovere i PlacedBaseplate che la referenziano)
```

```csharp
public record ReconcileRequest(Guid TargetBaseplateId, int? Cols, int? Rows);
public record DissolveResponse(bool Dissolved, int PlatesCreated, int PlacementsRewritten, string? Reason);
public record UnquarantineRequest(bool ConfirmAsPhysicalPlate);
public record SetReservationResponse(Guid BaseplateId, string PlateName, string Type,
    int WidthStuds, int DepthStuds, int LegoColorId, string? LegoColorName, string? LegoColorRgb,
    int Quantity, string? SourceSetId /*null*/, DateTimeOffset CreatedAt);
public record PlannerEntityResponse(Guid SetId, string Name, bool IsMoc,
    Guid ModuleBaseplateId, int ModuleWidthStuds, int ModuleDepthStuds,
    int TotalPlates, int Cols, int Rows, int FootprintWidthStuds, int FootprintDepthStuds,
    bool Quarantined);
```

`BaseplateResponse` guadagna in coda `bool quarantined, string? quarantineReason`.
**Non cambiare l'ordine esistente** degli altri campi (il frontend già li consuma).

### D.1 `planner-entities`
Set/MOC **con** almeno una riserva. Per ogni `SetId` distinto nelle `Reservations`:
- `TotalPlates` = somma quantità;
- `ModuleBaseplateId` = il tipo più riservato (o il primo);
- `Cols`/`Rows` = arrangiamento compatto: `cols = ceil(sqrt(TotalPlates))`, `rows = ceil(TotalPlates/cols)`
  (per 4 → 2×2);
- footprint = `cols*ModuleWidthStuds` × `rows*ModuleDepthStuds`;
- `Name`/`IsMoc` dal documento set (`LegoSet`), `Quarantined` = il set ha piastre in quarantena?
  (fase 1: `false`).
Composizione in memoria; due query (baseplates, sets).

### D.2 `by-set/{setId}`
Tutte le `Reservations` con quel `SetId`, denormalizzate con i dati della piastra.

### D.3 `DELETE` e riferimenti
`DeleteAsync` deve rimuovere anche i `PlacedBaseplate` che puntano all'id in ogni room
(bonifica). Vale per la cancellazione normale e per "confirm as physical plate" (§E).

## E. Riconciliazione UI (Flusso)

Wizard in library, per una riga `Quarantined`:
1. l'utente sceglie il **tipo reale** target (typeahead sul catalogo) e `cols×rows` (precompilati
   dalla derivazione, se possibile) → `POST /reconcile` → la riga sparisce, le riserve e i
   piazzamenti sono aggiornati.
2. **"Non è una MOC"** → `POST /unquarantine { confirmAsPhysicalPlate: true }`: `Quarantined=false`,
   `NeedsReview=false`, `Type` resta `Custom`, `LinkedSetId=null` (non è più legato a un set).
3. **"Elimina"** → `DELETE` (con conferma esplicita).

## F. Blocco del planner (decisione C)

Frontend: se `GET /api/baseplates` contiene **almeno una** riga `quarantined == true`,
`AggregateBpPlannerView` e `AggregatePlannerListView` mostrano una schermata di blocco al posto
del canvas:
- testo: *"N baseplates couldn't be converted automatically and must be reconciled before planning."*
- elenco (nome, dimensioni, `quarantineReason`) + link a `/baseplates` (sezione Unresolved);
- il gate è solo frontend, ricalcolato a ogni load (sblocco automatico).

## G. Planner: entità piazzabili (MOC/set)

- Sidebar: sezione **Baseplates** (catalogo, invariata) + **MOC / Sets** da
  `GET /api/baseplates/planner-entities`.
- Drag di una entità MOC/set: al drop, **scompone** in `TotalPlates` `PlacedBaseplate` reali del
  tipo `ModuleBaseplateId`, disposte `Cols×Rows` a partire dal punto di rilascio, tutte con
  `SourceSetId = SetId`. Nessuna nuova entità piazzabile: restano piastre reali.
- Canvas: le piastre con lo stesso `SourceSetId` sono raggruppate visivamente (bordo + etichetta
  col nome del set); sposta/elimina agiscono sul gruppo.
- Al load, `PlacedBaseplate.sourceSetId` viene letto dalla room e preservato in salvataggio.

## H. Set/MOC detail: dichiarazione piastre

Nella pagina del set (sia MOC sia set normali), sezione **"Baseplates used"**:
- legge `GET /api/baseplates/by-set/{setId}`;
- aggiunge con typeahead sul catalogo + quantità (`POST /api/baseplates/{id}/reservations`);
- rimuove (`DELETE /api/baseplates/{id}/reservations/{setId}`);
- mostra footprint derivato + impatto su disponibilità.

## I. Definition of Done
- `dotnet build` / `npm run build` verdi; nessun file di piano modificato; nessun test (non esistono).
- `.pi/tasks/<name>/result.md` con esito, file toccati, scostamenti, dubbi.
