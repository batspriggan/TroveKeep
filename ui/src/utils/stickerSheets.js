// Sticker-sheet geometry: how many tile-sized labels fit on an A4 sheet and where each
// one sits. Pure functions, no DOM and no jsPDF, so the packing maths is unit-testable
// (see ui/tests/stickerSheets.test.mjs) and the PDF renderer only draws what this returns.
//
// The packing is a 2D bottom-left skyline: the free edge of the used area is a list of
// horizontal segments `{ x, w, y }`, and every label goes where that edge is lowest (then
// leftmost). No shelves: the space under a short label is reused by the next one.
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
 * Deterministic packing order: deepest first (the tall labels open the skyline, so the
 * low edge stays flat), then widest, then by name and finally by id so two runs on the
 * same library always produce the same sheet byte for byte.
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

/** The skyline of a still empty page: one segment covering the whole content width. */
function emptySkyline(content) {
  return [{ x: content.x, w: content.w, y: content.y }]
}

/** Highest skyline over `[x, x + w)`: the lowest `y` a box of that width can rest at. */
function skylineHeightAt(segments, x, w) {
  const end = x + w
  let y = -Infinity
  for (const seg of segments) {
    if (seg.x + seg.w <= x + EPS || seg.x >= end - EPS) continue
    if (seg.y > y) y = seg.y
  }
  return y
}

/**
 * Bottom-left placement of one box (`w` × `h`) on the skyline: among the segment starts that
 * keep the box inside the page, the one with the lowest resulting skyline — leftmost wins a
 * tie. Returns `null` when no candidate fits inside the printable area.
 *
 * The height is read over the **whole foot print including the gap** (`[x, x + w + step)`),
 * not only over the box: a label placed just left of an already placed one would otherwise
 * reserve a band that overlaps its neighbour (breaking the cut gap) and could even *lower*
 * the skyline under it. Reading the full band keeps the skyline monotone, so a neighbour in
 * the gap band means "go above it" — one axis of clearance is enough.
 */
function findPlacement(segments, w, h, content, bottom, step) {
  const right = content.x + content.w
  let best = null
  for (const seg of segments) {
    const x = seg.x
    if (x + w > right + EPS) continue
    const bandEnd = Math.min(x + w + step, right)
    const y = skylineHeightAt(segments, x, bandEnd - x)
    if (!Number.isFinite(y) || y + h > bottom + EPS) continue
    if (!best || y < best.y - EPS || (Math.abs(y - best.y) <= EPS && x < best.x)) best = { x, y }
  }
  return best
}

/**
 * Raises the skyline over `[x, xEnd)` to `y`, splitting the segments it crosses. Adjacent
 * segments that end up at the same height are merged again, so the list never grows without
 * a matching plateau.
 */
function reserveSkyline(segments, x, xEnd, y) {
  const kept = []
  for (const seg of segments) {
    const segEnd = seg.x + seg.w
    if (segEnd <= x + EPS || seg.x >= xEnd - EPS) {
      kept.push({ ...seg })
      continue
    }
    if (seg.x < x - EPS) kept.push({ x: seg.x, w: x - seg.x, y: seg.y })
    if (segEnd > xEnd + EPS) kept.push({ x: xEnd, w: segEnd - xEnd, y: seg.y })
  }
  kept.push({ x, w: xEnd - x, y })
  kept.sort((a, b) => a.x - b.x)

  const merged = []
  for (const seg of kept) {
    const last = merged[merged.length - 1]
    if (last && Math.abs(last.y - seg.y) <= EPS && Math.abs(last.x + last.w - seg.x) <= EPS) {
      last.w += seg.w
    } else {
      merged.push({ ...seg })
    }
  }
  return merged
}

/**
 * Lays the requested copies out on as many A4 pages as needed.
 *
 * `items` is the selection: `[{ id, name, widthStuds, depthStuds, copies }]`. Copies of a
 * sticker whose artwork is missing are the caller's business — the plan only places what
 * it is given.
 *
 * Returns `{ options, page, content, step, cells, pages, count, perSticker }` where every
 * cell is `{ stickerId, name, copy, page, x, y, w, h, rotated, cut, graphic }`:
 *   cut     — the box to cut around (this is the label itself, `growMm` included)
 *   graphic — the area the artwork may use: `cut` inset by `borderMm + paddingMm`
 * and every page is `{ index, cells }`: the cells of that page, in the order they were placed.
 * There are no rows (shelves): a page is the flat list of its labels.
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

  /** Page currently being filled, or `null` before the first one is opened. */
  let page = null
  /** Free edge of that page, as a left-to-right list of `{ x, w, y }` plateaus. */
  let skyline = []

  // A page is only published once a label lands on it, so a plan never reports an empty
  // sheet (and an unfit label cannot leave one behind).
  const openPage = () => {
    page = { index: pages.length, cells: [] }
    skyline = emptySkyline(content)
  }

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
    if (!pages.includes(page)) pages.push(page)
    page.cells.push(cell)
    cells.push(cell)
    return cell
  }

  /** Upright first, rotated only when the upright variant has nowhere to go. */
  const placeOnPage = (variants) => {
    for (const variant of variants) {
      const at = findPlacement(skyline, variant.w, variant.h, content, bottom, step)
      if (at) return { variant, ...at }
    }
    return null
  }

  for (const unit of units) {
    const variants = variantsOf(unit, options)

    // 1. Bottom-left corner of the free area on the current page.
    let at = page ? placeOnPage(variants) : null

    // 2. No room left on this page: open a fresh one and retry there. An empty page that
    //    still cannot hold the label is the end of the line (an empty page is not opened).
    if (!at && (!page || page.cells.length > 0)) {
      openPage()
      at = placeOnPage(variants)
    }

    // 3. Not even an empty page holds it in any orientation: refuse the whole sheet.
    if (!at) {
      unfit.push(unit.name)
      continue
    }

    makeCell(unit, at.variant, at.x, at.y)
    // The gap belongs to the placed label: a neighbour at `x + w + step` (or `y + h + step`)
    // keeps `step` between the two cut lines.
    skyline = reserveSkyline(
      skyline,
      at.x,
      Math.min(at.x + at.variant.w + step, right),
      at.y + at.variant.h + step,
    )
  }

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
    pages: pages.map((p) => ({ index: p.index, cells: p.cells })),
    cells,
    count: cells.length,
    perSticker,
  }
}
