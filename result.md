# Task: stickers-frontend
- esito: success
- modello: deepseek/deepseek-flash:high
- file modificati: ui/src/api/stickerImages.js (nuovo), ui/src/api/stickerProjects.js (nuovo), ui/src/api/stickers.js (rimosso), ui/src/composables/useStickerSheet.js, ui/src/utils/stickerSheets.js, ui/src/utils/stickerImages.js, ui/src/utils/stickerPdf.js, ui/src/views/stickers/StickerLibraryView.vue, ui/src/views/stickers/StickerProjectListView.vue (nuovo), ui/src/views/stickers/StickerProjectView.vue (nuovo), ui/src/components/stickers/StickerImagePicker.vue (nuovo), ui/src/components/stickers/StickerPrintPanel.vue (nuovo), ui/src/router/index.js, ui/src/components/AppNav.vue, ui/tests/stickerFits.test.mjs (nuovo), CLAUDE.md, README.md, result.md
- test eseguiti: cd ui && npm ci --prefer-offline (node_modules assente, 60 pacchetti da cache locale), cd ui && npm test (77/77 verdi, incluso sfcBindings su tutte e 5 le SFC sticker), cd ui && npm run build (successo), grep "api/stickers" e vecchia shape in ui/ (nessun risultato)
- problemi aperti: i quattro fit e il PDF sono verificati headless (geometria dei rettangoli sorgente/destinazione + canvas stub), la resa pixel reale va provata in browser stampando un foglio; il salvataggio debounced (600 ms) viene flushato alla navigazione ma non alla chiusura brutale della tab
- riepilogo:
  Riscritta la SPA sticker sul modello a tre livelli. `/stickers` è ora solo la libreria artwork
  (griglia, rinomina inline, upload multiplo con nome dal file, sostituisci/rimuovi immagine,
  elimina con messaggio del server quando l'immagine è usata), `/sticker-projects` elenca i
  progetti (nome, item, label totali, date, crea/elimina con ConfirmDialog) e `/sticker-projects/:id`
  è l'editor: picker artwork con thumbnail, footprint 1×1..2×8 o custom, copie, fit con etichette
  umane, note; lista item con copie/fit/note modificabili in place, remove, PUT debounced della
  lista intera con gli id, stato Saving…/Saved/errore, 409 → reload + messaggio, 400 (footprint
  duplicato) surfaceato; pannello di stampa (opzioni layout + totale copie + Generate PDF) spostato
  qui. `useStickerSheet` conserva solo le opzioni persistite; le copie sono campo dell'item.
  I fit (Contain default / Cover / Fill / Center) sono cotti nel raster: `computeFitGeometry`
  calcola i rettangoli src/dst, il loader disegna in una canvas la cui dimensione in px deriva
  dalla `graphic` del piano (`stickerGraphicMm`, condiviso col packer) ed è chiavata per item +
  orientamento, con la variante ruotata pre-rotata (crop rotation-aware) e alias jsPDF invariata.
  Rotte e nav aggiornate, Viewer readonly (canWriteAnywhere), Guest esclusi; CLAUDE.md/README allineati.
