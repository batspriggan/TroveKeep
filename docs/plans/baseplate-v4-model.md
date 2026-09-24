# Piano v4: modello "occupazione piastre" — MOC/set come entità di planner

Sostituisce le parti di modello di `baseplate-tab-plan.md` e `baseplate-addendum.md` in conflitto.
Le sezioni non toccate (library, riserve, NeedsReview, Build Check) restano valide.

## 0. Il problema scoperto a runtime

Le righe `Custom + LinkedSetId` (es. `Fattoria-1 Fattoria 64×64`) **non sono piastre**: sono MOC
travestite da piastra. Servivano solo a (a) recuperare la foto del set e (b) essere trascinate sul
planner come un blocco.

Oggi il planner le accetta come qualsiasi altra baseplate, quindi "una MOC piazzata" esiste già —
ma è un accidente del modello, non un concetto: `PlacedBaseplate.BaseplateId` punta a un record
finto, e nessuna pressione/occupazione di piastre reali viene registrata.

Conseguenza: **dissolvere quelle righe romperebbe i layout esistenti** (inclusi quelli di altri
utenti del progetto). La migrazione deve quindi preservare i posizionamenti. Da qui il piano.

## 1. Modello finale (concetti separati)

| Concetto | Cos'è | Dove vive | Serve a |
| --- | --- | --- | --- |
| **Catalogo** | tipo di piastra reale + giacenza (`100× 32×32 verde`) | `baseplates` | sapere cosa possiedo |
| **Occupazione** | piastre impegnate da una MOC/set (`Fattoria → 4× 32×32`) | `baseplates[].Reservations: [{SetId, Quantity}]` (embedded) | disponibilità = `Quantity − Σ` |
| **Footprint** | rettangolo che l'entità occupa sul tavolo (`64×64`) | **derivato** dall'occupazione | il planner la disegna e la conteggia |
| **Piazzamento** | dove sta sul tavolo | `Room.AggregateBpLayouts[].PlacedBaseplates` (istanze di piastre reali) | layout |

### 1.1 Regola d'oro

Una MOC/set è **piazzabile dal planner se e solo se ha le piastre impostate** (`Reservations` non
vuote). Il footprint si deriva: `cols×rows` dalla composizione, `64×64` stud se `2×2`.
Non esiste un campo `Footprint` persistito: una sola fonte di verità, niente drift.

`WidthStuds/DepthStuds` del footprint = somma/aggregazione delle piastre riservate secondo
l'arrangiamento dichiarato (vedi §1.2). Per il caso "quadrato abbinabile" (le tue MOC) è
`cols × moduleW` × `rows × moduleH`.

### 1.2 Composizione della riserva

Una MOC può usare piastre di tipi diversi (`1 verde + 3 grigie`). Quindi la riserva è una
**lista per tipo**, non un singolo numero:

```jsonc
// baseplates[0]  (32x32 verde)
"Reservations": [ { "SetId": "<fattoria>", "Quantity": 1, "CreatedAt": ... } ]
// baseplates[1]  (32x32 grigio)
"Reservations": [ { "SetId": "<fattoria>", "Quantity": 3, "CreatedAt": ... } ]
```

Il **footprint** non dipende dai colori: dipende da quante piastre e di quale modulo.
Il totale `4` determina `2×2`. Se le piastre sono di dimensioni diverse (32×32 + 16×32), il
footprint diventa ambiguo → warning in UI e footprint derivato dall'arrangiamento più semplice
(vedi §1.3).

### 1.2.1 Over-reservation = warning, mai errore

`ReservedQuantity` può **superare** `Quantity`: non è un errore ma un avvertimento
(`overReserved`, badge rosso nella library, chips filtro). La `Quantity` è provvisoria (è il
senso del flag `to review`) e l'utente la riconcilia dalla sezione Baseplates. Nessun endpoint,
repository o migrazione deve rifiutare una reservation per quantità insufficiente; la
disponibilità è calcolata e clampata a 0, ma il segnale `overReserved` resta visibile.

### 1.3 Arrangiamento (layout interno della MOC)

Serve sapere *come* sono disposte le N piastre per ricavare il rettangolo. Opzioni:

- **Derivato greedy** (fase 1): si assume l'arrangiamento più compatto e tendente al quadrato
  (`cols = ceil(sqrt(N * aspect))`), cioè `4 → 2×2`. È ciò che serve per le MOC quadrate.
- **Dichiarato** (fase 2, se serve): `Arrangement: [{BaseplateId, Col, Row, Rotation}]` sul set.
  Necessario solo per MOC non rettangolari o con piastre eterogenee.

Per ora **derivato greedy**, con warning quando le piastre non sono omogenee.

## 2. Il planner

### 2.1 Cosa piazza

Il planner piazza **piastre reali** (le entità del catalogo). Una MOC si piazza **come blocco**
che, una volta rilasciato, si scompone nell'insieme delle sue piastre: sul tavolo non c'è un
"blocco Fattoria", ci sono le 4 piastre 32×32 che la compongono.

- Sidebar: sezione **Baseplates** (catalogo) + sezione **MOC / Sets** (entità con piastre
  impostate), con anteprima del footprint derivato (`2×2 · 64×64 stud`) e contatore piastre.
- Drag di una entità MOC → il drop piazza N `PlacedBaseplate` (piastre reali) nell'arrangiamento
  derivato, ancorate al punto di rilascio.
- `PlacedBaseplate` guadagna `SourceSetId: Guid?` — l'etichetta di provenienza (null per piastre
  piazzate singolarmente). Serve a: raggruppare visivamente, spostare/eliminare il blocco come
  unità, e mostrare "Fattoria" sul canvas. **Le piastre restano quelle reali**: nessuna nuova
  entità piazzabile, nessun doppio conteggio.

### 2.2 Conteggi

Il `ModuleGrid` resta (opzionale, dichiarato), ma ora può essere **derivato dalle piastre
piazzate**: `cols×rows` = bounding box / modulo. `Build Check` conta per tipo di piastra reale,
quindi una MOC contribuisce con `1× verdi + 3× grigi` e il fabbisogno è già corretto.

### 2.3 Blocco del planner su quarantena (decisione C)

Se il catalogo contiene anche una sola riga **quarantined** (§4.4), il planner **non si apre**:
la rotta `AggregateBpPlannerView` (e `AggregatePlannerListView` con il conteggio piastre) mostra
una **schermata di blocco** al posto del canvas.

- Testo esplicito: *"N baseplates couldn't be converted automatically and must be reconciled
  before planning"*, con l'elenco (nome, dimensioni, motivo) e un link diretto alla library.
- Il blocco è **frontend**: nessuna condizione server-side mascherata da 404. Il backend espone
  `quarantined`/`quarantineReason` in `BaseplateResponse`, il planner decide.
- Il blocco vale **solo** per la pianificazione; la library resta accessibile (è lì che si
  riconcilia), così come Build Check (che legge i dati ma non permette di piazzare).
- Il gate si sblocca **automaticamente** quando la quarantena è risolta, perché la condizione è
  ricalcolata a ogni caricamento.
- Riuso del pattern esistente: un controllo in `onMounted` prima del rendering del canvas, con
  stato `blocked`.

## 3. UI

### 3.1 MOC / Set → dichiarazione piastre
Nella pagina del set (MOC e set normali), sezione **"Baseplates used"**:

- elenco righe `{tipo di piastra, quantità}` con aggiunta tramite typeahead sul catalogo;
- validazione: la quantità non può superare la disponibilità totale del tipo;
- anteprima del footprint derivato (`2×2 = 64×64 stud`) e dell'impatto sulla disponibilità
  (`4 verdi reserved → 96 libere`);
- modifica/rimozione → aggiorna le `Reservations` sui documenti baseplate.

### 3.2 Library Baseplates
- Colonna "Reserved" ora **cliccabile con dettaglio** per MOC (già presente come "Reserved by").
- Le righe `Custom + LinkedSetId` non esistono più (dissolte dalla `005`).
- Badge "to review" resta per le quantità non confermate.

### 3.3 Planner
- Sezione MOC/Sets nella sidebar, come §2.1.
- Blocchi MOC disegnati con contorno + etichetta, sopra le piastre reali che li compongono.

## 4. Migrazione `Migration_005_BaseplateQuantity` estesa (v4 → v5, unica)

> **Scelta**: le modifiche non sono ancora rilasciate e il DB di test viene ripristinato a v4,
> quindi la dissoluzione MOC **si fonde nella `005` esistente** invece di introdurre una `006`.
> Una sola migrazione, un solo backup pre-migrazione, un solo break v4→v5. La `005` fa, in
> ordine: (a) backfill campi, (b) dedup duplicati, (c) **dissoluzione MOC**.
>
> Ordine obbligatorio: la dedup è per chiave di business `Type|PartNum|LegoColorId|WidthStuds|DepthStuds`
> e le righe MOC (`Custom + LinkedSetId`) hanno `PartNum` proprio, quindi non vengono toccate; la
> dissoluzione gira **dopo** e opera solo sulle candidate rimaste.

Obiettivo: **dissolvere le righe MOC senza rompere nessun layout**, robusca per installazioni di
terzi (che potrebbero avere decine di righe MOC piazzate).

### 4.1 Identificazione
`Type == Custom && LinkedSetId != null` → candidata MOC.
(Le `Custom` senza `LinkedSetId` restano piastre fisiche vere.)

### 4.2 Per ogni riga candidata

1. **Trova il modulo reale**: cerca nel catalogo una baseplate *non-MOC* le cui dimensioni
   dividono esattamente `WidthStuds`/`DepthStuds` (es. `64×64` → `32×32` → `2×2`).
   Ordine di preferenza: stessa `LegoColorId`, poi `Standard` prima di `Road`, poi dimensione
   maggiore.
2. **Crea le riserve**: per ogni piastra componente, `AddReservation` sul tipo reale con
   `Quantity = numero di piastre`, `SetId = LinkedSetId`.
   - Se *non* esiste un tipo reale che divide le dimensioni (es. `60×60`), **non** dissolvere:
     vedi §4.4 (quarantena).
3. **Rimappa i piazzamenti** (`Room.AggregateBpLayouts[].PlacedBaseplates`):
   sostituisci **una** entry che puntava alla riga MOC con **N entry** (piastre reali) disposte
   2×2 (o nell'arrangiamento derivato) nel rettangolo che la entry occupava, con
   `SourceSetId = LinkedSetId` su ognuna.
   - Ogni nuova piastra riceve un `InstanceId` **nuovo** (Guid), generato in modo deterministico
     se possibile; le coordinate derivano da `XMm/YMm/Rotation` della entry originale.
4. **Cancella** la riga candidata dal catalogo.

### 4.3 Robustezza (requisiti espliciti: "non rompere i layout di nessuno")

- **Idempotenza**: la `005` usa la presenza della riga `Custom + LinkedSetId` come trigger; una
  seconda esecuzione è no-op. Il runner non la riesegue comunque, ma fail-soft.
- **Fail-soft per riga**: se una singola riga MOC non è convertibile, **non** si cancella e **non**
  si tocca il layout di quella riga; si annota in `meta` (`migration_005_unresolved`) e si prosegue.
  Mai abortire l'intera migrazione per una riga.
- **Riferimenti pendenti**: prima di cancellare, verifica che **nessun** `PlacedBaseplate`
  residuo punti alla riga; se ne resta qualcuno, la riga va in quarantena (non cancellata).
- **Piastre eterogenee**: il vecchio modello permetteva solo dimensioni quadrate o rettangolari in
  stud (`32×32`, `64×64`, `48×48`), quindi il caso `1× 32×32 + 3× 16×32` **non esiste in origine**
  e non va gestito dalla `005`. Vi si può arrivare solo *dopo*, editando a mano le reservation: in
  quel caso il planner resta apribile (non è quarantena) ma il footprint è ambiguo → l'utente
  risolve con la riconciliazione manuale, mentre **le posizioni in layout restano intatte**.
- **Export/import stanze**: verificato che `RoomExportService` esporta solo `room.json` +
  `templates.json` (non le basi), quindi **non** introduce riferimenti rotti. Nessuna modifica
  necessaria.
- **Ordine e atomicità**: MongoDB non ha transazioni su più collection qui; la `005` procede
  documenti per documento (read-modify-write sulla room interessata) e il fallimento parziale
  lascia la riga in quarantena, mai un riferimento pendente.

### 4.4 Quarantena (risoluzione in UI) — decisione 4
Quando la conversione non è possibile (modulo non abbinabile, nessuna piastra reale, piazzamenti
ambigui):

- la riga **resta** ma viene marcata `NeedsReview = true` **e** `Quarantined = true` (nuovo campo),
  con motivo (`QuarantineReason: string?`);
- la library mostra una sezione **"Unresolved MOC plates"** con, per ogni riga, un wizard di
  riconciliazione: scegli il tipo/i reali + quantità + arrangiamento, oppure "confermo che non è
  una MOC" (torna piastra fisica) o "elimina" (con conferma esplicita);
- **la riconciliazione aggiorna anche il planner**: chiudendo il wizard, i `PlacedBaseplate` che
  puntavano alla riga vengono convertiti con la stessa logica del §4.2.3 (stessa funzione di
  servizio, così migrazione e UI condividono il codice).

### 4.5 Cosa fa la 005 se un set non esiste più
Se `LinkedSetId` punta a un set cancellato: la riga va in quarantena con motivo
`"linked set not found"`; nessuna riserva creata (non c'è a chi attribuirla).

## 5. Modifiche di schema (tutte additive)

| Entità | Campo | Tipo | Note |
| --- | --- | --- | --- |
| `Baseplate` | `Quarantined` | `bool` | §4.4 |
| `Baseplate` | `QuarantineReason` | `string?` | §4.4 |
| `PlacedBaseplate` | `SourceSetId` | `Guid?` | provenienza MOC/set, §2.1 |
| `LegoSet` | *(nessuno)* | — | l'occupazione sta nelle `Reservations`, il footprint è derivato |

Nessun campo di footprint: derivato (§1.1).

## 6. Criteri di accettazione

- Una MOC con 4 piastre impostate appare nel planner come entità piazzabile con footprint `2×2`
  (`64×64`), e il suo piazzamento crea 4 `PlacedBaseplate` reali con `SourceSetId` valorizzato.
- `Quantity`/disponibilità: una MOC che riserva 4 piastre riduce le libere di 4, **senza** cambiare
  il totale.
- La `005` non lascia **nessun** `PlacedBaseplate` che punti a una riga cancellata
  (verificabile con una query di controllo).
- Una installazione di terzi con N righe MOC piazzate mantiene **tutte** le posizioni (convertite
  in piastre reali) e, dove non convertibili, le ritrova in quarantena — mai layout vuoti.
- La riconciliazione dalla UI produce lo stesso risultato della migrazione (codice condiviso).
- Le righe `Custom + LinkedSetId` non esistono più nel catalogo al termine.
- Con righe in quarantena, il planner mostra la schermata di blocco (non il canvas) e il link alla
  riconciliazione; senza quarantena si apre normalmente.

## 7. Decisioni prese

- **A** — `SourceSetId` **solo** per MOC/set; `null` per le piastre piazzate singolarmente. ✅
- **B** — Le piastre eterogenee non esistono nei dati originari (solo quadrati/rettangoli in stud:
  `32×32`, `64×64`, `48×48`): la `005` non le gestisce. Se l'utente le crea *dopo* editando le
  reservation, rimappa a mano; **il layout è comunque preservato**. ✅
- **C** — Se il catalogo contiene righe non riconciliate, **il planner non si apre** (§2.3):
  schermata di blocco con motivo e link alla riconciliazione, sblocco automatico al termine. ✅
