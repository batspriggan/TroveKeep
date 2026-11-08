import { jsPDF } from 'jspdf'
import { marginForTile, parseBpKey, plateGeometry, planPlateGrid, planSheets, sheetClip, sheetGrid, tileDiagram } from './printSheets.js'

/**
 * room:         { name }
 * aggTables:    [{ instanceId, templateId, xCm, yCm }]
 * aggBounds:    { minXmm, minYmm, widthMm, heightMm }
 * templateMap:  { [id]: { widthCm, depthCm, description } }
 * placedPlates: [{ instanceId, baseplateId, xMm, yMm, rotation }]
 * baseplateMap: { [id]: { widthStuds, depthStuds, type, legoColorRgb, name } }
 * baseplates:   array — used for legend ordering
 * options:
 *   baseplateGrid — also draw a placeholder grid for a plate type that is *not*
 *                   placed yet (the real placements are already in the diagram).
 *                   Meant to be filled in by hand on paper.
 *   gridBpKey     — "16x32" of the placeholder plate type; ignored when unset
 *   tileSize      — 1 | 2 | 3: print the diagram across tileSize² A4 sheets at
 *                   full size instead of shrinking it onto one page.
 *   tableSummary  — 'inline' | 'separate-page' | 'none', same as in `roomPdf.js`.
 */
export function generateAggregatePdf(room, aggTables, aggBounds, templateMap, placedPlates, baseplateMap, baseplates, options = {}) {
  const { widthMm, heightMm } = aggBounds
  if (!widthMm || !heightMm) return

  const tile = options.tileSize ?? 1
  const margin = marginForTile(tile)
  const page = planSheets(widthMm, heightMm, tile, margin)
  const doc = new jsPDF({
    orientation: page.landscape ? 'landscape' : 'portrait',
    unit: 'mm',
    format: 'a4',
  })

  const pageW = page.pageW
  const contentW = page.contentW
  // With a placeholder size picked, the sheet is only the drawing: no title block, no
  // dimension lines, no summary, and the real placements give way to the grid. Modes
  // without a size (grid on, size not picked yet) are a normal sheet.
  const bare = !!options.baseplateGrid && !!parseBpKey(options.gridBpKey)
  // A plate-grid sheet is the drawing only: the summary is not an option there.
  const summaryMode = bare ? 'none' : (options.tableSummary ?? 'inline')
  // Pages past the first are diagram only — see `generateRoomPdf`.
  // The drawing is rendered on the BIG sheet on a tiled export (see `planSheets`), so
  // it gets the room of every page at once.
  let diagramPageH
  if (tile > 1) diagramPageH = page.sheetH - (bare ? 2 * margin : 25)
  else if (summaryMode === 'inline') diagramPageH = page.contentH - 30 - 60
  else diagramPageH = page.contentH - (bare ? 0 : 30)

  const DIM_RIGHT  = bare ? 0 : 18
  const DIM_BOTTOM = bare ? 0 : 10

  let curY = margin

  // Pre-compute legend ordering so diagram can reference numbers
  const counts = {}
  for (const p of placedPlates) counts[p.baseplateId] = (counts[p.baseplateId] || 0) + 1

  const usedBaseplates = baseplates
    .filter(bp => counts[bp.id])
    .sort((a, b) => {
      const ta = a.type ?? 'Standard', tb = b.type ?? 'Standard'
      if (ta !== tb) return ta.localeCompare(tb)
      if (a.widthStuds !== b.widthStuds) return b.widthStuds - a.widthStuds
      return b.depthStuds - a.depthStuds
    })

  const legendIndex = {}
  usedBaseplates.forEach((bp, i) => { legendIndex[bp.id] = i + 1 })

  // ── Header ────────────────────────────────────────────────────────────────────
  // A page element: drawn on page 1 only, never on the big sheet that gets cut.
  const drawHeader = () => {
    if (bare) return
    doc.setFont('helvetica', 'bold')
    doc.setFontSize(18)
    doc.setTextColor(30, 30, 30)
    doc.text(`${room.name} — Baseplate Layout`, margin, margin)
    doc.setFont('helvetica', 'normal')
    doc.setFontSize(10)
    doc.setTextColor(100, 100, 100)
    doc.text(`${formatMm(widthMm)} × ${formatMm(heightMm)}`, margin, margin + 8)
    if (tile > 1) {
      doc.text(`Full size · ${tile}×${tile} A4 sheets`, margin + page.contentW, margin, { align: 'right' })
    }
  }

  // ── Layout diagram ─────────────────────────────────────────────────────────────
  // Coordinates are sheet coordinates on a tiled export, page coordinates otherwise.
  const boxW = (tile > 1 ? page.sheetW - 2 * margin : contentW) - DIM_RIGHT
  const boxH = Math.max(40, diagramPageH)
  // Anchored to the top-left of the big sheet on a tiled export (centring there would
  // leave whole pages half empty); centred on a single sheet.
  const diagX = margin + (tile > 1 ? 0 : (boxW - widthMm * Math.min(boxW / widthMm, boxH / heightMm)) / 2)
  const scale = Math.min(boxW / widthMm, boxH / heightMm)
  const diagW = widthMm * scale
  const diagH = heightMm * scale
  const diagY = (bare ? margin : curY) + (tile > 1 ? 0 : (boxH - diagH) / 2)

  const drawSheet = () => {
    drawDiagramBody({
      doc, aggTables, templateMap, placedPlates, baseplateMap, legendIndex,
      widthMm, heightMm, minXmm: aggBounds.minXmm, minYmm: aggBounds.minYmm,
      diagX, diagY, diagW, diagH, scale, options, bare,
    })
  }

  // ── Baseplate summary legend ───────────────────────────────────────────────────
  // A page element like the header: drawn by the page frame, after the cut.
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
      curY = diagBottom() + DIM_BOTTOM + 6
    }
    drawSummaryTable()
    if (summaryMode === 'separate-page') doc.setPage(1)
  }

  const drawSummaryTable = () => {
  doc.setFont('helvetica', 'bold')
  doc.setFontSize(13)
  doc.setTextColor(30, 30, 30)
  doc.text('Baseplate Summary', margin, curY)
  curY += 7

  const swatchW = 10
  const numW    = 8
  const nameW   = 37
  const sizeW   = 28
  const dimsW   = 40
  const qtyW    = 18
  const rowH    = 7
  const pad     = 2

  // Header row
  doc.setFillColor(234, 236, 242)
  doc.rect(margin, curY, contentW, rowH, 'F')
  doc.setDrawColor(200, 200, 200)
  doc.setLineWidth(0.2)
  doc.rect(margin, curY, contentW, rowH, 'S')
  doc.setFont('helvetica', 'bold')
  doc.setFontSize(8.5)
  doc.setTextColor(70, 70, 70)
  const hY = curY + rowH / 2
  let cx = margin + swatchW + pad
  doc.text('#',          cx, hY, { baseline: 'middle' }); cx += numW
  doc.text('Name',       cx, hY, { baseline: 'middle' }); cx += nameW
  doc.text('Size',       cx, hY, { baseline: 'middle' }); cx += sizeW
  doc.text('Dims (mm)',  cx, hY, { baseline: 'middle' }); cx += dimsW
  doc.text('Qty',        cx, hY, { baseline: 'middle' })
  curY += rowH

  doc.setFont('helvetica', 'normal')
  doc.setFontSize(8.5)
  for (let i = 0; i < usedBaseplates.length; i++) {
    const bp  = usedBaseplates[i]
    const qty = counts[bp.id] || 0
    const rowY = curY
    const midY = rowY + rowH / 2

    if (i % 2 === 1) {
      doc.setFillColor(248, 249, 251)
      doc.rect(margin, rowY, contentW, rowH, 'F')
    }

    // Placeholder-grid printouts are greyscale: a colour swatch next to swatches that
    // are printed in grey would be the only colour left on the sheet.
    const rgb = Object.values(legoRgbToRgb(bp.legoColorRgb))
    doc.setFillColor(...rgb)
    doc.rect(margin, rowY, swatchW, rowH, 'F')

    doc.setDrawColor(224, 227, 234)
    doc.setLineWidth(0.2)
    doc.line(margin, rowY + rowH, margin + contentW, rowY + rowH)

    const nw = bpNaturalW(bp)
    const nh = bpNaturalH(bp)
    doc.setTextColor(40, 40, 40)
    let col = margin + swatchW + pad
    doc.text(String(i + 1),                                                           col, midY, { baseline: 'middle' }); col += numW
    doc.text(doc.splitTextToSize(bp.name || 'Baseplate', nameW - pad * 2)[0],         col, midY, { baseline: 'middle' }); col += nameW
    doc.text(`${bp.widthStuds}×${bp.depthStuds} studs`,                               col, midY, { baseline: 'middle' }); col += sizeW
    doc.text(`${nw.toFixed(1)}×${nh.toFixed(1)}`,                                     col, midY, { baseline: 'middle' }); col += dimsW
    doc.text(String(qty),                                                              col, midY, { baseline: 'middle' })

    curY += rowH
  }

  doc.setDrawColor(180, 180, 180)
  doc.setLineWidth(0.3)
  doc.rect(margin, curY - rowH * usedBaseplates.length - rowH, contentW, rowH * (usedBaseplates.length + 1), 'S')
  }

  // Bottom of the drawing on page 1, in page coordinates (the summary sits under it).
  const diagBottom = () => (bare ? margin + diagH : diagY + diagH)

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
    if (summaryMode !== 'none' && usedBaseplates.length > 0) drawSummary()
  })

  stampSheets(doc, page, room.name, tile, summaryMode === 'separate-page' ? 1 : 0)
  doc.save(pdfFileName(room.name, tile))
}

// ── Diagram body ───────────────────────────────────────────────────────────────
// Drawn once per tiled sheet: the page clip decides which slice is visible, so every
// coordinate here is relative to page 1.
function drawDiagramBody(ctx) {
  const {
    doc, aggTables, templateMap, placedPlates, baseplateMap, legendIndex,
    widthMm, heightMm, minXmm, minYmm, diagX, diagY, diagW, diagH, scale, options, bare,
  } = ctx

  // Placeholder size picked → neutral greys for the tables (see the block below).
  const gridStuds = bare ? parseBpKey(options.gridBpKey) : null

  // Diagram background — none in grid mode: the sheet then carries the tables and
  // nothing else, not even the aggregate's own frame.
  if (!gridStuds) {
    doc.setFillColor(247, 248, 250)
    doc.setDrawColor(200, 200, 200)
    doc.setLineWidth(0.3)
    doc.rect(diagX, diagY, diagW, diagH, 'FD')
  }

  // Table silhouettes
  for (const t of aggTables) {
    const tpl = templateMap[t.templateId]
    if (!tpl) continue
    const tx = diagX + (t.xCm * 10 - minXmm) * scale
    const ty = diagY + (t.yCm * 10 - minYmm) * scale
    const tw = tpl.widthCm * 10 * scale
    const th = tpl.depthCm * 10 * scale

    // With the grid on, the table is the grey field the white plates are cut out of;
    // the outline is heavy so it never reads as a plate edge.
    const withGrid = !!gridStuds
    doc.setFillColor(...(withGrid ? [176, 176, 176] : [218, 225, 236]))
    doc.setDrawColor(...(withGrid ? [40, 40, 40] : [160, 175, 200]))
    doc.setLineWidth(withGrid ? 0.4 : 0.2)
    doc.rect(tx, ty, tw, th, 'FD')

    // No caption on a grid printout: the sheet is meant to be written on.
    if (!withGrid && tw > 6 && th > 4) {
      const fs = Math.max(4, Math.min(6.5, Math.min(tw, th) * 0.6))
      doc.setFontSize(fs)
      doc.setTextColor(100, 120, 160)
      const label = doc.splitTextToSize(tpl.description, tw - 1)[0]
      doc.text(label, tx + tw / 2, ty + th / 2, { align: 'center', baseline: 'middle' })
    }
  }

  // Placeholder grid: whole plates, outlines only — the user draws the MOC footprint in
  // the squares by hand. When it is on it replaces the real placements below.
  if (gridStuds) {
    const plate = plateGeometry(gridStuds.widthStuds, gridStuds.depthStuds, options.gridBpType ?? 'Standard')
    const grid = planPlateGrid({ xMm: 0, yMm: 0, widthMm, depthMm: heightMm }, plate)
    // Solid and darker than the table outlines: the grid is the point of this sheet.
    doc.setDrawColor(70, 70, 70)
    doc.setLineWidth(0.2)
    for (const t of grid.tiles) {
      doc.rect(diagX + t.xMm * scale, diagY + t.yMm * scale, t.widthMm * scale, t.depthMm * scale, 'S')
    }
  }

  // Placed baseplates — suppressed on a grid sheet: the grid is what that sheet is for,
  // and laying the real plates over it would leave no empty square to write in.
  for (const p of gridStuds ? [] : placedPlates) {
    const bp = baseplateMap[p.baseplateId]
    if (!bp) continue
    const ew = bpEffW(bp, p.rotation)
    const eh = bpEffH(bp, p.rotation)
    const px = diagX + p.xMm * scale
    const py = diagY + p.yMm * scale
    const pw = ew * scale
    const ph = eh * scale

    const rgb = legoRgbToRgb(bp.legoColorRgb)
    doc.setFillColor(rgb.r, rgb.g, rgb.b)
    doc.setDrawColor(0, 0, 0)
    doc.setLineWidth(0.2)
    doc.rect(px, py, pw, ph, 'FD')

    if (pw > 4 && ph > 3) {
      const num = String(legendIndex[p.baseplateId] ?? '?')
      const dims = `${bp.widthStuds}×${bp.depthStuds}`
      const cx2 = px + pw / 2
      doc.setTextColor(255, 255, 255)
      if (ph > 6) {
        const fs = Math.max(4, Math.min(6.5, Math.min(pw, ph) * 0.4))
        doc.setFontSize(fs)
        doc.text(num,  cx2, py + ph / 2 - fs * 0.35, { align: 'center', baseline: 'middle' })
        doc.text(dims, cx2, py + ph / 2 + fs * 0.75, { align: 'center', baseline: 'middle' })
      } else {
        const fs = Math.max(4, Math.min(7, Math.min(pw, ph) * 0.6))
        doc.setFontSize(fs)
        doc.text(num, cx2, py + ph / 2, { align: 'center', baseline: 'middle' })
      }
    }
  }

  // Aggregate border on top — omitted in grid mode, where only the tables remain.
  if (gridStuds) return

  doc.setDrawColor(60, 60, 60)
  doc.setLineWidth(0.5)
  doc.rect(diagX, diagY, diagW, diagH, 'S')

  if (bare) return

  // ── Dimension annotations ────────────────────────────────────────────────────
  const dimGap  = 4
  const tickLen = 2
  const dimC    = [60, 60, 60]

  doc.setDrawColor(...dimC)
  doc.setLineWidth(0.3)
  doc.setFont('helvetica', 'normal')
  doc.setFontSize(7.5)
  doc.setTextColor(...dimC)

  // Bottom: aggregate width
  const bdY = diagY + diagH + dimGap
  doc.setLineDashPattern([0.8, 0.8], 0)
  doc.setDrawColor(160, 160, 160)
  doc.line(diagX,         diagY + diagH, diagX,         bdY + tickLen)
  doc.line(diagX + diagW, diagY + diagH, diagX + diagW, bdY + tickLen)
  doc.setLineDashPattern([], 0)
  doc.setDrawColor(...dimC)
  doc.line(diagX, bdY, diagX + diagW, bdY)
  doc.line(diagX,         bdY - tickLen, diagX,         bdY + tickLen)
  doc.line(diagX + diagW, bdY - tickLen, diagX + diagW, bdY + tickLen)
  doc.text(formatMm(widthMm), diagX + diagW / 2, bdY + 3.5, { align: 'center' })

  // Right: aggregate height
  const rdX = diagX + diagW + dimGap
  doc.setLineDashPattern([0.8, 0.8], 0)
  doc.setDrawColor(160, 160, 160)
  doc.line(diagX + diagW, diagY,          rdX + tickLen, diagY)
  doc.line(diagX + diagW, diagY + diagH,  rdX + tickLen, diagY + diagH)
  doc.setLineDashPattern([], 0)
  doc.setDrawColor(...dimC)
  doc.line(rdX, diagY, rdX, diagY + diagH)
  doc.line(rdX - tickLen, diagY,         rdX + tickLen, diagY)
  doc.line(rdX - tickLen, diagY + diagH, rdX + tickLen, diagY + diagH)
  doc.text(formatMm(heightMm), rdX + 4, diagY + diagH / 2, { angle: 90, align: 'center', baseline: 'middle' })
}

// ── Baseplate geometry helpers ─────────────────────────────────────────────────
function isStdGeom(bp) { return bp.type !== 'Custom' || (bp.widthStuds % 8 === 0 && bp.depthStuds % 8 === 0) }
function bpNaturalW(bp) { return bp.widthStuds * 8 - (isStdGeom(bp) ? 0.2 : 2) }
function bpNaturalH(bp) { return bp.depthStuds * 8 - (isStdGeom(bp) ? 0.2 : 2) }
function bpEffW(bp, rot) { return (rot === 0 || rot === 180) ? bpNaturalW(bp) : bpNaturalH(bp) }
function bpEffH(bp, rot) { return (rot === 0 || rot === 180) ? bpNaturalH(bp) : bpNaturalW(bp) }

// ── Formatting ─────────────────────────────────────────────────────────────────
function formatMm(mm) {
  if (mm >= 1000) return `${(mm / 1000).toFixed(2)} m`
  if (mm >= 10)   return `${(mm / 10).toFixed(1)} cm`
  return `${mm.toFixed(1)} mm`
}

// Sheet index on every page of a tiled export (bottom-right, inside the margin). N is
// the number of pages really printed — see `page.grid`.
function stampSheets(doc, page, name, tile, extraPages) {
  const pages = page.grid.rows * page.grid.cols
  // A single sheet with nothing appended needs no footer; a summary page must not
  // inflate the count ("sheet 2 of 5" for a 2×2 export is wrong).
  if (pages === 1 && extraPages === 0) return
  for (let p = 1; p <= pages; p++) {
    doc.setPage(p)
    const clip = sheetClip(page, p - 1)
    // 1-based row/column of the cut; slate numbering follows reading order.
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

function pdfFileName(name, tile = 1) {
  const slug = name.toLowerCase().replace(/\s+/g, '-').replace(/[^a-z0-9-]/g, '')
  const date = new Date().toISOString().slice(0, 10)
  const suffix = tile > 1 ? `-${tile}x${tile}` : ''
  return `baseplates-${slug}${suffix}-${date}.pdf`
}

function legoRgbToRgb(rgbHex) {
  if (!rgbHex) return { r: 154, g: 172, b: 204 }
  const h = rgbHex.padStart(6, '0')
  return {
    r: parseInt(h.substring(0, 2), 16) || 0,
    g: parseInt(h.substring(2, 4), 16) || 0,
    b: parseInt(h.substring(4, 6), 16) || 0,
  }
}
