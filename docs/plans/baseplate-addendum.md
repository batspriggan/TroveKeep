# Addendum: importati, modulo piastre e verifica multi-configurazione

Estende `docs/plans/baseplate-tab-plan.md`. Concetti emersi in revisione.

Decisioni prese:

- **D1 = sì**: dedup automatica in migrazione, con rimappatura dei layout. Le righe fuse nascono `NeedsReview = true` (§A).
- **D2 = build check**: le basi nei layout **non scalano** la disponibilità; serve una vista che confronti il fabbisogno di N configurazioni selezionate con la giacenza (§C).

---

## A. Basi "importate" / da verificare (`NeedsReview`)

> Origine: la fattoria occupa 2×2 piastre, ma il dato derivato/importato non è affidabile finché l'utente non lo sistema. Le righe non verificate vanno marcate e devono produrre un warning visibile.

### Problema
Le righe di catalogo possono nascere da tre fonti, con affidabilità diversa:

1. **Ricerca part archive Rebrickable** (Standard/Road) → `PartNum` e nome sono affidabili, le **dimenstudi sono dedotte** dal nome (`GuessStudDimensions`, regex `(\d+)\s*[xX×]\s*(\d+)`): può fallire o dedurre male.
2. **Custom da set** → dimensioni **inserite a mano**: affidabili.
3. **Deduplica della `Migration_005`** → righe fuse da dati storici mai verificati: affidabilità ignota.

In tutti i casi l'utente non ha ancora confermato tipo/size/colore/forma.

### Modello
Campo nel documento `baseplates`:

| Campo | Tipo | Note |
| --- | --- | --- |
| `NeedsReview` | `bool` | **calcolato in creazione/update** da regole di completezza e precisione (sotto), non da manuale |

### Regola di calcolo (`NeedsReview`)

Il service imposta `NeedsReview = true` quando **manca un campo necessario alla pianificazione** o quando un campo è stato **dedotto/importato** e non confermato manualmente:

| Tipo | Campi necessari | `NeedsReview = true` se… |
| --- | --- | --- |
| Standard | `WidthStuds`, `DepthStuds`, `LegoColorId` | uno è assente/≤0 |
| Road | `WidthStuds`, `DepthStuds` | uno è assente/≤0. **`RoadShape` non è richiesto**: è metadato opzionale, e richiederlo ri-marcava per sempre le righe road legacy |
| Custom | `WidthStuds`, `DepthStuds` | uno è assente/≤0, oppure `Name`/`LinkedSetId`/immagine mancanti |
| tutte | `Quantity` | `Quantity < 1` |

Origine del dato (A1):
- **Custom inserita a mano con dati completi** → `NeedsReview = false` (nessuna review).
- **Custom incompleta** (manca nome, set collegato o dimensioni) → `NeedsReview = true`.
- **Part-search (Standard/Road)** → `NeedsReview = true` di default, perché le dimensioni sono **dedotte** dal nome (`GuessStudDimensions`); l'utente conferma al primo salvataggio.
- **Righe fuse dalla dedup della migrazione** → `NeedsReview = true`.

Regole operative:
- `CreateAsync` applica la funzione `ComputeNeedsReview(entry, isImport)`.
- `UpdateAsync` **ricalcola** `NeedsReview` dai dati aggiornati: se diventano completi/precisi passa a `false` automaticamente (niente pulsante separato obbligatorio).
- Un `POST /api/baseplates/{id}/confirm` resta disponibile per la conferma esplicita di una riga la cui unica mancanza è la conferma dell'import (forza `false`).

### UI
- **Badge "To review"** sulla riga.
- Filtro "solo da verificare".
- **Indicatore globale**: contatore warning sempre visibile nella **voce di navigazione Baseplates** (pallino con numero) e/o badge nel header della library. Stesso pattern già usato per i warning altrove; il numero arriva da un campo calcolato in `GET /api/baseplates` (`needsReviewCount`) oppure da un piccolo endpoint dedicato.
- Azione rapida "Confirm" per riga + "Confirm all filtered".

### Migrazione
`Migration_005` deve includere:

```csharp
await baseplates.UpdateManyAsync(
    Builders<BsonDocument>.Filter.Exists("NeedsReview", false),
    Builders<BsonDocument>.Update.Set("NeedsReview", true));   // mai verificati finora
```

---

## B. Modulo piastre del layout ("2×2 piastre")

### Problema
La fattoria *occupa 2×2 basi 32×32*. Questa informazione oggi **non esiste**: il planner registra quali piastre fisiche sono state posizionate su un aggregato, non il "progetto" dell'aggregato come griglia di moduli. Conseguenze:
- il fabbisogno aggregato (punto C) non sa quante basi serve comprare/usare per una certa configurazione finché non è disegnata piastra per piastra;
- gli aggregati non ancora disegnati esistono ma non contribuiscono a nessun conteggio.

### Decisione di modellazione (confermata: **B1 = dentro**)

Introdurre il concetto di **griglia modulo** a livello di aggregato, **derivabile e sovrascrivibile** (soft warning, **B2 = warning soft**):

- **Derivazione**: dai tavoli dell'aggregato si ricava la bounding box (già calcolata in `AggregateBpPlannerView.aggBounds`). Le **dimensioni del modulo** (base canonica dell'aggregato) vengono dall'eventuale `AggregateSelection.bpKey` già esistente (es. `"16x32"`): `cols = ceil(bboxW / baseW)`, `rows = ceil(bboxD / baseD)`.
- **Override manuale**: l'utente può dichiarare esplicitamente `moduleCols × moduleRows` (la fattoria = 2×2) quando il calcolo geometrico non è quello che intende (es. le piastre si sovrappongono a più tavoli).

### Dove vive il dato (NoSQL, embedded)

Dentro `Room`, nell'esistente `AggregateBpLayout`, accanto ai `PlacedBaseplates`:

```jsonc
"AggregateBpLayouts": [
  {
    "RepresentativeId": "…",
    "PlacedBaseplates": [ … ],      // piastre singole realmente piazzate (disegno fine)
    "LayoutVersion": 2,
    "ModuleGrid": {                  // NUOVO — progetto dell'aggregato
      "BaseplateId": Guid | null,    // base usata come modulo (null = si usa AggregateSelection.bpKey)
      "Cols": 2,
      "Rows": 2,
      "IsOverridden": true           // true = valore scelto dall'utente, false/assente = derivato
    }
  }
]
```

Note:
- `ModuleGrid` è **opzionale** (assente = derivato). Nessun campo obbligatorio nuovo.
- `Cols × Rows` è la sorgente del "fabbisogno dichiarato" dell'aggregato quando `PlacedBaseplates` è vuoto o incompleto.
- `LayoutVersion` sale a `2` per gli aggregati che hanno un `ModuleGrid`; il codice già gestisce `layoutVersion` come flag di formato (`needsFix`).

### Regole (service)
- `ModuleGrid.Cols/Rows ≥ 1`.
- Se presente e `PlacedBaseplates` presente: nessun blocco; la UI mostra un **warning soft** quando `Cols×Rows ≠ PlacedBaseplates.Count` (B2). Il disegno fine non viene mai sovrascritto dal modulo.
- Il "fabbisogno aggregato" (punto C) usa `ModuleGrid` se presente, altrimenti `PlacedBaseplates.Count`, altrimenti `Cols×Rows` derivato dalla bounding box.

### UI
- Nel planner (`AggregateBpPlannerView`): piccolo form "Module grid" con `Cols × Rows` e spunta "override", + chip che mostra "2×2 = 4 basi (dichiarato)" vs "3 basi disegnate".
- In `AggregatePlannerListView`: colonna "Plate need" che mostra il fabbisogno dichiarato dell'aggregato.

---

## C. Verifica multi-configurazione (risolve D2)

### Principio deciso
- Le basi usate nei layout **NON scalano** la disponibilità: una stessa base può servire configurazioni alternative per fiere diverse. La disponibilità resta `Quantity − Reservations` (riserve MOC).
- Serve invece un **controllo puntuale**: date N configurazioni selezionate, il fabbisogno totale per tipo di base supera la giacenza?

### Definizione "configurazione"
Una **configurazione** = un aggregato (room + representativeId) da portare in una data occasione. La selezione è un insieme di aggregati (anche di room diverse). Fase 1: selezione manuale; in futuro si potrà salvare un "evento" (insieme di aggregati + data + note).

### Calcolo (in memoria, lato server o client)
Per ogni aggregato selezionato, fabbisogno per `BaseplateId`:

```
need(bp) = Σ_aggregati {
             PlacedBaseplates di quell'aggregato dove BaseplateId = bp
             se PlacedBaseplates vuoto/parziale e ModuleGrid presente:
               espandi ModuleGrid in Cols*Rows pezzi di ModuleGrid.BaseplateId
           }
```
`ModuleGrid` senza `BaseplateId` → usa il `BaseplateId` del tipo base dell'aggregato (da `AggregateSelection.bpKey` risolto sul catalogo).

Poi per ogni base:
- `need` vs `Quantity − ReservedQuantity` → **OK / SHORT**;
- warning se `need > AvailableQuantity`.

### Endpoint dedicato

```
POST /api/baseplates/feasibility
{ "aggregates": [ { "roomId": "...", "representativeId": "..." }, ... ] }
→ 200
{
  "lines": [
    { "baseplateId": "...", "name": "32x32 Gray", "need": 6, "quantity": 12,
      "reserved": 2, "available": 10, "deficit": 0, "status": "ok" },
    { "baseplateId": "...", "name": "16x32 Gray", "need": 5, "quantity": 2,
      "reserved": 0, "available": 2, "deficit": 3, "status": "short" }
  ],
  "totalDeficit": 3,
  "hasShortage": true
}
```

Calcolo server-side in un `BaseplateService`/`PlanningService`, che legge room + catalogo in memoria (MongoDB: due query, composizione in C#). Il controller risolve i nomi come già fa con i colori.

### UI

Nuova **tab "Build Check" dentro il Table Planner** (C1, confermato: è lì che si fa la validazione), accanto a `Rooms` e `Baseplate Planner`:

- lista di tutti gli aggregati di tutte le room con checkbox (stessa base della `AggregatePlannerListView`, che già calcola gli aggregati di tutte le room);
- selezione di una o più configurazioni (C2: **solo volatile**, nessun salvataggio);
- pulsante "Check" → tabella esito: base · need · giacenza · riservate · disponibili · **deficit** (evidenziato in rosso);
- riepilogo con eventuale totale di pezzi mancanti e suggerimento "serve comprare N basi 32×32 grigie".

Nessun dato nuovo persistito: è una **vista di calcolo**. Coerente con "non scalare": selezionare 2 configurazioni non prenota nulla, mostra solo se stanno in piedi insieme.

### Struttura navigazione risultante

```
Nav di primo livello:  Sets · Bulk Pieces · Boxes · Drawer Containers · Search · Scanner · Archives · Baseplates* · Table Planner · Settings
Table Planner (tab):    Rooms | Baseplate Planner | Build Check**
```

\* `Baseplates` = library sempre visibile, slegata da `tablePlannerEnabled`.
\** `Build Check` = validazione multi-configurazione, dentro Table Planner, gated da `tablePlannerEnabled`.

---

## D. Impatto sulla migrazione (§3 del piano principale)

`Migration_005` diventa:

1. `Quantity` = 1 dove assente
2. `RoadShape` = null dove assente
3. `Reservations` = [] dove assente
4. `Notes` = null dove assente
5. **`NeedsReview` = true** dove assente
6. **dedup D1**: merge per chiave di business, somma `Quantity`, merge `Reservations`, rimappatura `Rooms[].AggregateBpLayouts[].PlacedBaseplates[].BaseplateId` → id canonico, `NeedsReview = true` sulle righe fuse

`ModuleGrid` **non** richiede migrazione: assente = derivato, e i documenti `Rooms` esistenti restano validi (`[BsonIgnoreExtraElements]`).
Nessun cambio di schema per le `Room` è obbligatorio → nessun rischio sui layout esistenti.

---

## E. Decisioni (risolte)

- **B1** = **dentro** — `ModuleGrid` embedded in `AggregateBpLayout`. ✅
- **B2** = **warning soft** — disegno fine mai sovrascritto; segnalazione di discrepanza `Cols×Rows ≠ PlacedBaseplates.Count`. ✅
- **C1** = **tab dentro il Table Planner** (non nella library). ✅
- **C2** = **volatile** in fase 1; eventuale `BuildPreset`/`Event` in fase 2 senza toccare lo schema ora. ✅
- **A1** = review **solo se manca informazione precisa**; Custom manuali complete → nessuna review. Vedi regola in §A. ✅
