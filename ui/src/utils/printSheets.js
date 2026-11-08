// Print-sheet geometry shared by the two planner PDF exporters (`roomPdf.js`,
// `platePdf.js`): multi-sheet printing and the baseplate placeholder grid a planner
// fills in by hand.
//
// Multi-sheet printing is real page size, not the printer driver's "poster mode" and not
// "shrink one A4 and chop it": a 2×2 export renders the drawing on a sheet as big as four
// A4 pages, then cuts that sheet into four A4 pages. Each page prints 1:1 and they simply
// line up when glued together.

/** A4 in millimetres. */
export const PAGE = { width: 210, height: 297 }

/** Scale factors offered by the UI. */
export const TILE_OPTIONS = [1, 2, 3]

/** Margin used for a given tile count — see `planSheets`. */
export function marginForTile(tile) {
  return tile > 1 ? 10 : 15
}

/**
 * Sheet layout for a diagram of (widthMm × heightMm) printed at factor `tile`.
 *
 * A4-ish pages: the longer diagram side follows the longer page side, so a landscape
 * diagram still tiles with the pages and not across the grain. `sheetW`/`sheetH` is the
 * big sheet in PDF mm (A4 × tile); `viewport` is what a single printed page shows.
 *
 * `grid` is filled in by `sheetGrid` once the drawing is sized — until then, every page
 * of the grid is assumed to be used.
 */
export function planSheets(widthMm, heightMm, tile = 1, margin = 15) {
  const landscape = widthMm > heightMm
  const pageW = landscape ? PAGE.height : PAGE.width
  const pageH = landscape ? PAGE.width : PAGE.height
  const sheetW = pageW * tile
  const sheetH = pageH * tile
  return {
    landscape,
    tile,
    pageW,
    pageH,
    margin,
    sheetW,
    sheetH,
    viewport: { x: margin, y: margin, w: pageW - margin * 2, h: pageH - margin * 2 },
    // Content box of a single sheet, for what is drawn on the page itself (header,
    // summary): plain page coordinates.
    contentX: margin,
    contentY: margin,
    contentW: pageW - margin * 2,
    contentH: pageH - margin * 2,
    grid: {
      cols: tile,
      rows: tile,
      pages: tile,
      content: { x: 0, y: 0, w: sheetW, h: sheetH },
    },
  }
}

/**
 * Which pages of the export really carry the drawing, and where it sits on the big sheet.
 *
 * `content` is the box the drawing fills, in sheet millimetres. A page is printed only
 * when it is really filled: below HALF a page, the last page row or column is dropped.
 * A drawing that merely pokes into the next page would otherwise produce a sheet of
 * which most is blank — dropping it keeps every sheet full and lets the drawing print
 * larger.
 */
export function sheetGrid(plan, content) {
  const vp = plan.viewport
  const inside = {
    x: Math.max(content.x, plan.contentX),
    y: Math.max(content.y, plan.contentY),
    w: Math.max(0, Math.min(content.x + content.w, plan.sheetW - plan.contentX) - Math.max(content.x, plan.contentX)),
    h: Math.max(0, Math.min(content.y + content.h, plan.sheetH - plan.contentY) - Math.max(content.y, plan.contentY)),
  }
  if (inside.w <= 0 || inside.h <= 0) return { cols: 1, rows: 1, pages: 1, content: inside }

  // Cell index of the page the far edge of the drawing falls into; the tiny epsilon
  // absorbs rounding so an exact page boundary does not add a page.
  const span = (origin, size, step) => Math.max(1, Math.min(plan.tile, Math.ceil((origin + size - vp.x) / step - 0.002)))
  const cols = span(inside.x, inside.w, vp.w)
  const rows = span(inside.y, inside.h, vp.h)

  // A whole slice is only worth a sheet when the drawing covers most of it: under HALF
  // the free length of the last row/column is clipped, and clipping there would leave a
  // mostly blank sheet. Clipping along the other axis is harmless — that page is full.
  //
  // A drawing too small to fill even one page is a different case: shrinking the export
  // would not help (one page cannot be made fuller), so the limit is relaxed to a quarter.
  const threshold = (used, step, size) =>
    used > 1
      ? step / 2
      : Math.min(step / 2, Math.max(0, (used * step - size) / 4))
  const keep = (origin, size, step, used) =>
    used <= 1 || origin + size >= vp.x + (used - 1) * step + threshold(used, step, size)
  return {
    cols: keep(inside.x, inside.w, vp.w, cols) ? cols : cols - 1,
    rows: keep(inside.y, inside.h, vp.h, rows) ? rows : rows - 1,
    pages: plan.tile,
    content: inside,
  }
}

/** Slots of the pages to print, in reading order (row by row, left to right). */
export function gridSlots(grid) {
  const slots = []
  for (let row = 0; row < grid.rows; row++) {
    for (let col = 0; col < grid.cols; col++) slots.push(row * grid.pages + col)
  }
  return slots
}

/**
 * Where page `slot` of the big sheet is cut. `rect` is the slice of the sheet it shows,
 * in sheet millimetres from its top-left corner; `col`/`row` are 0-based page indices.
 */
export function sheetClip(plan, slot) {
  const col = slot % plan.grid.pages
  const row = Math.floor(slot / plan.grid.pages)
  const vp = plan.viewport
  return {
    rect: { x: vp.x + col * vp.w, y: vp.y + row * vp.h, w: vp.w, h: vp.h },
    col,
    row,
  }
}

/**
 * Moves the origin of the current page by (dx, dy), in sheet millimetres.
 *
 * jsPDF's `setCurrentTransformationMatrix` joins the matrix with commas, which is not
 * PDF syntax; the operator needs spaces, so the raw write is the reliable path. Units
 * are PDF user units (millimetres × scaleFactor), like every other coordinate.
 */
function shiftSheet(doc, dx, dy) {
  const k = doc.internal.scaleFactor
  doc.internal.write(`1 0 0 1 ${dx * k} ${-dy * k} cm`)
}

/**
 * Turns a drawing made on the big sheet into the A4 pages it needs: each page shifts
 * the sheet so the slice it owns lands under its viewport, then clips to the viewport,
 * so nothing from the neighbouring slices bleeds across the cut.
 *
 * `draw()` draws the *whole* sheet in sheet coordinates — the translation does the
 * cutting. `drawFrame()` draws what belongs to one page only (header, summary, footer)
 * in plain page coordinates.
 */
export function tileDiagram(doc, plan, draw, drawFrame) {
  const slots = gridSlots(plan.grid)

  if (slots.length === 1) {
    if (typeof draw === 'function') {
      doc.saveGraphicsState()
      doc.rect(plan.viewport.x, plan.viewport.y, plan.viewport.w, plan.viewport.h, null)
      doc.clip()
      doc.discardPath()
      draw(slots[0])
      doc.restoreGraphicsState()
    }
    drawFrame?.(doc, 0)
    return
  }

  for (let i = 1; i < slots.length; i++) doc.addPage('a4', plan.landscape ? 'landscape' : 'portrait')
  // Page 1 is drawn last: text already on it would otherwise be touched again when the
  // flow returns to it at the end.
  for (let i = slots.length - 1; i >= 0; i--) {
    doc.setPage(i + 1)
    const { rect } = sheetClip(plan, slots[i])
    doc.saveGraphicsState()
    shiftSheet(doc, plan.viewport.x - rect.x, plan.viewport.y - rect.y)
    doc.rect(rect.x, rect.y, rect.w, rect.h, null)
    doc.clip()
    doc.discardPath()
    if (typeof draw === 'function') draw(slots[i])
    doc.restoreGraphicsState()
    drawFrame?.(doc, i)
  }
  doc.setPage(1)
}

// ── Baseplate placeholder grid ────────────────────────────────────────────────
// Standard/Road plates and Custom plates with stud counts that are multiples of 8 are
// studs×8 − 0.2 mm (0.1 mm clearance each side); Custom plates with a non-standard stud
// count are studs×8 − 2 mm. Same rule as `calcPlateCount` in RoomPlannerView and the
// placement canvas, so the printout matches the count the planner shows.
export function plateGeometry(widthStuds, depthStuds, type = 'Standard') {
  const standard = type !== 'Custom' || (widthStuds % 8 === 0 && depthStuds % 8 === 0)
  const gap = standard ? 0.2 : 2
  return { widthMm: widthStuds * 8 - gap, depthMm: depthStuds * 8 - gap }
}

/** Parse the canonical `"16x32"` baseplate key into stud counts, or null. */
export function parseBpKey(bpKey) {
  const m = /^(\d+)x(\d+)$/.exec(bpKey ?? '')
  if (!m) return null
  const widthStuds = Number(m[1])
  const depthStuds = Number(m[2])
  if (!widthStuds || !depthStuds) return null
  return { widthStuds, depthStuds }
}

/**
 * Grid of whole baseplates centred on a rectangular area (origin at 0,0, size in mm).
 *
 * Only plates that fit entirely are laid out — nothing is truncated: a plate that would
 * hang over the edge is simply not there, because in reality it could not rest on the
 * tables either. The grid is centred on the area rather than flush with its top-left
 * corner, so the leftover is split evenly between the two opposite sides.
 *
 * This is a *placeholder* layout, not a packing algorithm: the user decides on paper
 * where the real plates go. The count matches the one the planner's calculator shows.
 *
 * Returns `{ widthMm, depthMm, tiles: [{ xMm, yMm, widthMm, depthMm }] }`.
 */
export function planPlateGrid(area, plate) {
  const { widthMm: pw, depthMm: pd } = plate
  const empty = { widthMm: 0, depthMm: 0, tiles: [] }
  if (!(pw > 0) || !(pd > 0) || !(area.widthMm > 0) || !(area.depthMm > 0)) return empty
  // A plate wider than the area never fits: nothing to draw.
  if (pw > area.widthMm || pd > area.depthMm) return empty

  const cols = Math.floor(area.widthMm / pw)
  const rows = Math.floor(area.depthMm / pd)
  const offsetX = area.xMm + (area.widthMm - cols * pw) / 2
  const offsetY = area.yMm + (area.depthMm - rows * pd) / 2

  const tiles = []
  for (let r = 0; r < rows; r++) {
    for (let c = 0; c < cols; c++) {
      tiles.push({
        xMm: offsetX + c * pw,
        yMm: offsetY + r * pd,
        widthMm: pw,
        depthMm: pd,
      })
    }
  }
  return { widthMm: area.widthMm, depthMm: area.depthMm, tiles }
}
