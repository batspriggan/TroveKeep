import { jsPDF } from 'jspdf'

/**
 * room:         { name }
 * aggTables:    [{ instanceId, templateId, xCm, yCm }]
 * aggBounds:    { minXmm, minYmm, widthMm, heightMm }
 * templateMap:  { [id]: { widthCm, depthCm, description } }
 * placedPlates: [{ instanceId, baseplateId, xMm, yMm, rotation }]
 * baseplateMap: { [id]: { widthStuds, depthStuds, type, legoColorRgb, name } }
 * baseplates:   array — used for legend ordering
 */
export function generateAggregatePdf(room, aggTables, aggBounds, templateMap, placedPlates, baseplateMap, baseplates) {
  const { widthMm, heightMm } = aggBounds
  if (!widthMm || !heightMm) return

  const landscape = widthMm > heightMm
  const doc = new jsPDF({ orientation: landscape ? 'landscape' : 'portrait', unit: 'mm', format: 'a4' })

  const pageW = doc.internal.pageSize.getWidth()
  const margin = 15
  const contentW = pageW - margin * 2

  const DIM_RIGHT  = 18
  const DIM_BOTTOM = 10

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
  doc.setFont('helvetica', 'bold')
  doc.setFontSize(18)
  doc.setTextColor(30, 30, 30)
  doc.text(`${room.name} — Baseplate Layout`, margin, curY)
  curY += 8

  doc.setFont('helvetica', 'normal')
  doc.setFontSize(10)
  doc.setTextColor(100, 100, 100)
  doc.text(`${formatMm(widthMm)} × ${formatMm(heightMm)}`, margin, curY)
  curY += 8

  // ── Layout diagram ─────────────────────────────────────────────────────────────
  const aspect   = widthMm / heightMm
  const maxDiagH = landscape ? 85 : 115
  let diagW = contentW - DIM_RIGHT
  let diagH = diagW / aspect
  if (diagH > maxDiagH) { diagH = maxDiagH; diagW = diagH * aspect }
  const diagX = margin
  const diagY = curY
  const scale = diagW / widthMm  // mm_pdf per mm_real

  // Diagram background
  doc.setFillColor(247, 248, 250)
  doc.setDrawColor(200, 200, 200)
  doc.setLineWidth(0.3)
  doc.rect(diagX, diagY, diagW, diagH, 'FD')

  // Table silhouettes
  for (const t of aggTables) {
    const tpl = templateMap[t.templateId]
    if (!tpl) continue
    const tx = diagX + (t.xCm * 10 - aggBounds.minXmm) * scale
    const ty = diagY + (t.yCm * 10 - aggBounds.minYmm) * scale
    const tw = tpl.widthCm * 10 * scale
    const th = tpl.depthCm * 10 * scale

    doc.setFillColor(218, 225, 236)
    doc.setDrawColor(160, 175, 200)
    doc.setLineWidth(0.2)
    doc.rect(tx, ty, tw, th, 'FD')

    if (tw > 6 && th > 4) {
      const fs = Math.max(4, Math.min(6.5, Math.min(tw, th) * 0.6))
      doc.setFontSize(fs)
      doc.setTextColor(100, 120, 160)
      const label = doc.splitTextToSize(tpl.description, tw - 1)[0]
      doc.text(label, tx + tw / 2, ty + th / 2, { align: 'center', baseline: 'middle' })
    }
  }

  // Placed baseplates
  for (const p of placedPlates) {
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

  // Aggregate border on top
  doc.setDrawColor(60, 60, 60)
  doc.setLineWidth(0.5)
  doc.rect(diagX, diagY, diagW, diagH, 'S')

  // Scale bar (1 stud = 8 mm — show 32 studs = 256 mm if space allows)
  const scaleStuds = widthMm >= 256 ? 32 : 16
  const sbRealMm   = scaleStuds * 8
  const sbW = sbRealMm * scale
  if (sbW < diagW * 0.6) {
    const sbX = diagX + diagW - sbW - 2
    const sbY = diagY + diagH - 3
    doc.setDrawColor(70, 70, 70)
    doc.setLineWidth(0.4)
    doc.line(sbX, sbY, sbX + sbW, sbY)
    doc.line(sbX, sbY - 1.5, sbX, sbY + 1)
    doc.line(sbX + sbW, sbY - 1.5, sbX + sbW, sbY + 1)
    doc.setFontSize(6)
    doc.setTextColor(70, 70, 70)
    doc.text(`${scaleStuds} studs`, sbX + sbW / 2, sbY - 2.5, { align: 'center' })
  }

  // ── Dimension annotations ──────────────────────────────────────────────────────
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

  curY = diagY + diagH + DIM_BOTTOM + 6

  // ── Baseplate summary legend ───────────────────────────────────────────────────
  if (usedBaseplates.length === 0) {
    doc.save(pdfFileName(room.name))
    return
  }

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

    const rgb = legoRgbToRgb(bp.legoColorRgb)
    doc.setFillColor(rgb.r, rgb.g, rgb.b)
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

  doc.save(pdfFileName(room.name))
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

function pdfFileName(name) {
  const slug = name.toLowerCase().replace(/\s+/g, '-').replace(/[^a-z0-9-]/g, '')
  const date = new Date().toISOString().slice(0, 10)
  return `baseplates-${slug}-${date}.pdf`
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
