# Task: runs-r-20261107t102126-c22d-work-1-fix-1
- esito: success
- modello: deepseek/deepseek-flash:high
- file modificati: ui/src/views/tableplanner/RoomPlannerView.vue, ui/src/views/tableplanner/AggregateBpPlannerView.vue, ui/package.json, ui/tests/marquee.test.mjs (nuovo), CLAUDE.md
- test eseguiti: `cd ui && npm test` (node:test, 11/11 verdi su marquee.js), `cd ui && npm run build` (vite build ok), controlli statici del piano 12(a) (`grep -c selectedAggregateId … = 0`, `grep -cE "selectedId\b" … = 0`, `git status --porcelain src/` vuoto)
- problemi aperti: la checklist manuale del punto 12(c) richiede app + browser e le regole vietano di avviarla: la esegui tu con `cd src/TroveKeep.Api && dotnet run` e `cd ui && npm run dev`?
- riepilogo: Round di correzione del lavoro già committato (ff7b568): la multi-selezione era in HEAD, qui ho corretto i difetti trovati e aggiunto test ripetibili.
  1) Click "fantasma" dopo un drag: mousedown su tavolo/piastra + mouseup sul canvas produce un `click` sul canvas che svuotava la selezione appena creata/spostata. Ora `_dragJustEnded` (room) e `_suppressClick` (piastre) consumano quel click, azzerati al mousedown sul canvas vuoto.
  2) Touch: nel planner piastre `@touchstart.prevent` sopprime il click sintetico, quindi il tap non selezionava più nulla (in HEAD startDrag assegnava la selezione): ora il touchstart seleziona l'unità premuta.
  3) `_dragMoved` (piastre) valuta la soglia in canvas px (divisa per lo zoom), coerente con MARQUEE_THRESHOLD_PX.
  4) Il computed `selectedAggregateIds` è diventato `selectedAggIndexes`: il controllo 12(a) `grep -c selectedAggregateId = 0` passava solo per caso (matchava il plurale).
  5) Quando un drag di gruppo è bloccato da una sovrapposizione le istanze trascinate vengono marcate `overlapping` (azzerate al mouseup), come nel drag singolo.
  6) Clamp di gruppo: `clampGroupShift(bbox, roomW, roomD) = min(max(0,-minX), roomW-maxX)` per asse. Deviazione deliberata dal piano, la cui formula `min(max(0,-minX), max(0, roomW-maxX))` vale 0 anche quando il gruppo sfora a destra/in basso e quindi non lo riporterebbe dentro: la versione implementata e testata ferma davvero il gruppo ai muri.
  7) Test nel repo: `ui/tests/marquee.test.mjs` (normaliseMarquee, intersezione stretta senza contatto di bordo, soglia 3 px, clamp sui quattro lati e su gruppi più larghi della stanza) + script `npm test` a zero dipendenze; CLAUDE.md documenta il comando.
  Stato: selezione sempre volatile (mai in `serialise`/`serialisePlates`), rotazione e cancellazione restano singole, nessuna modifica backend/API/modello. Verificato: unit test verdi, build ok, grep 12(a) a zero. Non verificato: comportamento interattivo in browser (12(c)).
