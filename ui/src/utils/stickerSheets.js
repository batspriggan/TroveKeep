// Sticker-sheet geometry: how many tile-sized labels fit on an A4 sheet and where each
// one sits. Pure functions, no DOM and no jsPDF, so the packing maths is unit-testable
// (see ui/tests/stickerSheets.test.mjs) and the PDF renderer only draws what this returns.
//
// Everything is in millimetres at 1:1 scale: a LEGO stud is 8 mm, so a 2×4 tile is a
// 16 mm × 32 mm label. The sheet is the physical A4 the operator prints on, and "print at
// 100%, do not fit to page" is the only way the label really matches the tile.

/** One LEGO stud in millimetres (the standard the label sizes are derived from). */
export const STUD_MM = 8

/** A4 portrait, in millimetres — the sticky sheets the operator uses. */
export const PAGE = { width: 210, height: 297 }

/**
 * Defaults of the sheet layout:
 *   marginMm  — white border of the sheet, outside every cut (5..20)
 *   borderMm  — thickness of the black cut border drawn around each label (0..3).
 *               The stroke is inset by half of it, so its OUTER edge is the cut line.
 *   paddingMm — the configurable inner border: gap between the black cut border and the
 *               artwork, i.e. how much room the graphics keep from the cut (0..3)
 *   gapMm     — wanted distance between two cut lines (0..10)
 *   growMm    — grows/shrinks the label box on every side (−0.5..1), to compensate a
 *               printer that is a hair off scale
 *   allowRotate — let the engine turn a label 90° when that is the only way it fits
 */
export const DEFAULT_OPTIONS = {
  marginMm: 8,
  borderMm: 0.6,
  paddingMm: 0.6,
  gapMm: 2.5,
  growMm: 0,
  allowRotate: false,
}

/** Accepted range of every numeric option; anything outside is clamped, not rejected. */
const LIMITS = {
  marginMm: [5, 20],
  borderMm: [0, 3],
  paddingMm: [0, 3],
  gapMm: [0, 10],
  growMm: [-0.5, 1],
}

/** Resolution of the numeric fields (10 = one decimal). */
const OPTION_STEP = 10

/** Tolerance for the packing comparisons: mm values are sums of tenths, so this is plenty. */
const EPS = 1e-9

const clamp = (value, [lo, hi]) => Math.min(hi, Math.max(lo, value))

/**
 * Fills in the defaults and clamps every numeric option to its range, rounding to a tenth
 * of a millimetre. Used by the UI (so a bad stored setting cannot produce a broken sheet)
 * and by the packer (so it cannot be called with a negative gap).
 */
export function resolveOptions(overrides = {}) {
  const resolved = { ...DEFAULT_OPTIONS }
  for (const key of Object.keys(LIMITS)) {
    const raw = overrides?.[key]
    if (raw === undefined || raw === null || raw === '' || Number.isNaN(Number(raw))) continue
    const rounded = Math.round(Number(raw) * OPTION_STEP) / OPTION_STEP
    resolved[key] = clamp(rounded, LIMITS[key])
  }
  resolved.allowRotate = Boolean(overrides?.allowRotate)
  return resolved
}

/**
 * Distance between two neighbouring cut boxes: the wanted gap, but never so small that the
 * two black borders would overlap (each label owns borderMm of black outside its artwork).
 */
export function cutSpacing(options) {
  return Math.max(options.gapMm, 2 * options.borderMm)
}

/** Printed size of one sticker, in millimetres, `growMm` included. */
export function stickerSizeMm(sticker, options = DEFAULT_OPTIONS) {
  const grow = options.growMm ?? 0
  return {
    w: sticker.widthStuds * STUD_MM + 2 * grow,
    h: sticker.depthStuds * STUD_MM + 2 * grow,
  }
}

/**
 * Inset that separates the cutting line from the artwork: the black cut border plus the
 * configurable inner border, capped so a tiny label still keeps a drawable area.
 *
 * Single source of truth for the `graphic` rectangle: the packer uses it per cell and
 * `stickerGraphicMm` uses it for the artwork raster, so the rasterised pixels always have the
 * aspect ratio the PDF draws into.
 */
function insetOf(options, widthMm, heightMm) {
  return Math.min(options.borderMm + options.paddingMm, Math.max(0, (Math.min(widthMm, heightMm) - 0.1) / 2))
}

/**
 * The box of one label and the artwork area inside it, in millimetres (`growMm` included).
 *
 * This is what the packer puts in `cell.cut`/`cell.graphic`; exposing it separately lets the
 * raster loader compute the very same rectangle before (or without) a plan, so the loader and
 * the PDF can never disagree by a millimetre.
 */
export function stickerGraphicMm(sticker, options = {}) {
  const opts = resolveOptions(options)
  const size = stickerSizeMm(sticker, opts)
  const inset = insetOf(opts, size.w, size.h)
  return {
    cut: { w: size.w, h: size.h },
    graphic: { w: size.w - 2 * inset, h: size.h - 2 * inset },
    inset,
  }
}

/** Thrown when one or more stickers cannot fit the sheet at all (too big, any orientation). */
export class StickerFitError extends Error {
  constructor(names) {
    const list = [...new Set(names)].sort((a, b) => a.localeCompare(b))
    super(
      list.length === 1
        ? `"${list[0]}" is larger than the printable area of the sheet: reduce its size or the sheet margin.`
        : `These stickers are larger than the printable area of the sheet: ${list.join(', ')}.`,
    )
    this.name = 'StickerFitError'
    /** Names of the stickers that do not fit, sorted and unique. */
    this.stickers = list
  }
}

const byId = (a, b) => String(a.id).localeCompare(String(b.id))

/**
 * Deterministic packing order: deepest first (a shelf is as tall as its first cell, so the
 * tall labels open the rows), then widest, then by name and finally by id so two runs on
 * the same library always produce the same sheet byte for byte.
 */
function sortUnits(a, b) {
  return (
    b.h - a.h ||
    b.w - a.w ||
    String(a.name).localeCompare(String(b.name)) ||
    byId(a, b) ||
    a.copy - b.copy
  )
}

/** The orientation candidates of one copy: upright always, rotated only when allowed. */
function variantsOf(unit, options) {
  const upright = { w: unit.w, h: unit.h, rotated: false }
  if (!options.allowRotate || Math.abs(unit.w - unit.h) < EPS) return [upright]
  // A rotated label is the only one that may be turned: the upright variant is always
  // preferred and rotation is a fallback, never a packing optimisation for its own sake.
  return [upright, { w: unit.h, h: unit.w, rotated: true }]
}

/**
 * Lays the requested copies out on as many A4 pages as needed.
 *
 * `items` is the selection: `[{ id, name, widthStuds, depthStuds, copies }]`. Copies of a
 * sticker whose artwork is missing are the caller's business — the plan only places what
 * it is given.
 *
 * Returns `{ options, content, step, cells, pages, count, perSticker }` where every cell is
 * `{ stickerId, name, copy, page, x, y, w, h, rotated, cut, graphic }`:
 *   cut     — the box to cut around (this is the label itself, `growMm` included)
 *   graphic — the area the artwork may use: `cut` inset by `borderMm + paddingMm`
 *
 * Throws `StickerFitError` naming the stickers that do not fit the sheet in any
 * orientation; the engine never silently drops a copy.
 */
export function planStickerSheet(items, overrides = {}) {
  const options = resolveOptions(overrides)
  const content = {
    x: options.marginMm,
    y: options.marginMm,
    w: PAGE.width - 2 * options.marginMm,
    h: PAGE.height - 2 * options.marginMm,
  }
  const step = cutSpacing(options)
  const right = content.x + content.w
  const bottom = content.y + content.h

  const units = []
  for (const item of items ?? []) {
    const copies = Math.max(0, Math.floor(Number(item.copies) || 0))
    if (copies === 0) continue
    const size = stickerSizeMm(item, options)
    for (let copy = 0; copy < copies; copy++) {
      units.push({
        id: item.id,
        name: item.name ?? '',
        widthStuds: item.widthStuds,
        depthStuds: item.depthStuds,
        copy,
        w: size.w,
        h: size.h,
      })
    }
  }
  units.sort(sortUnits)

  // Artwork is centred in its own cut box: the inset is the black border plus the
  // configurable inner border, capped so a tiny label still has a drawable area.
  const artworkInset = (w, h) => insetOf(options, w, h)

  const pages = []
  const cells = []
  const unfit = []

  let page = { index: 0, rows: [], nextY: content.y }
  let row = null

  const makeCell = (unit, variant, x, y) => {
    const inset = artworkInset(variant.w, variant.h)
    const cell = {
      stickerId: unit.id,
      name: unit.name,
      copy: unit.copy,
      page: page.index,
      x,
      y,
      w: variant.w,
      h: variant.h,
      rotated: variant.rotated,
      cut: { x, y, w: variant.w, h: variant.h },
      graphic: {
        x: x + inset,
        y: y + inset,
        w: variant.w - 2 * inset,
        h: variant.h - 2 * inset,
      },
    }
    cells.push(cell)
    return cell
  }

  const finishRow = () => {
    if (row) page.rows.push(row)
    row = null
  }

  const finishPage = () => {
    finishRow()
    if (page.rows.length) {
      pages.push(page)
      page = { index: page.index + 1, rows: [], nextY: content.y }
    }
  }

  for (const unit of units) {
    const variants = variantsOf(unit, options)

    // 1. Continue the open shelf: the label must fit in the remaining width and be no
    //    taller than the shelf (which is as tall as the cell that opened it).
    if (row) {
      const variant = variants.find(
        (v) => row.nextX + v.w <= right + EPS && v.h <= row.height + EPS,
      )
      if (variant) {
        const cell = makeCell(unit, variant, row.nextX, row.y)
        row.cells.push(cell)
        row.nextX = cell.x + cell.w + step
        continue
      }
    }

    // 2. Open a new shelf (on the current page when it still fits vertically).
    const startShelf = (variants, y) =>
      variants.find((v) => v.w <= content.w + EPS && y + v.h <= bottom + EPS)

    finishRow()
    let variant = startShelf(variants, page.nextY)

    // 3. Shelf does not fit this page: move on to a fresh one.
    if (!variant) {
      finishPage()
      variant = startShelf(variants, page.nextY)
    }

    // 4. Not even an empty page holds it in any orientation: refuse the whole sheet.
    if (!variant) {
      unfit.push(unit.name)
      continue
    }

    const cell = makeCell(unit, variant, content.x, page.nextY)
    row = { y: cell.y, height: cell.h, nextX: cell.x + cell.w + step, cells: [cell] }
    page.nextY = cell.y + cell.h + step
  }

  finishPage()

  if (unfit.length > 0) throw new StickerFitError(unfit)

  const perSticker = []
  const bySticker = new Map()
  for (const cell of cells) {
    const key = String(cell.stickerId)
    if (!bySticker.has(key)) {
      const entry = { id: cell.stickerId, name: cell.name, copies: 0 }
      bySticker.set(key, entry)
      perSticker.push(entry)
    }
    bySticker.get(key).copies++
  }

  return {
    options,
    page: { widthMm: PAGE.width, heightMm: PAGE.height },
    content,
    step,
    pages: pages.map((p) => ({
      index: p.index,
      rows: p.rows.map((r) => ({ y: r.y, height: r.height, cells: r.cells })),
    })),
    cells,
    count: cells.length,
    perSticker,
  }
}
