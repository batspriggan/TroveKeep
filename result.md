# Task: sticker-packing
- esito: success
- modello: deepseek/deepseek-flash:high
- file modificati: CLAUDE.md, ui/src/utils/stickerSheets.js, ui/src/utils/stickerPdf.js, ui/src/components/stickers/StickerPrintPanel.vue, ui/tests/stickerSheets.test.mjs
- test eseguiti: `cd ui && npm test` (90 test, 90 pass, 0 fail), `cd ui && npm run build` (ok, solo il warning preesistente sui chunk > 500 kB), script node ad-hoc di misura e fuzz (2000 plan pseudo-casuali: 0 overlap, 0 violazioni del gap, 0 nondeterminismi), `grep -rn "page.rows\|\.rows"` sui tre path richiesti (nessun risultato)
- problemi aperti: le soglie richieste dal task (bottom edge ≤ 85 mm, densità ≥ 0.85) sono aritmeticamente irraggiungibili su questo dataset (vedi riepilogo): i test asseriscono l'ottimo dimostrato (93 mm, 0.8251) invece di un valore impossibile; nessun commit/push eseguito (lo fa il broker, come da regole)
- riepilogo: |
  Sostituito il shelf packing di planStickerSheet con un packer 2D bottom-left skyline
  (`{x,w,y}` plateau, candidato = start di segmento con skyline più bassa poi più a sinistra,
  `y+h <= bottom`, merge dei segmenti ad uguale altezza). La variante upright è sempre provata
  prima della ruotata; pagina nuova con skyline azzerata quando non c'è candidato; StickerFitError
  invariato per chi non entra in nessuna pagina. La prenotazione è `[x, x+w+step) × [y, y+h+step)`,
  con il gap incluso.
  Bug trovato col fuzz e corretto: l'altezza va letta su TUTTA l'impronta (gap compreso), non solo
  su `[x, x+w)`, altrimenti un'etichetta collocata a sinistra di una già presente accorcia il gap di
  taglio (e può abbassare lo skyline sotto di essa). Caso di regressione in test.
  `rows` rimosso: `pages: [{ index, cells }]`; aggiornati stickerPdf.js (`page.cells`),
  StickerPrintPanel.vue (`plan.pages[0].cells.length`), i test e CLAUDE.md. Il resto della
  geometria (stickerSizeMm, insetOf/stickerGraphicMm, cut/graphic, opzioni) è intatto.
  Misure sul dataset che riproduce il problema (25 etichette, opzioni default):
    prima (shelf): bottom edge 125 mm, blocco 182.5 × 117 mm, densità 12 800 / 21 352.5 = 0.5995
                   (il 23.5% citato nel task è area label / area contenuto = 0.2348)
    dopo (skyline): bottom edge 93 mm, blocco 182.5 × 85 mm, densità 12 800 / 15 512.5 = 0.8251
  Perché non 85 mm / 0.85: ogni etichetta è larga 16 mm con gap 2.5 mm, quindi la colonna di
  contenuto da 194 mm tiene al massimo 10 etichette; le 25 etichette valgono 800 mm di altezza più
  15 gap obbligatori (837.5 mm) → almeno 83.75 mm per colonna, e con le altezze discrete l'ottimo è
  esattamente 85 mm (5 × (64+16+2.5) accanto a 5 × (32+32+16+5)). Il piano prodotto è quindi
  all'ottimo aritmetico (0.8251), il valore massimo possibile su questo dataset.
