# Task: stickers-backend
- esito: success
- modello: deepseek/deepseek-flash:high
- file modificati: src/TroveKeep.Core/Models/StickerImage.cs (nuovo), src/TroveKeep.Core/Models/StickerProject.cs (nuovo: StickerProject, StickerItem, StickerFit), src/TroveKeep.Core/Models/Sticker.cs (rimosso), src/TroveKeep.Core/Models/Image.cs, src/TroveKeep.Core/Interfaces/Repositories/IStickerImageRepository.cs (nuovo), src/TroveKeep.Core/Interfaces/Repositories/IStickerProjectRepository.cs (nuovo), src/TroveKeep.Core/Interfaces/Repositories/IStickerRepository.cs (rimosso), src/TroveKeep.Core/Interfaces/Services/IStickerImageService.cs (nuovo), src/TroveKeep.Core/Interfaces/Services/IStickerProjectService.cs (nuovo), src/TroveKeep.Core/Interfaces/Services/IStickerService.cs (rimosso), src/TroveKeep.Repositories/Documents/StickerImageDocument.cs (nuovo), src/TroveKeep.Repositories/Documents/StickerProjectDocument.cs (nuovo), src/TroveKeep.Repositories/Documents/StickerDocument.cs (rimosso), src/TroveKeep.Repositories/StickerImageRepository.cs (nuovo), src/TroveKeep.Repositories/StickerProjectRepository.cs (nuovo), src/TroveKeep.Repositories/StickerRepository.cs (rimosso), src/TroveKeep.Repositories/BackupService.cs, src/TroveKeep.Services/StickerRules.cs (nuovo), src/TroveKeep.Services/StickerImageService.cs (nuovo), src/TroveKeep.Services/StickerProjectService.cs (nuovo), src/TroveKeep.Services/StickerService.cs (rimosso), src/TroveKeep.Api/Controllers/StickerImagesController.cs (nuovo), src/TroveKeep.Api/Controllers/StickerProjectsController.cs (nuovo), src/TroveKeep.Api/Controllers/StickersController.cs (rimosso), src/TroveKeep.Api/DTOs/Requests/StickerRequests.cs (riscritto), src/TroveKeep.Api/DTOs/Responses/StickerResponse.cs (riscritto), src/TroveKeep.Api/Filters/CollectionWriteAnywhereFilter.cs, src/TroveKeep.Api/Filters/CollectionWriteTargetResolver.cs, src/TroveKeep.Api/Program.cs, src/TroveKeep.Migrations/MigrationRunner.cs, tests/TroveKeep.Tests/StickerServiceTests.cs, CLAUDE.md, README.md, result.md
- test eseguiti: dotnet build (0 warning, 0 errori), dotnet test (39/39 verdi), grep -rn "ImageReferenceType\.Sticker\b" src tests (nessun risultato), grep sui riferimenti ai tipi rimossi (solo testi di messaggi)
- problemi aperti: la SPA in ui/ usa ancora le rotte rimosse /api/stickers (fuori perimetro, task frontend separato); commit non eseguito per regola del broker
- riepilogo:
  Sostituito il modello `Sticker` a un livello con il modello a tre livelli su branch non pubblicato,
  senza migrazione: `StickerImage` (collection `sticker_images`, nome 1..80, artwork in `set_images`
  come `ImageReferenceType.StickerImage`), `StickerProject` (`sticker_projects`) con `StickerItem`
  embedded (ImageId, studs 1..32, Copies 1..999, Fit, Notes?) ed enum `StickerFit`
  {Contain(default), Cover, Fill, Center} serializzato per nome. `StudMm = 8` spostato su `StickerItem`;
  le response espongono WidthMm/DepthMm/ImageName/ImageCached. Regole nel service (400): immagini
  mancanti rifiutate nominando l'id, item duplicato (ImageId+studs) rifiutato, lista item anche vuota,
  update che sostituisce la lista conservando gli Id forniti, delete di un'immagine usata rifiutata con
  conteggio via `IStickerProjectRepository.CountUsagesAsync`.
  API: `/api/sticker-images` (GET, GET id, POST, PUT 409, DELETE 204 + image GET/POST/DELETE) e
  `/api/sticker-projects` (GET, GET id, POST 201, PUT 409, DELETE 204), letture `User,Admin`, scritture
  con `CollectionWriteAnywhereFilter`; `StickerImages`/`StickerProjects` in ExcludedControllers.
  Backup (BackupService + MigrationRunner) e DI in Program.cs aggiornati; CLAUDE.md e README riscritti.
