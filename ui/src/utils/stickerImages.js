// Rasterises sticker artwork for the PDF engine.
//
// The browser needs `fetch`, an `<img>`/`ImageBitmap` and a `<canvas>` to do that, none of which
// exist in the Node test runner — so every browser dependency is injectable:
//   * `loadArtwork` fetches and decodes one item into a bitmap,
//   * `canvasFactory(width, height)` creates the canvas the raster is drawn on.
// The tests inject both and then assert the *geometry* decisions (source and destination
// rectangles) instead of pixels (see ui/tests/stickerFits.test.mjs).
//
// Why resample at all instead of letting jsPDF embed the uploaded file: `doc.addImage` cannot
// crop, so the fit (`Contain` / `Cover` / `Fill` / `Center`) has to be baked into the raster.
// The raster is therefore drawn exactly into the label's `graphic` rectangle — the numbers the
// PDF then uses — at a pinned 600 dpi, which is what a 16 mm tile label needs to look crisp
// while a huge upload does not bloat the PDF.
import { resolveOptions, stickerGraphicMm } from './stickerSheets.js'

/** Target print resolution of the artwork. */
export const STICKER_DPI = 600

/** Hard cap on the raster's long side: a 24×32 stud label at 600 dpi would be ~6000 px. */
export const MAX_RASTER_PX = 4000

/**
 * How the artwork fills the label's graphic rectangle. The values are the backend enum names
 * (`StickerFit`), serialised as names, so they travel unchanged through the API.
 */
export const FIT_OPTIONS = [
  {
    value: 'Contain',
    label: 'Fit inside',
    hint: 'Whole artwork visible, letterboxed — nothing is cut off',
  },
  {
    value: 'Cover',
    label: 'Fill and crop',
    hint: 'Scaled up to fill the label, the overflowing side is cropped',
  },
  {
    value: 'Fill',
    label: 'Stretch',
    hint: 'Distorted to the label, so it always covers it completely',
  },
  {
    value: 'Center',
    label: 'Actual size',
    hint: 'Pixel size at the print resolution, centred, cropped when larger than the label',
  },
]

/** The backend default; also what an unknown stored value falls back to. */
export const DEFAULT_FIT = 'Contain'

const FIT_VALUES = FIT_OPTIONS.map((option) => option.value)

/** True for one of the four backend fit names. */
export function isStickerFit(value) {
  return FIT_VALUES.includes(value)
}

/** The fit to use: the given one when it is known, `Contain` otherwise. */
export function normalizeFit(value) {
  return isStickerFit(value) ? value : DEFAULT_FIT
}

/** Human label of a fit ("Contain" → "Fit inside"), for the read-only views. */
export function fitLabel(value) {
  return FIT_OPTIONS.find((option) => option.value === normalizeFit(value))?.label ?? DEFAULT_FIT
}

/**
 * How the ARTWORK is turned inside the label. The values are the backend enum names
 * (`StickerOrientation`), serialised as names, so they travel unchanged through the API.
 *
 * This is the per-item orientation and has nothing to do with the sheet option `allowRotate`:
 * that one lets the packer turn a whole label by 90° when it is the only way it fits the page.
 * The two rotations are independent and compose (see `drawStickerRaster`).
 */
export const ORIENTATION_OPTIONS = [
  {
    value: 'Auto',
    label: 'As uploaded',
    hint: 'Keep the artwork as it is — the raster’s own orientation',
  },
  {
    value: 'Landscape',
    label: 'Horizontal',
    hint: 'Turn the artwork to lie horizontally (wider than tall)',
  },
  {
    value: 'Portrait',
    label: 'Vertical',
    hint: 'Turn the artwork to stand vertically (taller than wide)',
  },
]

/** The backend default; also what an unknown stored value falls back to. */
export const DEFAULT_ORIENTATION = 'Auto'

const ORIENTATION_VALUES = ORIENTATION_OPTIONS.map((option) => option.value)

/** True for one of the three backend orientation names. */
export function isStickerOrientation(value) {
  return ORIENTATION_VALUES.includes(value)
}

/** The orientation to use: the given one when it is known, `Auto` otherwise. */
export function normalizeOrientation(value) {
  return isStickerOrientation(value) ? value : DEFAULT_ORIENTATION
}

/** Human label of an orientation ("Portrait" → "Vertical"), for the read-only views. */
export function orientationLabel(value) {
  return (
    ORIENTATION_OPTIONS.find((option) => option.value === normalizeOrientation(value))?.label ??
    DEFAULT_ORIENTATION
  )
}

/**
 * URL of an artwork file. Accepts a library row (`id`) or a project item (`imageId`); the
 * `updatedAt` cache buster applies when the caller has it, so a replaced file is re-fetched.
 */
export function stickerArtworkUrl({ id, imageId, updatedAt } = {}) {
  const key = imageId ?? id
  const version = updatedAt ? `?v=${encodeURIComponent(updatedAt)}` : ''
  return `/api/sticker-images/${key}/image${version}`
}

/** Millimetres to raster pixels at {@link STICKER_DPI}, capped at {@link MAX_RASTER_PX}. */
export function labelRasterPx(widthMm, heightMm) {
  let w = Math.max(1, Math.round((Math.max(0, widthMm) / 25.4) * STICKER_DPI))
  let h = Math.max(1, Math.round((Math.max(0, heightMm) / 25.4) * STICKER_DPI))
  const longest = Math.max(w, h)
  if (longest > MAX_RASTER_PX) {
    const scale = MAX_RASTER_PX / longest
    w = Math.max(1, Math.round(w * scale))
    h = Math.max(1, Math.round(h * scale))
  }
  return { w, h }
}

/** Natural pixel size of a bitmap or an `<img>` (also accepts a plain `{ w, h }`). */
export function sourcePixelSize(source) {
  const w = Number(source?.w ?? source?.naturalWidth ?? source?.width ?? 0)
  const h = Number(source?.h ?? source?.naturalHeight ?? source?.height ?? 0)
  return { w: Math.max(1, Math.round(w) || 1), h: Math.max(1, Math.round(h) || 1) }
}

/**
 * The artwork's size AFTER the item's orientation is applied — the source the fit is computed
 * against. This is an explicit step so the order is visible: <b>rotate first, then fit</b>.
 *
 * `Auto` means "as uploaded", not "never turned": it keeps the raster's own orientation.
 * `Landscape`/`Portrait` turn the artwork 90° only when it disagrees with the raster's natural
 * orientation (a square artwork has none, so it is never turned). The label itself never changes
 * size — only the artwork inside it is turned.
 *
 * Returns `{ w, h, turned }`: the effective size plus whether a 90° turn has to be applied by
 * the drawing step (`drawStickerRaster`).
 */
export function resolveEffectiveSource(source, orientation = DEFAULT_ORIENTATION) {
  const natural = sourcePixelSize(source)
  const wanted = normalizeOrientation(orientation)
  const naturalIsLandscape = natural.w > natural.h
  const naturalIsPortrait = natural.h > natural.w
  const turned =
    (wanted === 'Landscape' && naturalIsPortrait) || (wanted === 'Portrait' && naturalIsLandscape)

  return turned
    ? { w: natural.h, h: natural.w, turned: true }
    : { w: natural.w, h: natural.h, turned: false }
}

/**
 * Geometry of one artwork inside the label area, as the two rectangles `ctx.drawImage` takes:
 * `{ sx, sy, sw, sh }` in source pixels and `{ dx, dy, dw, dh }` in destination pixels.
 *
 * `destination` is the label's graphic rectangle in raster pixels, `source` the artwork's
 * natural size. `orientation` is the item's artwork orientation: the effective source is resolved
 * FIRST (`resolveEffectiveSource`, i.e. the artwork is turned) and the fit is computed afterwards
 * against the turned axes. Pure arithmetic, so all four fits are pinned by unit tests:
 *
 *   Fill    — destination fully covered, source fully used (the aspect ratio may change)
 *   Contain — source fully used, destination letterboxed (the whole artwork, never cropped)
 *   Cover   — destination fully covered, source cropped on the overflowing axis
 *   Center  — 1:1 pixels (the artwork prints at its own resolution), centred, cropped when larger
 *
 * The returned source rectangle is in the EFFECTIVE (turned) source frame; `drawStickerRaster`
 * maps it back onto the original bitmap when it applies the turn.
 */
export function computeFitGeometry(fit, source, destination, orientation = DEFAULT_ORIENTATION) {
  // Rotate first, then fit: the turned axes are what the four fits see.
  const src = resolveEffectiveSource(source, orientation)
  const dst = {
    w: Math.max(1, Math.round(destination?.w ?? 0)),
    h: Math.max(1, Math.round(destination?.h ?? 0)),
  }

  switch (normalizeFit(fit)) {
    case 'Fill':
      return { sx: 0, sy: 0, sw: src.w, sh: src.h, dx: 0, dy: 0, dw: dst.w, dh: dst.h }

    case 'Contain': {
      const scale = Math.min(dst.w / src.w, dst.h / src.h)
      const dw = src.w * scale
      const dh = src.h * scale
      return { sx: 0, sy: 0, sw: src.w, sh: src.h, dx: (dst.w - dw) / 2, dy: (dst.h - dh) / 2, dw, dh }
    }

    case 'Cover': {
      // The artwork overflows the label on one axis; the source rectangle is the visible part.
      const scale = Math.max(dst.w / src.w, dst.h / src.h)
      const sw = Math.min(src.w, dst.w / scale)
      const sh = Math.min(src.h, dst.h / scale)
      return { sx: (src.w - sw) / 2, sy: (src.h - sh) / 2, sw, sh, dx: 0, dy: 0, dw: dst.w, dh: dst.h }
    }

    // Center: one source pixel is one raster pixel, so the artwork keeps its own resolution.
    default: {
      const dw = Math.min(src.w, dst.w)
      const dh = Math.min(src.h, dst.h)
      return {
        sx: (src.w - dw) / 2,
        sy: (src.h - dh) / 2,
        sw: dw,
        sh: dh,
        dx: (dst.w - dw) / 2,
        dy: (dst.h - dh) / 2,
        dw,
        dh,
      }
    }
  }
}

/** The browser canvas factory: overridden in tests (which have no `document`). */
export function browserCanvasFactory(width, height) {
  const canvas = document.createElement('canvas')
  canvas.width = width
  canvas.height = height
  return canvas
}

/**
 * Label areas of the given items, in millimetres, keyed by item id:
 * `{ upright: { wMm, hMm }, rotated: { wMm, hMm } | null }`.
 *
 * The upright area is always present. The turned one is produced only when the sheet actually
 * uses it — the plan's cells say so — or, without a plan, for every non-square label. Both come
 * from `stickerGraphicMm`, i.e. from the same numbers the packer puts in `cell.graphic`.
 */
export function stickerLabelAreas(items, options = {}, plan = null) {
  const resolved = resolveOptions(options)
  const rotatedIds = new Set()
  for (const cell of plan?.cells ?? []) {
    if (cell.rotated) rotatedIds.add(String(cell.stickerId))
  }

  const areas = new Map()
  for (const item of items ?? []) {
    const key = String(item.id)
    const { graphic } = stickerGraphicMm(item, resolved)
    const square = Math.abs(graphic.w - graphic.h) < 1e-9
    const needsRotated = !square && (plan ? rotatedIds.has(key) : true)
    areas.set(key, {
      upright: { wMm: graphic.w, hMm: graphic.h },
      // A turned label is the upright box turned by 90°: the numbers are swapped, not recomputed
      // differently, so a rotated label crops for the rotated rectangle.
      rotated: needsRotated ? { wMm: graphic.h, hMm: graphic.w } : null,
    })
  }
  return areas
}

/**
 * The source rectangle of the ORIGINAL bitmap that corresponds to a geometry computed in the
 * turned (effective) frame. A 90° clockwise turn maps the original pixel `(x, y)` to the effective
 * `(height - y, x)`, so the effective rectangle `(sx, sy, sw, sh)` comes back as the original
 * rectangle `(sy, height - sx - sw, sh, sw)`.
 */
export function turnedSourceRect(source, geometry) {
  const natural = sourcePixelSize(source)
  return {
    sx: geometry.sy,
    sy: natural.h - geometry.sx - geometry.sw,
    sw: geometry.sh,
    sh: geometry.sw,
  }
}

/**
 * Draws the artwork of one item into a fresh raster of `size` pixels.
 *
 * `background` (white for JPEG, which has no alpha; `null` to keep transparency) is painted
 * first, so a `Contain` letterbox is white for a JPEG and transparent for a PNG.
 *
 * Two rotations exist in this feature and they are applied in two different places:
 *   * the item's `orientation` turns the ARTWORK inside the label — here, by rotating the drawing
 *     frame around the centre of the label rectangle;
 *   * the packer's `allowRotate` turns the WHOLE LABEL — afterwards, by `rotateStickerRaster` on
 *     the finished upright raster (see `loadStickerImages`).
 * Because they act on different things they compose: a `Portrait` artwork on a label the packer
 * turned comes out turned twice, not cancelled out.
 */
export function drawStickerRaster(
  { fit, source, size, background = null, orientation = DEFAULT_ORIENTATION },
  { canvasFactory = browserCanvasFactory } = {},
) {
  const canvas = canvasFactory(size.w, size.h)
  const ctx = canvas.getContext('2d')
  if (background) {
    ctx.fillStyle = background
    ctx.fillRect(0, 0, size.w, size.h)
  }

  const geometry = computeFitGeometry(fit, source, size, orientation)
  const { turned } = resolveEffectiveSource(source, orientation)

  if (!turned) {
    // No artwork turn: exactly the rectangle the fit computed, in one call.
    ctx.drawImage(source, geometry.sx, geometry.sy, geometry.sw, geometry.sh,
      geometry.dx, geometry.dy, geometry.dw, geometry.dh)
    return canvas
  }

  // Artwork turn: the fit was computed in the turned frame, so the frame is rotated 90° clockwise
  // around the centre of the label rectangle and the effective source rectangle is mapped back
  // onto the original bitmap. After the turn the local width runs down the canvas and the local
  // height across it, hence the swapped destination numbers below; the canvas rectangle stays the
  // label's own `dw × dh`, so the label itself never changes size.
  const rect = turnedSourceRect(source, geometry)
  ctx.translate(geometry.dx + geometry.dw / 2, geometry.dy + geometry.dh / 2)
  ctx.rotate(Math.PI / 2)
  ctx.drawImage(source, rect.sx, rect.sy, rect.sw, rect.sh,
    -geometry.dh / 2, -geometry.dw / 2, geometry.dh, geometry.dw)
  return canvas
}

/**
 * Turns a raster by 90° clockwise. A label the packer rotated is placed in a box whose sides are
 * the upright ones swapped, so the turned raster is the upright raster rotated — the crop baked
 * into it then matches the box the PDF draws into.
 */
export function rotateStickerRaster(canvas, { canvasFactory = browserCanvasFactory } = {}) {
  const out = canvasFactory(canvas.height, canvas.width)
  const ctx = out.getContext('2d')
  ctx.translate(out.width / 2, out.height / 2)
  ctx.rotate(Math.PI / 2)
  ctx.drawImage(canvas, -canvas.width / 2, -canvas.height / 2)
  return out
}

/** Serialises a raster for jsPDF, keeping PNG transparency but flattening everything else. */
function toDataUrl(canvas, format) {
  return format === 'PNG'
    ? canvas.toDataURL('image/png')
    : canvas.toDataURL('image/jpeg', 0.95)
}

/** Fetches and decodes one artwork into a bitmap (browser only). */
export async function fetchStickerArtwork(item, signal) {
  const response = await fetch(stickerArtworkUrl(item), { credentials: 'include', signal })
  if (!response.ok) throw new Error(`Sticker artwork ${item.imageId ?? item.id}: HTTP ${response.status}`)
  const blob = await response.blob()
  // `createImageBitmap` is the cheap path; the object-URL fallback keeps older browsers
  // working (Safari < 15).
  if (typeof createImageBitmap === 'function') {
    return { source: await createImageBitmap(blob), type: blob.type }
  }
  const url = URL.createObjectURL(blob)
  try {
    const image = await new Promise((resolve, reject) => {
      const img = new Image()
      img.onload = () => resolve(img)
      img.onerror = () => reject(new Error(`Sticker artwork ${item.imageId ?? item.id} is not decodable`))
      img.src = url
    })
    return { source: image, type: blob.type }
  } finally {
    URL.revokeObjectURL(url)
  }
}

/**
 * Loads every item's artwork as PDF-ready data URLs keyed by item id:
 * `{ dataUrl, format, rotatedDataUrl?, rotatedFormat? }`.
 *
 * The key is the *item*, not the image: two items may print the same artwork with a different
 * footprint, fit or artwork orientation and therefore need a different raster (the item id is the
 * key, so an item differing only by `orientation` never shares a raster; the orientation is also
 * carried in the entry). jsPDF still aliases repeated copies of one item (the PDF alias is
 * `itemId` / `itemId-r`), so 400 copies of one label embed the image once. A PNG keeps its
 * transparency (the label sheet is transparent polyethylene); anything else is flattened onto
 * white and exported as a quality-0.95 JPEG.
 *
 * `plan` is optional but recommended: it is where the raster sizes come from, so a rotated label
 * gets a raster for the rotated rectangle.
 */
export async function loadStickerImages(
  items,
  {
    plan = null,
    options = {},
    signal,
    onProgress,
    canvasFactory = browserCanvasFactory,
    loadArtwork = fetchStickerArtwork,
  } = {},
) {
  const list = items ?? []
  const areas = stickerLabelAreas(list, options, plan)
  const images = {}
  let done = 0

  for (const item of list) {
    try {
      const { source, type } = await loadArtwork(item, signal)
      const isPng = (type || '').toLowerCase().includes('png')
      const format = isPng ? 'PNG' : 'JPEG'
      const area = areas.get(String(item.id))
      const size = labelRasterPx(area.upright.wMm, area.upright.hMm)
      // `orientation` turns the artwork inside the label; the packer's turn is applied to the
      // finished raster below, so the two rotations compose instead of cancelling out.
      const orientation = normalizeOrientation(item.orientation)
      const canvas = drawStickerRaster(
        { fit: normalizeFit(item.fit), source, size, background: isPng ? null : '#ffffff', orientation },
        { canvasFactory },
      )
      const entry = { dataUrl: toDataUrl(canvas, format), format, orientation }
      if (area.rotated) {
        const turned = rotateStickerRaster(canvas, { canvasFactory })
        entry.rotatedDataUrl = toDataUrl(turned, format)
        entry.rotatedFormat = format
      }
      images[String(item.id)] = entry
      source.close?.()
    } catch (error) {
      if (signal?.aborted) throw error
      // An item whose artwork cannot be fetched is reported, not fatal: the sheet is still
      // generated and that label simply comes out as an empty cut box.
      images[String(item.id)] = null
    }
    done++
    onProgress?.({ done, total: list.length, item })
  }
  return images
}
