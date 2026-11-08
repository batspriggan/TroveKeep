import { jsPDF } from 'jspdf'
import { marginForTile, parseBpKey, plateGeometry, planPlateGrid, planSheets, sheetClip, sheetGrid, tileDiagram } from './printSheets.js'

/**
 * room:         { name, widthCm, depthCm }
 * templates:    [{ id, description, widthCm, depthCm, color }]
 * placedTables: [{ instanceId, templateId, xCm, yCm, rotation }]
 * aggregates:   [[instanceId, ...], ...]  — each sub-array is a connected group
 * options:
 *   baseplateGrid — draw a placeholder grid of baseplates over every aggregate that
 *                   has a baseplate selected in the plate calculator. The tiles are
 *                   empty outlines, meant to be filled in by hand on paper.
 *   bpKeyByAggIdx — { [aggregateIndex]: "16x32" } — the calculator's selection, keyed
 *                   by canonical studs (min × max).
 *   bpTypes       — { [canonicalKey]: "Standard" | "Road" | "Custom" } — the plate
 *                   type drives the geometry rule; absent entries are Standard.
 *   tileSize      — 1 | 2 | 3: print the diagram across tileSize² A4 sheets at
 *                   full size instead of shrinking it onto one page.
 *   tableSummary  — 'inline' | 'separate-page' | 'none'. 'inline' shrinks the diagram to
 *                   share page 1 with the legend, 'separate-page' gives the diagram the
 *                   whole sheet and appends the legend on its own sheet.
 */
export function generateRoomPdf(room, templates, placedTables, aggregates = [], options = {}) {
  const templateMap = {}
  for (const t of templates) templateMap[t.id] = t

  const counts = {}
  for (const p of placedTables) counts[p.templateId] = (counts[p.templateId] || 0) + 1

  const tile = options.tileSize ?? 1
  const margin = marginForTile(tile)
  const page = planSheets(room.widthCm * 10, room.depthCm * 10, tile, margin)

  const doc = new jsPDF({
    orientation: page.landscape ? 'landscape' : 'portrait',
    unit: 'mm',
    format: 'a4',
  })

  const contentW = page.contentW
  // On a grid printout the sheet is only the drawing — no room title, no dimension
  // lines, no summary — so the diagram can use the whole page: it is a sheet to write
  // on, not a record of the room.
  const bare = !!options.baseplateGrid
  // A plate-grid sheet is the drawing only: the summary is not an option there.
  const summaryMode = bare ? 'none' : (options.tableSummary ?? 'inline')
  // Pages past the first are diagram only: the header and the table summary are on
  // page 1, and repeating them on every slice would be noise. On a tiled export the
  // diagram owns almost the whole sheet; on a single page it leaves room for the
  // header, and for the summary only when the summary shares the page.
  // The drawing is rendered on the BIG sheet (`page.sheetW × page.sheetH`), so on a
  // tiled export it has the room of all the pages at once. Everything after it (header,
  // summary) lives on page 1 and is measured in A4 page coordinates.
  // Height of the drawing band: on a tiled export it spans the big sheet, and its top
  // matches page 1's box so the pages line up (page 1 carries the header there too).
  const boxY = bare ? margin : margin + 15
  let diagramPageH
  if (tile > 1) diagramPageH = page.sheetH - 2 * boxY
  else if (summaryMode === 'inline') diagramPageH = page.contentH - 30 - 60
  else diagramPageH = page.contentH - (bare ? 0 : 30)
  // Everything below is a cursor on page 1; the tiled renderer resets the
  // graphics state per page, so absolute coordinates always mean page 1.
  let curY = margin

  // ── Header ────────────────────────────────────────────────────────────────────
  // Drawn by the page frame, not part of the big sheet — see `tileDiagram`.
  const drawHeader = () => {
    if (bare) return
    // A page element: it belongs on page 1 whatever the sheet transform was.
    doc.setPage(1)
    doc.setFont('helvetica', 'bold')
    doc.setFontSize(20)
    doc.setTextColor(30, 30, 30)
    doc.text(room.name, margin, margin + 5)
    doc.setFont('helvetica', 'normal')
    doc.setFontSize(10)
    doc.setTextColor(100, 100, 100)
    doc.text(`${formatDim(room.widthCm)} × ${formatDim(room.depthCm)}`, margin, margin + 11)
    if (tile > 1) {
      doc.text(`Full size · ${tile}×${tile} A4 sheets`, margin + contentW, margin + 11, { align: 'right' })
    }
  }

  // ── Layout diagram ─────────────────────────────────────────────────────────────
  // Room for the depth dimension line on the right of the diagram; none on a bare
  // grid sheet, where the drawing has to fill the page.
  const DIM_RIGHT = bare ? 0 : 18
  // On a tiled export the drawing is made on the big sheet: its box is the sheet minus
  // margins, and the header (page 1) takes its 25 mm out of that box too.
  const sheetZone = tile > 1 ? { w: page.sheetW - 2 * margin, h: page.sheetH - 2 * margin } : null
  const boxX = margin
  const boxW = (sheetZone ? sheetZone.w : contentW) - DIM_RIGHT
  // A bare grid sheet is scaled to the tables, not to the room: the empty part of the
  // room is not the subject and would waste most of the page. A normal sheet keeps the
  // room frame, so it is scaled to the room.
  // On a tiled export the drawing is scaled to the pages it needs anyway, and so is a
  // bare grid sheet: in both cases the tables are the content, not the empty room.
  const roomBox = { minX: 0, minY: 0, w: room.widthCm, h: room.depthCm || 1 }
  const content = bare || tile > 1
    ? (tablesBBox(placedTables, templateMap) ?? roomBox)
    : roomBox
  const aspect = content.w / (content.h || 1)
  // Vertical extent available for the drawing. On a tiled export the box starts where
  // page 1's box does and ends at the bottom margin of the last sheet, so the slice on
  // page 1 and the band on the pages below it line up.
  // On a bare single sheet there is no header and no summary: the box is the whole
  // printable height, and the drawing is centred on the page itself.
  const bandTop = boxY
  const bandH = Math.max(40, diagramPageH)
  // A bare sheet carries no header and no summary: the drawing is centred on the whole
  // printable page, single or tiled (on a tiled export the vertical centre falls on the
  // seam between the pages, which is why only the horizontal centring is visible).
  const contentBoxH = bare ? page.contentH : bandH
  const contentBoxY = bare ? margin : bandTop
  const maxDiagH = contentBoxH
  // Fit on BOTH axes, and take the smaller of the two scales: on a bare sheet the box
  // is the whole printable page, and filling the width blindly pushes the drawing past
  // the bottom margin — which is what made it look off-centre.
  let scaleFit = Math.min(boxW / content.w, maxDiagH / (content.h || 1))
  // Tiled: no page may be a sliver of content. The drawing keeps the page counts of its
  // natural fit and is scaled up to fill exactly those pages — within the page limits, so
  // a drawing barely crossing a page boundary fills a whole extra page instead.
  if (tile > 1 && page.viewport.w > 0 && page.viewport.h > 0) {
    const vp = page.viewport
    const cells = (size, step) => Math.max(1, Math.min(tile, Math.ceil(size / step - 0.002)))
    const cols = cells(content.w * scaleFit, vp.w)
    const rows = cells(content.h * scaleFit, vp.h)
    const fill = Math.min((cols * vp.w) / content.w, (rows * vp.h) / content.h)
    const room = Math.min((tile * vp.w) / content.w, (tile * vp.h) / content.h)
    scaleFit = Math.min(room, Math.max(scaleFit, fill))
  }
  const diagW = content.w * scaleFit
  const diagH = content.h * scaleFit

  // Centring is only meaningful on one sheet: with several, the drawing is anchored to
  // the top-left of the big sheet and then PADDED to fill whole pages, otherwise every
  // page below/right of the free guess starts half empty.
  const diagX = tile > 1 ? boxX : boxX + (boxW - diagW) / 2
  const diagY = tile > 1 ? contentBoxY : contentBoxY + (maxDiagH - diagH) / 2
  const scale = scaleFit  // mm per cm — same on both axes, never distorted
  // Origin of the drawing content relative to the diagram box, in cm.
  const originX = content.minX
  const originY = content.minY

  // Plate size for the placeholder grid, resolved once for the whole diagram: the
  // calculator's key is canonical (min × max studs), the same size for every group.
  // No selection → null → no grid (and then no bare mode either).
  const studs = options.baseplateGrid ? parseBpKey(Object.values(options.bpKeyByAggIdx ?? {}).find(Boolean)) : null
  const gridPlate = studs
    ? plateGeometry(studs.widthStuds, studs.depthStuds, options.bpTypes?.[`${studs.widthStuds}x${studs.depthStuds}`] ?? 'Standard')
    : null
  // Instance ids of the tables that carry a grid — drives the greyscale fills and, on
  // a grid printout, the legend swatches.
  const grouped = gridPlate ? aggregatesWithGrid(aggregates, options) : null

  // The whole drawing is made once on the big sheet; each A4 gets its slice of it.
  const drawSheet = () => {
    drawDiagram(doc, {
      room, templateMap, placedTables, aggregates, options, gridPlate, grouped, bare,
      diagX, diagY, diagW, diagH, scale, originX, originY,
    })
  }

  // ── Table summary legend ───────────────────────────────────────────────────────
  // Placement was decided before the diagram was sized (it drives `diagramPageH`):
  // 'none' drops it, 'separate-page' gives it its own sheet so the diagram can use
  // the whole page.
  const usedTemplates = templates
    .filter(t => counts[t.id])
    .sort((a, b) => a.description.localeCompare(b.description))

  // The summary is a page element, so it is drawn by the page frame after the slices of
  // the big sheet are cut — never on the sheet itself, where it would be sliced too.
  const drawSummary = () => {
    if (summaryMode === 'separate-page') {
      doc.addPage('a4', page.landscape ? 'landscape' : 'portrait')
      curY = margin
      doc.setFont('helvetica', 'bold')
      doc.setFontSize(14)
      doc.setTextColor(30, 30, 30)
      doc.text(room.name, margin, curY + 5)
      curY += 12
    } else {
      curY = bracketBottom() + 30 + 6
    }
    drawSummaryTable()
    if (summaryMode === 'separate-page') doc.setPage(1)
  }
  const drawSummaryTable = () => {
  doc.setFont('helvetica', 'bold')
  doc.setFontSize(13)
  doc.setTextColor(30, 30, 30)
  doc.text('Table Summary', margin, curY)
  curY += 7

  const swatchW = 10
  const dimsW = 38
  const qtyW = 18
  const descW = contentW - swatchW - dimsW - qtyW
  const rowH = 7
  const textPad = 2

  // Header row
  doc.setFillColor(234, 236, 242)
  doc.rect(margin, curY, contentW, rowH, 'F')
  doc.setDrawColor(200, 200, 200)
  doc.setLineWidth(0.2)
  doc.rect(margin, curY, contentW, rowH, 'S')
  doc.setFont('helvetica', 'bold')
  doc.setFontSize(8.5)
  doc.setTextColor(70, 70, 70)
  const headerY = curY + rowH / 2
  doc.text('Description', margin + swatchW + textPad, headerY, { baseline: 'middle' })
  doc.text('Dimensions', margin + swatchW + descW + textPad, headerY, { baseline: 'middle' })
  doc.text('Qty', margin + swatchW + descW + dimsW + textPad, headerY, { baseline: 'middle' })
  curY += rowH

  doc.setFont('helvetica', 'normal')
  doc.setFontSize(8.5)
  for (let i = 0; i < usedTemplates.length; i++) {
    const tpl = usedTemplates[i]
    const qty = counts[tpl.id] || 0
    const rowY = curY
    const midY = rowY + rowH / 2

    if (i % 2 === 1) {
      doc.setFillColor(248, 249, 251)
      doc.rect(margin, rowY, contentW, rowH, 'F')
    }

    // The legend follows the diagram: with the grid on, the swatches are the same two
    // neutrals, so the sheet carries no colour that the drawing no longer uses.
    // A template shares a swatch across its instances: it gets the "with plates" grey as
    // soon as one of its placed tables carries a grid.
    const tplHasGrid = grouped
      ? placedTables.some(p => p.templateId === tpl.id && grouped.has(p.instanceId))
      : false
    const swatch = !grouped
      ? Object.values(hexToRgb(tpl.color))
      : (tplHasGrid ? GREY_TABLE_WITH_PLATES : GREY_TABLE_PLAIN)
    doc.setFillColor(...swatch)
    doc.setDrawColor(150, 150, 150)
    doc.setLineWidth(0.2)
    doc.rect(margin, rowY, swatchW, rowH, 'FD')

    doc.setDrawColor(224, 227, 234)
    doc.setLineWidth(0.2)
    doc.line(margin, rowY + rowH, margin + contentW, rowY + rowH)

    doc.setTextColor(40, 40, 40)
    doc.text(
      doc.splitTextToSize(tpl.description, descW - textPad * 2)[0],
      margin + swatchW + textPad, midY, { baseline: 'middle' }
    )
    doc.text(`${tpl.widthCm} × ${tpl.depthCm}`, margin + swatchW + descW + textPad, midY, { baseline: 'middle' })
    doc.text(String(qty), margin + swatchW + descW + dimsW + textPad, midY, { baseline: 'middle' })

    curY += rowH
  }

  doc.setDrawColor(180, 180, 180)
  doc.setLineWidth(0.3)
  doc.rect(margin, curY - rowH * usedTemplates.length - rowH, contentW, rowH * (usedTemplates.length + 1), 'S')
  }

  // Everything below the drawing on page 1, in page coordinates.
  const bracketBottom = () => diagY + diagH

  // Only the pages the drawing actually reaches are emitted: a small layout on a 3×3
  // export must not come back as nine sheets of which most are blank.
  page.grid = sheetGrid(page, {
    x: diagX,
    y: diagY,
    w: diagW,
    h: diagH,
  })

  tileDiagram(doc, page, drawSheet, () => {
    drawHeader()
    if (summaryMode !== 'none' && usedTemplates.length > 0) drawSummary()
  })

  drawFooters(doc, page, room.name, summaryMode === 'separate-page' ? 1 : 0)

  doc.save(pdfFileName(room.name, tile))
}

// Neutral fills used when the baseplate grid is printed — a grey scale, not colour.
// A table carrying plates is the grey field the white plates are cut out of; a table
// without plates is a lighter grey. Lines are dark either way: the table boundary is
// never mistaken for a plate edge.
const GREY_TABLE_WITH_PLATES = [176, 176, 176]
const GREY_TABLE_PLAIN = [214, 214, 214]
const TABLE_OUTLINE_WITH_PLATES = [40, 40, 40]
const TABLE_OUTLINE_PLAIN = [120, 120, 120]

/**
 * Table instance ids that belong to a group with a plate size selected. Empty set when
 * no group has one — then there is no grid and the template colours stay.
 */
function aggregatesWithGrid(aggregates, options) {
  const ids = new Set()
  aggregates.forEach((group, idx) => {
    if (!parseBpKey(options.bpKeyByAggIdx?.[idx])) return
    for (const id of group) ids.add(id)
  })
  return ids
}

// ── Diagram body ───────────────────────────────────────────────────────────────
// Drawn identically on every tile page: the page clip selects the visible slice.
function drawDiagram(doc, ctx) {
  const { room, templateMap, placedTables, aggregates, options, gridPlate, grouped, bare,
    diagX, diagY, diagW, diagH, scale, originX, originY } = ctx

  // No room background box: a white one is the whole page here, and a bordered one
  // reads as the room outline on a bare grid sheet. `drawTiled` clips each sheet, so
  // filling the page is harmless either way — but on a single sheet it is the printer's
  // paper that shows, and the drawing is already centred on it.

  // Tables. With the placeholder grid on, the template colours go away in favour of two
  // neutral greys, because the diagram is then a field to write on: a dark fill hides
  // the grid and a colour photocopy/print muddies it. The distinction printed is the
  // one that matters on paper — tables that carry a plate grid vs. the rest.
  for (const placed of placedTables) {
    const tpl = templateMap[placed.templateId]
    if (!tpl) continue
    const tw = (placed.rotation % 180 === 0 ? tpl.widthCm : tpl.depthCm) * scale
    const th = (placed.rotation % 180 === 0 ? tpl.depthCm : tpl.widthCm) * scale
    const tx = diagX + (placed.xCm - originX) * scale
    const ty = diagY + (placed.yCm - originY) * scale

    if (grouped) {
      // A table carrying a grid stays white: the grid and whatever the user writes in
      // it are the content, the table is only its boundary.
      const hasGrid = grouped.has(placed.instanceId)
      doc.setFillColor(...(hasGrid ? GREY_TABLE_WITH_PLATES : GREY_TABLE_PLAIN))
      doc.setDrawColor(...(hasGrid ? TABLE_OUTLINE_WITH_PLATES : TABLE_OUTLINE_PLAIN))
      doc.setLineWidth(hasGrid ? 0.4 : 0.2)
    } else {
      const rgb = hexToRgb(tpl.color)
      doc.setFillColor(rgb.r, rgb.g, rgb.b)
      doc.setDrawColor(0, 0, 0)
    }
    if (!grouped) doc.setLineWidth(0.2)
    doc.rect(tx, ty, tw, th, 'FD')

    // On a grid printout the table carries no caption: the sheet is meant to be written
    // on, the legend below already names every template.
    if (!grouped && tw > 8 && th > 5) {
      const fs = Math.max(4.5, Math.min(7, Math.min(tw, th) * 0.9))
      doc.setFontSize(fs)
      doc.setTextColor(255, 255, 255)
      const label = doc.splitTextToSize(tpl.description, tw - 1)[0]
      doc.text(label, tx + tw / 2, ty + th / 2, { align: 'center', baseline: 'middle' })
    }
  }

  // Baseplate placeholder grid — drawn over the tables, under the aggregate outline.
  // Tiles are pure outlines: they only show *where* a plate would sit, the planner
  // writes the MOC's footprint inside them by hand.
  // A group only gets a grid when it has a size picked in the plate calculator: with
  // nothing picked there is no plate size to tile with, and the print options say so
  // instead of silently drawing nothing.
  if (gridPlate) {
    for (let idx = 0; idx < aggregates.length; idx++) {
      if (!parseBpKey(options.bpKeyByAggIdx?.[idx])) continue
      const bbox = aggregateBBox(aggregates[idx], placedTables, templateMap)
      if (!bbox) continue
      // Relative to the group's bounding box; whole plates only, centred on it.
      const grid = planPlateGrid({ xMm: 0, yMm: 0, widthMm: bbox.w * 10, depthMm: bbox.h * 10 }, gridPlate)
      const x0 = diagX + (bbox.minX - originX) * scale
      const y0 = diagY + (bbox.minY - originY) * scale

      // Plates are white, the table under them is grey: the plate is the hole you write
      // the MOC in, and white is what a pen works best on.
      doc.setDrawColor(56, 56, 56)
      doc.setLineWidth(0.2)
      doc.setLineJoin('miter')
      for (const t of grid.tiles) {
        const w = t.widthMm / 10 * scale
        const h = t.depthMm / 10 * scale
        if (w < 0.8 || h < 0.8) continue
        doc.setFillColor(255, 255, 255)
        doc.rect(x0 + t.xMm / 10 * scale, y0 + t.yMm / 10 * scale, w, h, 'FD')
      }

      // The aggregate outline is drawn wider than any plate edge: where a plate ends on
      // the group boundary the two lines coincide, and the group's own border must win.
      doc.setLineWidth(0.5)
    }
  }

  // Aggregate bounding boxes with dimension labels — no annotations on a bare sheet.
  for (const group of bare ? [] : aggregates) {
    const bbox = aggregateBBox(group, placedTables, templateMap)
    if (!bbox) continue

    const bx = diagX + bbox.minX * scale
    const by = diagY + bbox.minY * scale
    const bw = bbox.w * scale
    const bh = bbox.h * scale

    // Dashed blue bounding box (grey on the grid sheet — see the note in the grid block)
    doc.setDrawColor(...(gridPlate ? [56, 56, 56] : [26, 144, 208]))
    doc.setLineWidth(gridPlate ? 0.5 : 0.4)
    doc.setLineDashPattern([1.5, 1], 0)
    doc.rect(bx, by, bw, bh, 'S')
    doc.setLineDashPattern([], 0)

    // Dimension tag — place above the bounding box, clamp inside diagram
    const label = `${formatDim(Math.round(bbox.w))} × ${formatDim(Math.round(bbox.h))}`
    doc.setFontSize(6)
    const tagW = doc.getTextWidth(label) + 3
    const tagH = 4
    const tagX = Math.max(diagX, Math.min(diagX + diagW - tagW, bx + bw / 2 - tagW / 2))
    const tagY = by > diagY + tagH + 1 ? by - tagH - 1 : by + 1

    doc.setFillColor(26, 144, 208)
    doc.roundedRect(tagX, tagY, tagW, tagH, 0.5, 0.5, 'F')
    doc.setTextColor(255, 255, 255)
    doc.text(label, tagX + tagW / 2, tagY + tagH / 2, { align: 'center', baseline: 'middle' })
  }

  // Room border on top. Not drawn on a bare grid sheet: the request there is the tables
  // and nothing else — no room outline, no dimensions.
  if (bare) return

  doc.setDrawColor(60, 60, 60)
  doc.setLineWidth(0.5)
  doc.rect(diagX, diagY, diagW, diagH, 'S')

  // ── Dimension annotations ──────────────────────────────────────────────────────
  const dimGap = 4
  const tickLen = 2
  const dimC = [60, 60, 60]

  doc.setDrawColor(...dimC)
  doc.setLineWidth(0.3)
  doc.setFont('helvetica', 'normal')
  doc.setFontSize(7.5)
  doc.setTextColor(...dimC)

  // Bottom: room width
  const bdY = diagY + diagH + dimGap
  doc.setLineDashPattern([0.8, 0.8], 0)
  doc.setDrawColor(160, 160, 160)
  doc.line(diagX, diagY + diagH, diagX, bdY + tickLen)
  doc.line(diagX + diagW, diagY + diagH, diagX + diagW, bdY + tickLen)
  doc.setLineDashPattern([], 0)
  doc.setDrawColor(...dimC)
  doc.line(diagX, bdY, diagX + diagW, bdY)
  doc.line(diagX, bdY - tickLen, diagX, bdY + tickLen)
  doc.line(diagX + diagW, bdY - tickLen, diagX + diagW, bdY + tickLen)
  doc.text(formatDim(room.widthCm), diagX + diagW / 2, bdY + 3.5, { align: 'center' })

  // Right: room depth
  const rdX = diagX + diagW + dimGap
  doc.setLineDashPattern([0.8, 0.8], 0)
  doc.setDrawColor(160, 160, 160)
  doc.line(diagX + diagW, diagY, rdX + tickLen, diagY)
  doc.line(diagX + diagW, diagY + diagH, rdX + tickLen, diagY + diagH)
  doc.setLineDashPattern([], 0)
  doc.setDrawColor(...dimC)
  doc.line(rdX, diagY, rdX, diagY + diagH)
  doc.line(rdX - tickLen, diagY, rdX + tickLen, diagY)
  doc.line(rdX - tickLen, diagY + diagH, rdX + tickLen, diagY + diagH)
  doc.text(formatDim(room.depthCm), rdX + 4, diagY + diagH / 2, { angle: 90, align: 'center', baseline: 'middle' })
}

/**
 * Bounding box of every placed table, in cm, or null when there is nothing to draw.
 * Used on a bare grid sheet, where the drawing is scaled to the tables instead of the
 * room — an almost empty 6×6 m room should still print a legible grid.
 */
function tablesBBox(placedTables, templateMap) {
  const boxes = []
  for (const p of placedTables) {
    const tpl = templateMap[p.templateId]
    if (!tpl) continue
    const w = p.rotation % 180 === 0 ? tpl.widthCm : tpl.depthCm
    const h = p.rotation % 180 === 0 ? tpl.depthCm : tpl.widthCm
    boxes.push({ x: p.xCm, y: p.yCm, w, h })
  }
  if (boxes.length === 0) return null
  const minX = Math.min(...boxes.map(b => b.x))
  const minY = Math.min(...boxes.map(b => b.y))
  const maxX = Math.max(...boxes.map(b => b.x + b.w))
  const maxY = Math.max(...boxes.map(b => b.y + b.h))
  return { minX, minY, w: maxX - minX, h: maxY - minY }
}

// Returns bounding box { minX, minY, maxX, maxY, w, h } in cm, or null if empty
function aggregateBBox(group, placedTables, templateMap) {
  let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity
  for (const id of group) {
    const t = placedTables.find(p => p.instanceId === id)
    if (!t) continue
    const tpl = templateMap[t.templateId]
    if (!tpl) continue
    const w = t.rotation % 180 === 0 ? tpl.widthCm : tpl.depthCm
    const h = t.rotation % 180 === 0 ? tpl.depthCm : tpl.widthCm
    minX = Math.min(minX, t.xCm);     minY = Math.min(minY, t.yCm)
    maxX = Math.max(maxX, t.xCm + w); maxY = Math.max(maxY, t.yCm + h)
  }
  if (!isFinite(minX)) return null
  return { minX, minY, maxX, maxY, w: maxX - minX, h: maxY - minY }
}

function formatDim(cm) {
  if (cm >= 100) return `${(cm / 100).toFixed(2)} m`
  return `${cm} cm`
}

function pdfFileName(name, tile = 1) {
  const slug = name.toLowerCase().replace(/\s+/g, '-').replace(/[^a-z0-9-]/g, '')
  const date = new Date().toISOString().slice(0, 10)
  const suffix = tile > 1 ? `-${tile}x${tile}` : ''
  return `room-${slug}${suffix}-${date}.pdf`
}

function hexToRgb(hex) {
  const h = (hex || '#888888').replace('#', '')
  return {
    r: parseInt(h.substring(0, 2), 16) || 0,
    g: parseInt(h.substring(2, 4), 16) || 0,
    b: parseInt(h.substring(4, 6), 16) || 0,
  }
}

// Sheet index on the diagram pages (bottom-right, inside the margin). The numbering is
// "diagram sheet k of N" so a summary page appended after the sheets is not counted as a
// map sheet, and N is the number of pages really printed — see `page.grid`.
function drawFooters(doc, page, name, extraPages) {
  const pages = page.grid.rows * page.grid.cols
  // A single sheet with nothing appended needs no footer at all.
  if (pages === 1 && extraPages === 0) return
  for (let p = 1; p <= pages; p++) {
    doc.setPage(p)
    const clip = sheetClip(page, p - 1)
    const where = pages > 1 ? ` (row ${clip.row + 1}, column ${clip.col + 1})` : ''
    doc.setFont('helvetica', 'normal')
    doc.setFontSize(7)
    doc.setTextColor(130, 130, 130)
    doc.text(
      `${name} — sheet ${p} of ${pages}${where}`,
      page.pageW - page.margin, page.pageH - page.margin / 2,
      { align: 'right', baseline: 'middle' }
    )
  }
  doc.setPage(1)
}
