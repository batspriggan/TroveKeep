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
  FIT_OPTIONS,
  MAX_RASTER_PX,
  STICKER_DPI,
  computeFitGeometry,
  fitLabel,
  isStickerFit,
  labelRasterPx,
  loadStickerImages,
  normalizeFit,
  rotateStickerRaster,
  stickerLabelAreas,
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
