# Task: stickers-orientation
- esito: success
- modello: deepseek/deepseek-flash:high
- file modificati: CLAUDE.md, README.md, src/TroveKeep.Api/Controllers/StickerProjectsController.cs, src/TroveKeep.Api/DTOs/Requests/StickerRequests.cs, src/TroveKeep.Api/DTOs/Responses/StickerResponse.cs, src/TroveKeep.Core/Models/StickerProject.cs, src/TroveKeep.Repositories/Documents/StickerProjectDocument.cs, src/TroveKeep.Repositories/StickerProjectRepository.cs, src/TroveKeep.Services/StickerProjectService.cs, src/TroveKeep.Services/StickerRules.cs, tests/TroveKeep.Tests/StickerServiceTests.cs, ui/src/api/stickerProjects.js, ui/src/utils/stickerImages.js, ui/src/views/stickers/StickerProjectView.vue, ui/tests/stickerFits.test.mjs, result.md
- test eseguiti: dotnet build (0 warning, 0 errori), dotnet test (50/50 verdi), cd ui && npm test (84/84 verdi, sfcBindings incluso), cd ui && npm run build (successo), controllo ad-hoc della resa con canvas 2D simulato (rotazione artwork verificata pixel per pixel)
- problemi aperti: `Fit` è in realtà persistito come valore numerico dell'enum, quindi `Orientation` è salvato esplicitamente come stringa (`BsonRepresentation(BsonType.String)`) per rispettare la specifica "store as string"; `ui/src/api/stickerImages.js` non è stato toccato perché l'orientamento appartiene all'item di progetto, non alla riga di libreria (viaggia in `toItemPayload` di `stickerProjects.js`); la resa pixel reale va comunque controllata in browser stampando un foglio
- riepilogo:
  Aggiunto `StickerOrientation { Auto, Landscape, Portrait }` (default `Auto`) a `StickerItem` nel
  Core, con documentazione che lo distingue da `allowRotate`. Backend: parsing per nome
  (`StickerRules.ParseOrientation`, sconosciuto → 400, assente → `Auto`), validazione
  `Enum.IsDefined` nel servizio, mapping del documento Mongo, `Orientation` in request e response.
  Frontend: `ORIENTATION_OPTIONS` ("As uploaded" / "Horizontal" / "Vertical"),
  `isStickerOrientation`/`normalizeOrientation`, `resolveEffectiveSource` che scambia gli assi
  PRIMA del fit (ordine ruota-poi-adatta), `computeFitGeometry` con parametro orientamento e
  `drawStickerRaster` che applica la rotazione dell'artwork con una trasformazione canvas attorno
  al rettangolo dell'etichetta (la rotazione del packer resta `rotateStickerRaster` sul raster
  finito: le due compongono). UI: selettore orientamento nel form "add item" e nella riga
  dell'item, persistito dal PUT debounced; etichetta in sola lettura per chi non può scrivere.
  Test: backend 6 nuovi casi (parse, default, rifiuto, round-trip create/update/response, mm
  invariati); frontend 7 nuovi test (opzioni, resolveEffectiveSource, i quattro fit con orientamento,
  raster ruotato, Auto invariato, mm invariati, composizione con la rotazione del packer).
