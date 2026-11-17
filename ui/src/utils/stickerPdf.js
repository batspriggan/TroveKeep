// Drawing of the sticker sheet: turns the plan produced by `stickerSheets.js` into a real
// A4 PDF the operator prints at 100% and cuts out. Only drawing lives here — where each
// label goes is the packer's job, so this module can be exercised with a stub document
// (see ui/tests/stickerPdf.test.mjs).
import { jsPDF } from 'jspdf'
import { planStickerSheet } from './stickerSheets.js'

/**
 * Draws the plan on `doc` (any object with the jsPDF drawing API: `addPage`, `rect`,
 * `addImage`, `setDrawColor`, `setLineWidth`), one PDF page per packed page.
 *
 * `images` maps a **project item id** to `{ dataUrl, format, rotatedDataUrl?, rotatedFormat? }`:
 * the artwork rasterised into the label's graphic rectangle, with the item's fit already baked in
 * (`doc.addImage` cannot crop, so `stickerImages.js` does it — see `computeFitGeometry`). A cell
 * whose artwork is missing is still cut (a bordered empty label) rather than silently skipped, so
 * the sheet the operator sees matches the plan.
 */
export function drawStickerSheet(doc, plan, images = {}) {
  const border = plan.options?.borderMm ?? 0
  doc.setDrawColor?.(0, 0, 0)
  doc.setLineWidth?.(border)

  plan.pages.forEach((page, index) => {
    if (index > 0) doc.addPage()
    for (const cell of page.cells) drawCell(doc, cell, images, border)
  })
  return doc
}

function drawCell(doc, cell, images, border) {
  // The black cut border is stroked INSET by half its width, so the outer edge of the
  // stroke falls exactly on the cutting line (no black bleeding outside the label).
  if (border > 0) {
    doc.setDrawColor?.(0, 0, 0)
    doc.setLineWidth?.(border)
    doc.rect(cell.x + border / 2, cell.y + border / 2, cell.w - border, cell.h - border, 'S')
  }

  const image = images[String(cell.stickerId)]
  if (!image) return

  // A rotated label uses the pre-rotated raster: its aspect ratio is the swapped one, so
  // the artwork fills the graphic area without a second scaling decision here.
  const dataUrl = (cell.rotated && image.rotatedDataUrl) || image.dataUrl
  const format = (cell.rotated && image.rotatedFormat) || image.format || 'PNG'
  if (!dataUrl) return

  // One alias per (item, orientation): jsPDF stores the raster once in the PDF and every
  // further copy references it, so a sheet of 400 copies of one label is not 400 images.
  const alias = `${cell.stickerId}${cell.rotated ? '-r' : ''}`
  doc.addImage(dataUrl, format, cell.graphic.x, cell.graphic.y, cell.graphic.w, cell.graphic.h, alias)
}

/**
 * Builds the PDF document of an already packed plan. `documentFactory` exists for tests
 * that want to inspect the document instead of the file.
 */
export function buildStickerPdf(plan, images = {}, { documentFactory } = {}) {
  const make = documentFactory ?? ((opts) => new jsPDF(opts))
  const doc = make({
    orientation: 'portrait',
    unit: 'mm',
    format: 'a4',
    compress: true,
  })
  drawStickerSheet(doc, plan, images)
  return doc
}

/** `YYYY-MM-DD`, in local time: the file name the operator files the sheet under. */
export function stickerFileDate(date = new Date()) {
  const pad = (n) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
}

/**
 * Packs the selection, builds the PDF and hands it to the browser download.
 * `images` is what `loadStickerImages` returned (keyed by item id).
 * Returns `{ fileName, pages, count }` so the caller can report what was generated.
 */
export function downloadStickerPdf(selection, options, images = {}, { date = new Date(), documentFactory } = {}) {
  const plan = planStickerSheet(selection, options)
  const doc = buildStickerPdf(plan, images, { documentFactory })
  const fileName = `sticker-sheet-${stickerFileDate(date)}.pdf`
  doc.save(fileName)
  return { fileName, pages: plan.pages.length, count: plan.count }
}
