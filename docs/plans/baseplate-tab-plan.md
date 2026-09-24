# Piano: sezione "Baseplates" di primo livello

Stato: **proposta rivista — vedi anche `docs/plans/baseplate-addendum.md` (importati, module grid, build check)**.

Decisioni già prese con l'utente:

| # | Decisione |
| --- | --- |
| 1 | La gestione basi è **slegata dal toggle `tablePlannerEnabled`** ed è una **voce di primo livello** nella navigazione. |
| 2 | Le **Table Templates restano in Archives** (nessuno spostamento). |
| 3 | Si modella la **`Quantity` per tipo+colore** (es. "12× 32×32 grigio" in una sola riga). |
| 4 | Lo **stoccaggio fisico delle basi è rinviato** (non ora). |
| 5 | Si salva la **shape delle road** (`Straight`/`Curve`/`Junction`/`TJunction`/`Crossroad`). |
| 6 | **Modello A**: le MOC "fermano" N basi di un tipo tramite **riserve embedded nel documento baseplate** (nessuna collection separata). Solo le MOC, non i set normali. |

Nuova informazione esplicita: **questo è MongoDB, non SQL**. Il piano è scritto di conseguenza (aggregati embedded, operatori atomici, nessuna join).

---

## 1. Modello dati (NoSQL-native)

### 1.1 Principi

Il progetto già segue il pattern "aggregato radice + dati embedded": `Room` contiene `Layout`, `AggregateSelections` e `AggregateBpLayouts` (→ `PlacedBaseplates`) **dentro lo stesso documento**. Le riserve delle MOC seguono lo stesso principio: vivono **dentro il documento baseplate**, come richiesto.

Niente `baseplate_reservations` collection. Niente foreign key. L'integrità (somma riserve ≤ quantità, merge di riserve duplicate) è responsabilità del **service layer**, non del database.

### 1.2 Documento `baseplates` (dopo la modifica)

```jsonc
{
  "_id": Guid,                    // BSON standard
  "Type": 0 | 1 | 2,              // Standard | Road | Custom
  "PartNum": "3811",
  "Name": "Baseplate 32 x 32",
  "WidthStuds": 32,
  "DepthStuds": 32,
  "LegoColorId": 6,               // Rebrickable color id (0 per Road/Custom)
  "ImageCached": false,           // swatch di colore per le Standard, immagine per Road/Custom
  "LinkedSetId": Guid | null,     // legacy: base Custom agganciata a un set (immagine)
  "RoadShape": "Straight" | null, // NUOVO — solo baseplates Type = Road
  "Quantity": 12,                 // NUOVO — giacenza totale per tipo+colore
  "Reservations": [               // NUOVO — riserve MOC, embedded nell'aggregato
    { "SetId": Guid, "Quantity": 2, "CreatedAt": ISODate }
  ],
  "Notes": null,                  // NUOVO (opzionale)
  "NeedsReview": false,          // NUOVO — riga importata/dedotta da verificare (addendum §A)
  "CreatedAt": ISODate,
  "UpdatedAt": ISODate,
  "Version": 3                    // ottimistic concurrency già nel progetto
}
```

**Chiave di business** (identità della riga di catalogo): `Type + PartNum + LegoColorId + WidthStuds + DepthStuds`. Due basi "uguali" sono **una riga con `Quantity > 1`**; basi diverse per colore/forma/dimensione sono righe distinte (come da punto 6: "se non sono identiche saranno rappresentate da 2 righe diverse").

### 1.3 Quantità derivate (calcolate, non salvate)

Non si persistono valori derivati: si calcolano in lettura, così non si possono disallineare.

- `ReservedQuantity = Σ Reservations[].Quantity`
- `AvailableQuantity = Quantity − ReservedQuantity`
- `InLayoutQuantity = numero di PlacedBaseplates con questo BaseplateId in tutti i Room` (composizione in memoria)

> ⚠️ `InLayoutQuantity` è **informativo** e non sottrae dalla disponibilità in fase 1: vedi punto aperto **D2**.

### 1.4 Riserve: regole (service layer)

- Aggiungere una riserva per un `SetId` già presente → **incrementa** (stesso pattern del progetto: "duplicate location merges (increments)").
- `ReservedQuantity` non può superare `Quantity` → altrimenti `400`.
- `DELETE` riserva per `SetId` → rimuove la voce; se la MOC viene cancellata, la riserva va rimossa (aggiornare il flusso `LegoSetService`/delete set — oggi cancella anche le basi Custom collegate via `DeleteByLinkedSetIdAsync`).

---

## 2. Backend

### 2.1 Modello e document

- `Core/Models/Baseplate.cs`: `+ Quantity`, `+ RoadShape` (nuovo enum `RoadShape?`), `+ Notes`, `+ List<BaseplateReservation> Reservations`.
- `Core/Models/BaseplateReservation.cs`: `{ Guid SetId, int Quantity, DateTimeOffset CreatedAt }`.
- `Repositories/Documents/BaseplateDocument.cs`: gli stessi campi + `BaseplateReservationDocument` embedded.
- `[BsonIgnoreExtraElements]` già presente → i documenti vecchi restano leggibili anche senza migrazione.

### 2.2 Repository (`IBaseplateRepository`)

Mantenere le operazioni esistenti e aggiungere quelle **atomiche sul documento** (NoSQL: modifiche puntuali sull'aggregato, non read-modify-write dell'intero documento dove possibile):

- `Task<Baseplate?> UpdateAsync(Baseplate)` — update dei campi anagrafici, preservando `CreatedAt`, `ImageCached`, `LinkedSetId`.
- `Task AddOrIncrementReservationAsync(Guid baseplateId, Guid setId, int quantity)` — `$push`/`$inc` atomico con guardia di disponibilità.
- `Task RemoveReservationAsync(Guid baseplateId, Guid setId)` — `$pull`.
- `Task RemoveReservationsBySetIdAsync(Guid setId)` — `$pull` su tutte le basi (pulizia alla cancellazione della MOC).

La guardia "somma riserve ≤ Quantity" può essere imposta con un **update con aggregation pipeline** (MongoDB 4.2+, il driver supporta `UpdateOneAsync` con `PipelineDefinition`), oppure con controllo + `Version` (ottimistico, `ConcurrencyException` già esistente). Da scegliere in implementazione: preferibile la pipeline per atomicità reale.

### 2.3 Service (`IBaseplateService`)

- `UpdateAsync`, `AddReservationAsync`, `RemoveReservationAsync`, `RemoveReservationsBySetIdAsync`.
- Le regole di business (merge riserve, cap quantità, validazione RoadShape solo su tipo Road) stanno qui.

### 2.4 API (`BaseplatesController`)

| Metodo | Endpoint | Note |
| --- | --- | --- |
| `GET` | `/api/baseplates` | include `quantity`, `reservedQuantity`, `availableQuantity`, `inLayoutQuantity`, `reservations[]` (con `setDescription` denormalizzata **in memoria** dal controller, come già fa `BuildColorLookupAsync`) |
| `POST` | `/api/baseplates` | + `quantity`, `roadShape` |
| `PUT` | `/api/baseplates/{id}` | **NUOVO** — oggi manca: senza questo non si edita nulla dalla nuova tab |
| `POST` | `/api/baseplates/{id}/reservations` | body `{ setId, quantity }` |
| `DELETE` | `/api/baseplates/{id}/reservations/{setId}` | rimuove la riserva |
| `GET/POST` | `/api/baseplates/{id}/image` | invariati |
| `DELETE` | `/api/baseplates/{id}` | invariato |

DTO: aggiungere i campi a `CreateBaseplateRequest`/`BaseplateResponse`, nuovi `UpdateBaseplateRequest`, `CreateReservationRequest`, `ReservationResponse`.

`InvalidOperationException → 400`, `KeyNotFoundException → 404` (pattern del progetto).

---

## 3. Migrazione

### 3.1 `Migration_005_BaseplateQuantity` (v4 → v5)

> ⚠️ La versione **autorevole e completa** della migrazione è nell'addendum §D: include anche `NeedsReview = true` e la **dedup D1** (merge + rimappatura `PlacedBaseplates.BaseplateId`). Il codice qui sotto è il nucleo del backfill.

Additiva e idempotente: backfill dei campi nuovi sui documenti esistenti.

```csharp
public class Migration_005_BaseplateQuantity : IMigration
{
    public int VersionFrom => 4;
    public int VersionTo => 5;
    public string Description =>
        "Add Quantity, RoadShape, Reservations and Notes to existing baseplates documents.";

    public async Task RunAsync(IMongoDatabase database)
    {
        var baseplates = database.GetCollection<BsonDocument>("baseplates");

        await baseplates.UpdateManyAsync(
            Builders<BsonDocument>.Filter.Exists("Quantity", false),
            Builders<BsonDocument>.Update.Set("Quantity", 1));

        await baseplates.UpdateManyAsync(
            Builders<BsonDocument>.Filter.Exists("RoadShape", false),
            Builders<BsonDocument>.Update.Set("RoadShape", BsonNull.Value));

        await baseplates.UpdateManyAsync(
            Builders<BsonDocument>.Filter.Exists("Reservations", false),
            Builders<BsonDocument>.Update.Set("Reservations", new BsonArray()));

        await baseplates.UpdateManyAsync(
            Builders<BsonDocument>.Filter.Exists("Notes", false),
            Builders<BsonDocument>.Update.Set("Notes", BsonNull.Value));
    }
}
```

- Registrare in `MigrationRunner._migrations`; `"baseplates"` è già in `BackupCollections` → backup pre-migrazione automatico (`auto-backup-v4-*.json.gz`).
- `Exists(field, false)` ⇒ idempotente.
- **Nessuna perdita dati**: solo campi aggiunti.

### 3.2 D1 (aperto) — deduplica dei duplicati esistenti

Con la `Quantity` per tipo+colore, il DB attuale potrebbe contenere **più documenti identici** (stesso `Type/PartNum/Colore/Stud`). Con il nuovo modello dovrebbero diventare una sola riga con quantità sommata, altrimenti la library mostra duplicati e la disponibilità è frammentata.

Se confermi **D1 = sì**, la `Migration_005` esegue anche il merge (sempre idempotente, sempre con backup):

1. raggruppa per chiave di business e somma `Quantity`;
2. tiene un documento "canonico" per gruppo, migra le `Reservations` (accodandole);
3. **rimappa i riferimenti**: aggiorna `Rooms[].AggregateBpLayouts[].PlacedBaseplates[].BaseplateId` da ogni id duplicato all'id canonico;
4. elimina i documenti duplicati.

Senza D1, la migrazione resta la sola §3.1 e la dedup sarà manuale dall'utente in UI.

> ❓ **D1 — fondo automaticamente i duplicati esistenti (con rimappatura dei layout di stanza)?** Consigliato **sì**.

---

## 4. Frontend

### 4.1 Navigazione (voce di primo livello)

Nuova voce **Baseplates**, **non** condizionata da `settings.tablePlannerEnabled`:

- `ui/src/components/AppNav.vue`: `<RouterLink to="/baseplates">Baseplates</RouterLink>` (desktop; visibile sempre).
- `ui/src/components/BottomNav.vue`: nuova voce con icona (mobile).
- `ui/src/router/index.js`: route `/baseplates` (library) **fuori** dal guard `tablePlannerEnabled`.
- `ui/src/views/SettingsView.vue`: aggiornare il testo del toggle Table Planner (oggi dice "including rooms, table templates **and plates configuration**") → le basi non sono più dentro quel toggle.

Nota di chiarezza route: la library è `/baseplates`; il planner resta `/table-planner/baseplates...` (tab "Baseplate Planner"). Valutare in implementazione un rename del percorso planner per evitare confusione (`/table-planner/plate-planner`), senza cambiare la tab label.

### 4.2 Vista library `ui/src/views/baseplates/BaseplateListView.vue`

**A. Riepilogo**
- Totali per tipo (Standard / Road / Custom), superficie in stud² e m².
- Contatori: **totale**, **riservate** (MOC), **libere**, **in layout**.
- Filtri/chips per colore e tipo.

**B. Tabella catalogo**
- Colonne: Preview · Tipo · Part # · Nome · Dimensioni (stud) · Colore · **Quantità** (stepper) · **Riservate** · **Libere** · RoadShape (per Road) · Azioni.
- Filtri: tipo, dimensioni, colore, "solo libere / riservate / in layout".
- Ordinamento per tipo/dimensioni/colore/nome.

**C. Riserve MOC (inline)**
- Per ogni riga, sezione espandibile "Reserved by": elenco MOC con quantità, `+ Add MOC` (typeahead sui set/MOC già presente in `ArchivesView`), `Remove`.
- Avviso visivo quando `AvailableQuantity = 0` (riga non piazzabile dal planner).

**D. Form Add / Edit**
- Type Standard/Road/Custom; ricerca part archive (già `searchArchivePartsBaseplates`) con pre-guess stud (`GuessStudDimensions`).
- **Datalist dimensioni canoniche**: 8×8, 8×16, 16×16, 16×32, 32×32, 32×48, 48×48.
- **RoadShape** select (solo per Road).
- **Quantity** (default 1).
- Colore `ColorSelect` (solo Standard); Custom → typeahead set + `LinkedSetId`; upload immagine per Road/Custom.

**E. Link**
- In testa: il planner e la tab Build Check restano raggiungibili dalla nav Table Planner (visibile solo se `tablePlannerEnabled`).
- Badge **warning** se `needsReviewCount > 0` (righe importate/da verificare, addendum §A).

### 4.3 Integrazione planner (`AggregateBpPlannerView.vue`)
- Il pannello laterale mostra per ogni base `Quantity`, `Libere` (riserve MOC); segnala il piazzamento oltre la disponibilità.
- **Build Check** (addendum §C): nuova tab dentro il Table Planner per la validazione multi-configurazione; le basi in layout **non** scalano la disponibilità.
- `ModuleGrid` (addendum §B) salvato dentro `AggregateBpLayout`, con warning soft se disegno fine ≠ modulo.

### 4.4 Pulizia `ArchivesView.vue`
- Rimuovere la sezione `Baseplates` (UI righe ~259–353) e lo script collegato (`baseplates`, `newBp*`, `bpPendingFile`, `addBaseplate`, `removeBaseplate`, `onBpFileChange`, `saveBpImage`, ~righe 679–775, 845–847).
- Aggiornare gli import: `searchArchivePartsBaseplates` si sposta nella nuova vista; togliere da `tableplanner.js` le funzioni baseplate spostate.
- `ArchivesView` resta: stato archivi, upload CSV, colori, categorie, **e Table Templates** (invariati).
- Aggiornare `README.md` (features) e `CLAUDE.md` (architettura/route).

### 4.5 API frontend
- Nuovo `ui/src/api/baseplates.js` (spostato da `api/tableplanner.js`), con `getAllBaseplates`, `createBaseplate`, `updateBaseplate`, `deleteBaseplate`, `uploadBaseplateImage`, `getBaseplateImageUrl`, `addReservation`, `removeReservation`.

---

## 5. Fasi

| Fase | Contenuto | Dipendenza |
| --- | --- | --- |
| 1 | Backend: campi modello/document, `PUT /api/baseplates/{id}`, endpoint riserve, service rules, pipeline atomica | D1 deciso |
| 2 | `Migration_005` (+ eventuale dedup D1) e registrazione | fase 1 |
| 3 | Nuova `BaseplateListView` (spostamento UI da Archives), route + voce di primo livello AppNav/BottomNav, badge `NeedsReview` | fase 2 |
| 4 | Quantità, riserve MOC inline, filtri, riepilogo, `NeedsReview` UI | fase 3 |
| 5 | Integrazione planner: disponibilità (riserve MOC), `ModuleGrid` + warning soft, tab **Build Check** | fase 4 |
| 6 (rinviata) | Stoccaggio fisico basi | — |

## 6. Punti aperti (storico)

- ~~**D1** dedup duplicati in migrazione~~ → **sì**, con rimappatura dei layout (addendum §D).
- ~~**D2** basi in layout scalano la disponibilità?~~ → **no**; validazione via tab **Build Check** (addendum §C).

Restano i punti secondari dell'addendum §E, tutti risolti tranne eventuali raffinamenti futuri (eventi/preset in fase 2).

## 7. Criteri di accettazione

- Voce **Baseplates** di primo livello sempre visibile, **indipendente** da `tablePlannerEnabled`.
- La library gestisce `Quantity`, riserve MOC embedded, filtri/riepilogo; nessuna sezione basi in `ArchivesView`.
- `PUT /api/baseplates/{id}` funziona e preserva `CreatedAt`, `ImageCached`, `LinkedSetId`.
- Le riserve sono **nel documento baseplate** (nessuna collection dedicata); `ReservedQuantity ≤ Quantity` garantito dal service.
- `Migration_005` idempotente, additiva, registrata, con backup pre-migrazione; un DB v4 resta leggibile e ogni base ha `Quantity = 1`, `Reservations = []`, `RoadShape = null`.
- `RoadShape` salvata per le road.
- `NeedsReview` calcolato dalle regole (addendum §A); badge/contatore warning visibile.
- `ModuleGrid` salvato in `AggregateBpLayout`; warning soft su discordanza (addendum §B).
- Tab **Build Check** nel Table Planner con esito fabbisogno/deficit (addendum §C).
- Nessuna regressione nel calcolo copertura basi (`bpNaturalW/H`) e negli export/import stanza.
