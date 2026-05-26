import { jsPDF } from 'jspdf'

/**
 * room:         { name, widthCm, depthCm }
 * templates:    [{ id, description, widthCm, depthCm, color }]
 * placedTables: [{ instanceId, templateId, xCm, yCm, rotation }]
 * aggregates:   [[instanceId, ...], ...]  — each sub-array is a connected group
 */
export function generateRoomPdf(room, templates, placedTables, aggregates = []) {
  const templateMap = {}
  for (const t of templates) templateMap[t.id] = t

  const counts = {}
  for (const p of placedTables) counts[p.templateId] = (counts[p.templateId] || 0) + 1

  const landscape = room.widthCm > room.depthCm
  const doc = new jsPDF({ orientation: landscape ? 'landscape' : 'portrait', unit: 'mm', format: 'a4' })

  const pageW = doc.internal.pageSize.getWidth()
  const margin = 15
  const contentW = pageW - margin * 2

  const DIM_RIGHT  = 18
  const DIM_BOTTOM = 10

  let curY = margin

  // ── Header ────────────────────────────────────────────────────────────────────
  doc.setFont('helvetica', 'bold')
  doc.setFontSize(20)
  doc.setTextColor(30, 30, 30)
  doc.text(room.name, margin, curY)
  curY += 8

  doc.setFont('helvetica', 'normal')
  doc.setFontSize(10)
  doc.setTextColor(100, 100, 100)
  doc.text(`${formatDim(room.widthCm)} × ${formatDim(room.depthCm)}`, margin, curY)
  curY += 8

  // ── Layout diagram ─────────────────────────────────────────────────────────────
  const aspect   = room.widthCm / (room.depthCm || 1)
  const maxDiagH = landscape ? 85 : 115
  let diagW = contentW - DIM_RIGHT
  let diagH = diagW / aspect
  if (diagH > maxDiagH) { diagH = maxDiagH; diagW = diagH * aspect }
  const diagX = margin
  const diagY = curY
  const scale = diagW / room.widthCm  // mm per cm

  // Room background
  doc.setFillColor(255, 255, 255)
  doc.setDrawColor(200, 200, 200)
  doc.setLineWidth(0.3)
  doc.rect(diagX, diagY, diagW, diagH, 'FD')

  // Tables
  for (const placed of placedTables) {
    const tpl = templateMap[placed.templateId]
    if (!tpl) continue
    const tw = (placed.rotation % 180 === 0 ? tpl.widthCm : tpl.depthCm) * scale
    const th = (placed.rotation % 180 === 0 ? tpl.depthCm : tpl.widthCm) * scale
    const tx = diagX + placed.xCm * scale
    const ty = diagY + placed.yCm * scale

    const rgb = hexToRgb(tpl.color)
    doc.setFillColor(rgb.r, rgb.g, rgb.b)
    doc.setDrawColor(0, 0, 0)
    doc.setLineWidth(0.2)
    doc.rect(tx, ty, tw, th, 'FD')

    if (tw > 8 && th > 5) {
      const fs = Math.max(4.5, Math.min(7, Math.min(tw, th) * 0.9))
      doc.setFontSize(fs)
      doc.setTextColor(255, 255, 255)
      const label = doc.splitTextToSize(tpl.description, tw - 1)[0]
      doc.text(label, tx + tw / 2, ty + th / 2, { align: 'center', baseline: 'middle' })
    }
  }

  // Aggregate bounding boxes with dimension labels
  for (const group of aggregates) {
    const bbox = aggregateBBox(group, placedTables, templateMap)
    if (!bbox) continue

    const bx = diagX + bbox.minX * scale
    const by = diagY + bbox.minY * scale
    const bw = bbox.w * scale
    const bh = bbox.h * scale

    // Dashed blue bounding box
    doc.setDrawColor(26, 144, 208)
    doc.setLineWidth(0.4)
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

  // Room border on top
  doc.setDrawColor(60, 60, 60)
  doc.setLineWidth(0.5)
  doc.rect(diagX, diagY, diagW, diagH, 'S')

  // Scale bar (1 m) inside diagram, bottom-right
  if (room.widthCm >= 100) {
    const sbW = 100 * scale
    const sbX = diagX + diagW - sbW - 2
    const sbY = diagY + diagH - 3
    doc.setDrawColor(70, 70, 70)
    doc.setLineWidth(0.4)
    doc.line(sbX, sbY, sbX + sbW, sbY)
    doc.line(sbX, sbY - 1.5, sbX, sbY + 1)
    doc.line(sbX + sbW, sbY - 1.5, sbX + sbW, sbY + 1)
    doc.setFontSize(6)
    doc.setTextColor(70, 70, 70)
    doc.text('1 m', sbX + sbW / 2, sbY - 2.5, { align: 'center' })
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

  // Bottom: room width
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
  doc.text(formatDim(room.widthCm), diagX + diagW / 2, bdY + 3.5, { align: 'center' })

  // Right: room depth
  const rdX = diagX + diagW + dimGap
  doc.setLineDashPattern([0.8, 0.8], 0)
  doc.setDrawColor(160, 160, 160)
  doc.line(diagX + diagW, diagY,         rdX + tickLen, diagY)
  doc.line(diagX + diagW, diagY + diagH, rdX + tickLen, diagY + diagH)
  doc.setLineDashPattern([], 0)
  doc.setDrawColor(...dimC)
  doc.line(rdX, diagY, rdX, diagY + diagH)
  doc.line(rdX - tickLen, diagY,         rdX + tickLen, diagY)
  doc.line(rdX - tickLen, diagY + diagH, rdX + tickLen, diagY + diagH)
  doc.text(formatDim(room.depthCm), rdX + 4, diagY + diagH / 2, { angle: 90, align: 'center', baseline: 'middle' })

  curY = diagY + diagH + DIM_BOTTOM + 6

  // ── Table summary legend ───────────────────────────────────────────────────────
  const usedTemplates = templates
    .filter(t => counts[t.id])
    .sort((a, b) => a.description.localeCompare(b.description))

  if (usedTemplates.length === 0) {
    doc.save(pdfFileName(room.name))
    return
  }

  doc.setFont('helvetica', 'bold')
  doc.setFontSize(13)
  doc.setTextColor(30, 30, 30)
  doc.text('Table Summary', margin, curY)
  curY += 7

  const swatchW = 10
  const dimsW   = 38
  const qtyW    = 18
  const descW   = contentW - swatchW - dimsW - qtyW
  const rowH    = 7
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
  doc.text('Description', margin + swatchW + textPad,                 headerY, { baseline: 'middle' })
  doc.text('Dimensions',  margin + swatchW + descW + textPad,         headerY, { baseline: 'middle' })
  doc.text('Qty',         margin + swatchW + descW + dimsW + textPad, headerY, { baseline: 'middle' })
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

    const rgb = hexToRgb(tpl.color)
    doc.setFillColor(rgb.r, rgb.g, rgb.b)
    doc.rect(margin, rowY, swatchW, rowH, 'F')

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

  doc.save(pdfFileName(room.name))
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

function pdfFileName(name) {
  const slug = name.toLowerCase().replace(/\s+/g, '-').replace(/[^a-z0-9-]/g, '')
  const date = new Date().toISOString().slice(0, 10)
  return `room-${slug}-${date}.pdf`
}

function hexToRgb(hex) {
  const h = (hex || '#888888').replace('#', '')
  return {
    r: parseInt(h.substring(0, 2), 16) || 0,
    g: parseInt(h.substring(2, 4), 16) || 0,
    b: parseInt(h.substring(4, 6), 16) || 0,
  }
}
