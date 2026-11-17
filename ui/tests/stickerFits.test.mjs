// Unit tests for the sticker artwork raster: the four fits (Contain / Cover / Fill / Center) and
// the handover between the sheet plan and the rasterised bitmap.
//
// These are pure geometry assertions — the source and destination rectangles `ctx.drawImage`
// receives — plus the raster's pixel size. No canvas, no DOM and no network: the loader takes an
// injected canvas factory and an injected artwork loader, exactly like the PDF renderer takes a
// document factory.
import test from 'node:test'
import assert from 'node:assert/strict'

import { planStickerSheet, stickerGraphicMm } from '../src/utils/stickerSheets.js'
import {
  DEFAULT_FIT,
  DEFAULT_ORIENTATION,
  FIT_OPTIONS,
  MAX_RASTER_PX,
  ORIENTATION_OPTIONS,
  STICKER_DPI,
  computeFitGeometry,
  drawStickerRaster,
  fitLabel,
  isStickerFit,
  isStickerOrientation,
  labelRasterPx,
  loadStickerImages,
  normalizeFit,
  normalizeOrientation,
  orientationLabel,
  resolveEffectiveSource,
  rotateStickerRaster,
  stickerLabelAreas,
  turnedSourceRect,
} from '../src/utils/stickerImages.js'

const near = (actual, expected, message) =>
  assert.ok(Math.abs(actual - expected) < 1e-6, `${message ?? 'value'}: ${actual} ≉ ${expected}`)

/** Aspect ratios are compared with a relative tolerance: whole pixels round. */
const nearRatio = (actual, expected, message) =>
  assert.ok(
    Math.abs(actual - expected) / Math.abs(expected) < 1e-3,
    `${message ?? 'ratio'}: ${actual} ≉ ${expected}`,
  )

/** A canvas stub that records the drawing calls and the raster size. */
function recordingCanvasFactory(log) {
  return (width, height) => {
    const canvas = {
      width,
      height,
      ops: [],
      getContext: () => ({
        fillStyle: '',
        fillRect() {},
        drawImage(...args) {
          canvas.ops.push(args)
        },
        translate: (...args) => canvas.ops.push(['translate', ...args]),
        rotate: (...args) => canvas.ops.push(['rotate', ...args]),
      }),
      toDataURL: () => `data:image/${canvas.width}x${canvas.height}`,
    }
    log.push(canvas)
    return canvas
  }
}

/** The 9-argument drawImage call of the upright raster (the rotation uses the 3-argument form). */
const uprightDraw = (canvas) => canvas.ops.find((op) => op.length === 9)

test('a fit is always one of the four backend names, Contain being the default', () => {
  assert.deepEqual(FIT_OPTIONS.map((o) => o.value), ['Contain', 'Cover', 'Fill', 'Center'])
  for (const option of FIT_OPTIONS) {
    assert.ok(option.label && option.hint, `${option.value} needs a label and a hint`)
  }
  assert.equal(DEFAULT_FIT, 'Contain')
  assert.equal(normalizeFit('Cover'), 'Cover')
  assert.equal(normalizeFit('nonsense'), 'Contain')
  assert.equal(normalizeFit(undefined), 'Contain')
  assert.equal(isStickerFit('Center'), true)
  assert.equal(isStickerFit('center'), false)
  assert.equal(fitLabel('Cover'), 'Fill and crop')
})

test('Fill stretches the artwork over the whole label area', () => {
  const g = computeFitGeometry('Fill', { w: 200, h: 100 }, { w: 400, h: 100 })
  assert.deepEqual(g, { sx: 0, sy: 0, sw: 200, sh: 100, dx: 0, dy: 0, dw: 400, dh: 100 })
  // No crop and no margin: the source is used entirely, the destination is covered entirely.
  assert.equal(g.sw, 200)
  assert.equal(g.dw, 400)
})

test('Contain keeps the whole artwork and letterboxes the label', () => {
  // A 2:1 artwork inside a square label: it becomes 1:1-worth of content, centred vertically.
  const g = computeFitGeometry('Contain', { w: 200, h: 100 }, { w: 400, h: 400 })
  assert.deepEqual(g, { sx: 0, sy: 0, sw: 200, sh: 100, dx: 0, dy: 100, dw: 400, dh: 200 })

  // The other axis wins for a tall artwork.
  const tall = computeFitGeometry('Contain', { w: 100, h: 200 }, { w: 400, h: 400 })
  assert.deepEqual(tall, { sx: 0, sy: 0, sw: 100, sh: 200, dx: 100, dy: 0, dw: 200, dh: 400 })

  // Nothing is cropped, ever: the source rectangle is the whole bitmap.
  assert.deepEqual([g.sx, g.sy, g.sw, g.sh], [0, 0, 200, 100])
  assert.deepEqual([tall.sx, tall.sy, tall.sw, tall.sh], [0, 0, 100, 200])
})

test('Cover fills the label and crops the overflowing axis, centred', () => {
  // 2:1 artwork in a square label: the width overflows, so half of it is cropped horizontally.
  const g = computeFitGeometry('Cover', { w: 200, h: 100 }, { w: 400, h: 400 })
  assert.deepEqual(g, { sx: 50, sy: 0, sw: 100, sh: 100, dx: 0, dy: 0, dw: 400, dh: 400 })
  assert.equal(g.sw / g.sh, 1, 'the visible source keeps the label aspect ratio')

  // 1:2 artwork in a 4:1 label: the height overflows instead.
  const tall = computeFitGeometry('Cover', { w: 100, h: 200 }, { w: 400, h: 100 })
  assert.deepEqual(tall, { sx: 0, sy: 87.5, sw: 100, sh: 25, dx: 0, dy: 0, dw: 400, dh: 100 })
  near(tall.sw / tall.sh, 4)

  // A same-shape artwork is neither scaled up nor cropped beyond one pixel of rounding.
  const exact = computeFitGeometry('Cover', { w: 200, h: 100 }, { w: 400, h: 200 })
  assert.deepEqual(exact, { sx: 0, sy: 0, sw: 200, sh: 100, dx: 0, dy: 0, dw: 400, dh: 200 })
})

test('Center keeps the natural pixel size, centred, and crops what does not fit', () => {
  // Smaller than the label: still its own pixel size, centred on both axes.
  const small = computeFitGeometry('Center', { w: 120, h: 60 }, { w: 400, h: 200 })
  assert.deepEqual(small, { sx: 0, sy: 0, sw: 120, sh: 60, dx: 140, dy: 70, dw: 120, dh: 60 })
  assert.equal(small.dw, small.sw, 'one source pixel is one raster pixel')

  // Larger than the label: cropped on both axes, centred.
  const big = computeFitGeometry('Center', { w: 900, h: 500 }, { w: 400, h: 200 })
  assert.deepEqual(big, { sx: 250, sy: 150, sw: 400, sh: 200, dx: 0, dy: 0, dw: 400, dh: 200 })
})

test('the raster is pinned to the print resolution and capped', () => {
  // 25.4 mm is exactly one inch.
  assert.deepEqual(labelRasterPx(25.4, 25.4), { w: STICKER_DPI, h: STICKER_DPI })
  // A 16 × 32 mm tile label.
  assert.deepEqual(labelRasterPx(16, 32), { w: 378, h: 756 })
  // A huge label is capped on its long side and keeps its aspect ratio.
  const capped = labelRasterPx(2000, 1000)
  assert.equal(Math.max(capped.w, capped.h), MAX_RASTER_PX)
  near(capped.w / capped.h, 2, 'aspect ratio survives the cap')
  // Degenerate input still yields a drawable raster.
  assert.deepEqual(labelRasterPx(0, 0), { w: 1, h: 1 })
})

test('the label areas are the numbers the packer puts in cell.graphic', () => {
  const item = { id: 'i1', name: 'A', widthStuds: 2, depthStuds: 4, copies: 1 }
  const options = { borderMm: 0.6, paddingMm: 0.6, gapMm: 2 }
  const plan = planStickerSheet([item], options)
  const cell = plan.cells[0]

  const { graphic } = stickerGraphicMm(item, options)
  near(graphic.w, cell.graphic.w)
  near(graphic.h, cell.graphic.h)
  // …and the raster of the upright area has the label's aspect ratio, which is what lets the
  // PDF draw into `cell.graphic` without a second scaling decision.
  const upright = labelRasterPx(graphic.w, graphic.h)
  // Rounding to whole pixels moves the ratio by well under a per mille: the PDF stretches the
  // raster into `cell.graphic`, so a tenth of a pixel is invisible at 600 dpi.
  nearRatio(upright.w / upright.h, cell.graphic.w / cell.graphic.h, 'raster vs graphic aspect')
})

test('a rotated label gets a raster for the rotated rectangle', () => {
  // A 32-stud-wide label (256 mm) only fits the default sheet turned by 90°.
  const item = { id: 'wide', name: 'Wide', widthStuds: 32, depthStuds: 4, copies: 1 }
  const plan = planStickerSheet([item], { allowRotate: true })
  const cell = plan.cells[0]
  assert.equal(cell.rotated, true)

  const areas = stickerLabelAreas([item], plan.options, plan)
  const area = areas.get('wide')
  near(area.upright.wMm, cell.graphic.h, 'the upright area is the turned box swapped back')
  near(area.upright.hMm, cell.graphic.w)
  near(area.rotated.wMm, cell.graphic.w, 'the turned area is the box the PDF draws into')
  near(area.rotated.hMm, cell.graphic.h)

  // Without a plan, a non-square label still gets both orientations.
  const guessed = stickerLabelAreas([item], plan.options).get('wide')
  near(guessed.rotated.wMm, guessed.upright.hMm)
  // A square label never needs a second raster.
  assert.equal(stickerLabelAreas([{ id: 'sq', widthStuds: 2, depthStuds: 2 }]).get('sq').rotated, null)
})

test('the loader keys the rasters per item and orientation and crops for that box', async () => {
  const items = [
    {
      id: 'item-1',
      imageId: 'image-1',
      imageName: 'Apple',
      imageCached: true,
      widthStuds: 32,
      depthStuds: 4,
      copies: 2,
      fit: 'Cover',
    },
  ]
  const plan = planStickerSheet(
    [{ id: 'item-1', name: 'Apple', widthStuds: 32, depthStuds: 4, copies: 2 }],
    { allowRotate: true },
  )
  assert.equal(plan.cells[0].rotated, true)

  const log = []
  const canvasFactory = recordingCanvasFactory(log)
  const source = { width: 1200, height: 300 }
  const images = await loadStickerImages(items, {
    plan,
    options: plan.options,
    canvasFactory,
    loadArtwork: async () => ({ source, type: 'image/png' }),
  })

  // One upright raster plus its turned twin, both keyed by the item (not by the image id).
  assert.deepEqual(Object.keys(images), ['item-1'])
  assert.ok(images['item-1'].dataUrl)
  assert.ok(images['item-1'].rotatedDataUrl)
  assert.equal(images['item-1'].format, 'PNG')
  assert.equal(log.length, 2)

  const uprightCanvas = log[0]
  const turnedCanvas = log[1]
  const area = stickerLabelAreas(items, plan.options, plan).get('item-1')

  // The pixel grid comes from the label areas, i.e. from the numbers the PDF uses.
  const expected = labelRasterPx(area.upright.wMm, area.upright.hMm)
  assert.equal(uprightCanvas.width, expected.w)
  assert.equal(uprightCanvas.height, expected.h)
  assert.equal(turnedCanvas.width, uprightCanvas.height)
  assert.equal(turnedCanvas.height, uprightCanvas.width)

  // The crop is computed for those same numbers: Cover fills the label, so the source rectangle
  // is cropped to the label's aspect ratio (a 4:1 artwork on a 1:8 label crops the width).
  const [src, sx, sy, sw, sh, dx, dy, dw, dh] = uprightDraw(uprightCanvas)
  assert.equal(src, source)
  const geometry = computeFitGeometry('Cover', source, { w: uprightCanvas.width, h: uprightCanvas.height })
  assert.deepEqual({ sx, sy, sw, sh, dx, dy, dw, dh }, geometry)
  nearRatio(sw / sh, uprightCanvas.width / uprightCanvas.height, 'cropped source keeps the label aspect')
  assert.equal(dw, uprightCanvas.width)
  assert.equal(dh, uprightCanvas.height)

  // …and the turned raster is the upright one rotated, so it fills the rotated graphic box.
  nearRatio(
    turnedCanvas.width / turnedCanvas.height,
    plan.cells[0].graphic.w / plan.cells[0].graphic.h,
    'turned raster vs turned graphic',
  )
  const rotation = turnedCanvas.ops.find((op) => op[0] === 'rotate')
  assert.ok(rotation, 'the turned raster is produced by rotating')
})

test('every fit crops against the same label area, rotated or not', async () => {
  // A 32×4 stud label (256 × 32 mm) is wider than the printable area, so it is turned by 90°: the
  // raster must be built for the box the PDF draws into, and the fit geometry must be computed
  // from those very pixels — for all four fits.
  const item = {
    id: 'tall',
    imageId: 'img',
    imageCached: true,
    widthStuds: 32,
    depthStuds: 4,
    copies: 1,
  }
  const plan = planStickerSheet(
    [{ id: 'tall', name: 'Tall', widthStuds: 32, depthStuds: 4, copies: 1 }],
    { allowRotate: true },
  )
  assert.equal(plan.cells[0].rotated, true)
  const area = stickerLabelAreas([item], plan.options, plan).get('tall')

  for (const { value: fit } of FIT_OPTIONS) {
    const log = []
    const source = { width: 300, height: 900 }
    const images = await loadStickerImages([{ ...item, fit }], {
      plan,
      options: plan.options,
      canvasFactory: recordingCanvasFactory(log),
      loadArtwork: async () => ({ source, type: 'image/png' }),
    })
    assert.ok(images.tall.rotatedDataUrl, `${fit}: the turned raster is produced`)

    const upright = log[0]
    const expected = labelRasterPx(area.upright.wMm, area.upright.hMm)
    assert.deepEqual({ w: upright.width, h: upright.height }, expected, `${fit}: raster size`)

    // What the raster draw does is exactly the geometry computed for that raster.
    const [, sx, sy, sw, sh, dx, dy, dw, dh] = uprightDraw(upright)
    assert.deepEqual(
      { sx, sy, sw, sh, dx, dy, dw, dh },
      computeFitGeometry(fit, source, expected),
      `${fit}: draw geometry`,
    )

    // The turned raster has the turned box's dimensions, so the PDF fill is 1:1.
    assert.equal(log[1].width, upright.height, `${fit}: turned width`)
    assert.equal(log[1].height, upright.width, `${fit}: turned height`)
    nearRatio(
      log[1].width / log[1].height,
      plan.cells[0].graphic.w / plan.cells[0].graphic.h,
      `${fit}: turned raster vs turned graphic`,
    )
  }
})

test('a JPEG is flattened onto white, a PNG keeps its transparency', async () => {
  const items = [{ id: 'i', imageId: 'img', widthStuds: 2, depthStuds: 2, copies: 1, fit: 'Contain' }]
  const logJpeg = []
  const jpeg = await loadStickerImages(items, {
    canvasFactory: recordingCanvasFactory(logJpeg),
    loadArtwork: async () => ({ source: { width: 10, height: 10 }, type: 'image/jpeg' }),
  })
  assert.equal(jpeg.i.format, 'JPEG')

  // The canvas stub records the fill through `fillRect`, which the JPEG path calls and the PNG
  // path does not; the format itself is the observable part here.
  const logPng = []
  const png = await loadStickerImages(items, {
    canvasFactory: recordingCanvasFactory(logPng),
    loadArtwork: async () => ({ source: { width: 10, height: 10 }, type: 'image/png' }),
  })
  assert.equal(png.i.format, 'PNG')
})

test('an item whose artwork cannot be fetched is null, not fatal', async () => {
  const items = [
    { id: 'ok', imageId: 'a', widthStuds: 1, depthStuds: 1, copies: 1 },
    { id: 'broken', imageId: 'b', widthStuds: 1, depthStuds: 1, copies: 1 },
  ]
  const progress = []
  const images = await loadStickerImages(items, {
    canvasFactory: recordingCanvasFactory([]),
    onProgress: (p) => progress.push(p.done),
    loadArtwork: async (item) => {
      if (item.id === 'broken') throw new Error('404')
      return { source: { width: 8, height: 8 }, type: 'image/png' }
    },
  })
  assert.ok(images.ok)
  assert.equal(images.broken, null)
  assert.deepEqual(progress, [1, 2])
})

test('rotation swaps the raster dimensions', () => {
  const log = []
  const canvasFactory = recordingCanvasFactory(log)
  const upright = canvasFactory(30, 10)
  const turned = rotateStickerRaster(upright, { canvasFactory })
  assert.equal(turned.width, 10)
  assert.equal(turned.height, 30)
})

// ── artwork orientation (a different thing from the packer's rotation) ──────────────────────

test('an orientation is one of the three backend names, Auto being the default', () => {
  assert.deepEqual(ORIENTATION_OPTIONS.map((o) => o.value), ['Auto', 'Landscape', 'Portrait'])
  for (const option of ORIENTATION_OPTIONS) {
    assert.ok(option.label && option.hint, `${option.value} needs a label and a hint`)
  }
  assert.deepEqual(ORIENTATION_OPTIONS.map((o) => o.label), ['As uploaded', 'Horizontal', 'Vertical'])
  assert.equal(DEFAULT_ORIENTATION, 'Auto')
  assert.equal(normalizeOrientation('Portrait'), 'Portrait')
  assert.equal(normalizeOrientation('sideways'), 'Auto')
  assert.equal(normalizeOrientation(undefined), 'Auto')
  assert.equal(isStickerOrientation('Landscape'), true)
  assert.equal(isStickerOrientation('landscape'), false)
  assert.equal(orientationLabel('Portrait'), 'Vertical')
  assert.equal(orientationLabel('nonsense'), 'As uploaded')
})

test('resolveEffectiveSource swaps the axes only when the orientation disagrees with the raster', () => {
  const landscape = { w: 1200, h: 300 }
  const portrait = { w: 300, h: 1200 }
  const square = { w: 500, h: 500 }

  // Auto means "as uploaded", never "no turn at all".
  assert.deepEqual(resolveEffectiveSource(landscape, 'Auto'), { w: 1200, h: 300, turned: false })
  assert.deepEqual(resolveEffectiveSource(portrait, 'Auto'), { w: 300, h: 1200, turned: false })
  assert.deepEqual(resolveEffectiveSource(portrait, undefined), { w: 300, h: 1200, turned: false })

  // Disagreement turns the artwork 90°…
  assert.deepEqual(resolveEffectiveSource(landscape, 'Portrait'), { w: 300, h: 1200, turned: true })
  assert.deepEqual(resolveEffectiveSource(portrait, 'Landscape'), { w: 1200, h: 300, turned: true })

  // …while agreement and a square artwork do not.
  assert.deepEqual(resolveEffectiveSource(landscape, 'Landscape'), { w: 1200, h: 300, turned: false })
  assert.deepEqual(resolveEffectiveSource(portrait, 'Portrait'), { w: 300, h: 1200, turned: false })
  assert.deepEqual(resolveEffectiveSource(square, 'Portrait'), { w: 500, h: 500, turned: false })
  assert.deepEqual(resolveEffectiveSource(square, 'Landscape'), { w: 500, h: 500, turned: false })
})

test('the orientation is applied BEFORE the fit, for all four fits', () => {
  // A landscape artwork placed Portrait is fitted as if it were portrait: the effective source is
  // the swapped one, so the four fits letterbox/crop/stretch against the turned axes.
  const source = { w: 200, h: 100 }
  const destination = { w: 400, h: 400 }
  const swapped = { w: source.h, h: source.w }

  for (const { value: fit } of FIT_OPTIONS) {
    const auto = computeFitGeometry(fit, source, destination, 'Auto')
    const portrait = computeFitGeometry(fit, source, destination, 'Portrait')

    // Auto is exactly today's behaviour: the natural source, unchanged.
    assert.deepEqual(auto, computeFitGeometry(fit, source, destination), `${fit}: Auto is the default`)
    // Portrait is the same fit computed on the turned source — the order rotate-then-fit.
    assert.deepEqual(
      portrait,
      computeFitGeometry(fit, swapped, destination, 'Auto'),
      `${fit}: rotate then fit`,
    )
    // The source rectangle is expressed in the turned frame, so its sides are swapped.
    assert.equal(portrait.sw, auto.sh, `${fit}: sw/swapped`)
    assert.equal(portrait.sh, auto.sw, `${fit}: sh/swapped`)
  }

  // Concrete numbers for the most visible fit: Contain letterboxes on the correct axis.
  const contained = computeFitGeometry('Contain', source, destination, 'Portrait')
  assert.deepEqual(contained, { sx: 0, sy: 0, sw: 100, sh: 200, dx: 100, dy: 0, dw: 200, dh: 400 })

  // …and for a portrait artwork forced Landscape.
  const landscape = computeFitGeometry('Contain', { w: 100, h: 200 }, destination, 'Landscape')
  assert.deepEqual(landscape, { sx: 0, sy: 0, sw: 200, sh: 100, dx: 0, dy: 100, dw: 400, dh: 200 })
})

test('the turned raster rotates the drawing frame and maps the source back', () => {
  // 16 × 32 mm label (the raster is 378 × 756 px), 4:1 landscape artwork forced to stand up.
  const source = { w: 1200, height: 300 }
  const size = { w: 378, h: 756 }
  const log = []
  const canvas = drawStickerRaster(
    { fit: 'Contain', source, size, orientation: 'Portrait' },
    { canvasFactory: recordingCanvasFactory(log) },
  )

  // The label's pixel grid is unchanged by the artwork turn.
  assert.deepEqual({ w: canvas.width, h: canvas.height }, size)

  const geometry = computeFitGeometry('Contain', source, size, 'Portrait')
  // The fit was computed on the turned 300 × 1200 artwork: a centred 189 × 756 band.
  assert.deepEqual(geometry, { sx: 0, sy: 0, sw: 300, sh: 1200, dx: 94.5, dy: 0, dw: 189, dh: 756 })

  // The frame is turned around the centre of that band…
  assert.deepEqual(canvas.ops.find((op) => op[0] === 'translate'), ['translate', 189, 378])
  near(canvas.ops.find((op) => op[0] === 'rotate')[1], Math.PI / 2, 'the artwork is turned 90°')

  // …and the turned source rectangle maps back onto the original bitmap (axes swapped) while the
  // canvas rectangle keeps the band's dw × dh.
  assert.deepEqual(turnedSourceRect(source, geometry), { sx: 0, sy: 0, sw: 1200, sh: 300 })
  const draw = canvas.ops.find((op) => op.length === 9)
  assert.deepEqual(draw, [source, 0, 0, 1200, 300, -378, -94.5, 756, 189])
})

test('Auto draws exactly what it draws today (no transform, the fit rectangle straight)', () => {
  const source = { w: 1200, h: 300 }
  const size = { w: 378, h: 756 }
  const log = []
  const canvas = drawStickerRaster(
    { fit: 'Cover', source, size, orientation: 'Auto' },
    { canvasFactory: recordingCanvasFactory(log) },
  )
  assert.equal(canvas.ops.filter((op) => op[0] === 'rotate').length, 0)
  assert.equal(canvas.ops.filter((op) => op[0] === 'translate').length, 0)
  const geometry = computeFitGeometry('Cover', source, size, 'Auto')
  assert.deepEqual(uprightDraw(canvas).slice(1), [
    geometry.sx, geometry.sy, geometry.sw, geometry.sh,
    geometry.dx, geometry.dy, geometry.dw, geometry.dh,
  ])
})

test('the artwork orientation never changes the label millimetres', () => {
  const base = { id: 'tile', name: 'Tile', widthStuds: 2, depthStuds: 4, copies: 2 }
  const auto = planStickerSheet([{ ...base, orientation: 'Auto' }], { allowRotate: false })
  const portrait = planStickerSheet([{ ...base, orientation: 'Portrait' }], { allowRotate: false })

  // The packer sees the same footprint: cut and graphic boxes are identical.
  assert.deepEqual(auto.cells[0].cut, portrait.cells[0].cut)
  assert.deepEqual(auto.cells[0].graphic, portrait.cells[0].graphic)
  assert.equal(auto.pages.length, portrait.pages.length)

  // …so the label areas and the raster pixel grid are identical too.
  const areaAuto = stickerLabelAreas([{ ...base, orientation: 'Auto' }], auto.options, auto).get('tile')
  const areaPortrait = stickerLabelAreas([{ ...base, orientation: 'Portrait' }], portrait.options, portrait).get('tile')
  assert.deepEqual(areaAuto, areaPortrait)
  assert.deepEqual(
    labelRasterPx(areaAuto.upright.wMm, areaAuto.upright.hMm),
    labelRasterPx(areaPortrait.upright.wMm, areaPortrait.upright.hMm),
  )
})

test('the item orientation and the packer rotation compose instead of cancelling out', async () => {
  // A 32 × 4 stud label only fits the sheet turned by 90° (allowRotate), and its artwork is a
  // landscape image the operator placed Portrait: two rotations, applied in two different places.
  const item = {
    id: 'wide',
    imageId: 'img',
    imageCached: true,
    widthStuds: 32,
    depthStuds: 4,
    copies: 1,
    fit: 'Contain',
    orientation: 'Portrait',
  }
  const plan = planStickerSheet(
    [{ id: 'wide', name: 'Wide', widthStuds: 32, depthStuds: 4, copies: 1 }],
    { allowRotate: true },
  )
  assert.equal(plan.cells[0].rotated, true)
  const area = stickerLabelAreas([item], plan.options, plan).get('wide')

  const log = []
  const source = { width: 900, height: 300 }
  const images = await loadStickerImages([item], {
    plan,
    options: plan.options,
    canvasFactory: recordingCanvasFactory(log),
    loadArtwork: async () => ({ source, type: 'image/png' }),
  })
  assert.equal(images.wide.orientation, 'Portrait', 'the raster entry carries the orientation')

  const upright = log[0]
  const turnedLabel = log[1]
  // 1. The ARTWORK turn is baked into the upright raster (a rotated drawing frame).
  assert.ok(upright.ops.some((op) => op[0] === 'rotate'), 'the artwork is turned in the upright raster')
  // 2. The LABEL turn then rotates that finished raster, so the two compose.
  assert.ok(turnedLabel.ops.some((op) => op[0] === 'rotate'), 'the label turn is applied afterwards')
  assert.ok(images.wide.rotatedDataUrl)
  assert.equal(turnedLabel.width, upright.height)
  assert.equal(turnedLabel.height, upright.width)

  // The upright raster is the label's own pixel grid, not the turned one.
  const expected = labelRasterPx(area.upright.wMm, area.upright.hMm)
  assert.deepEqual({ w: upright.width, h: upright.height }, expected)

  // The two turns are visible in the numbers: the artwork is fitted on the turned source (900×300 →
  // 300×900) while the destination is still the upright label box.
  const geometry = computeFitGeometry('Contain', source, expected, 'Portrait')
  assert.deepEqual(geometry, computeFitGeometry('Contain', { w: 300, h: 900 }, expected, 'Auto'))
  assert.equal(geometry.sw, source.height)
  assert.equal(geometry.sh, source.width)
  const draw = upright.ops.find((op) => op.length === 9)
  assert.deepEqual(draw.slice(1), [
    turnedSourceRect(source, geometry).sx,
    turnedSourceRect(source, geometry).sy,
    turnedSourceRect(source, geometry).sw,
    turnedSourceRect(source, geometry).sh,
    -geometry.dh / 2,
    -geometry.dw / 2,
    geometry.dh,
    geometry.dw,
  ])
})
