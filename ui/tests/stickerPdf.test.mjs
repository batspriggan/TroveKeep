// Unit tests for the sticker-sheet PDF renderer. Two halves:
//   1. a stub document that records the drawing calls, which pins down "one black cut
//      rectangle per label, one image placement per label, the rotated raster when the
//      label was turned";
//   2. a real jsPDF document, to prove the output is a well-formed PDF with the expected
//      number of pages.
//
// NOTE: only JPEG can be pushed through jsPDF here. A PNG needs a canvas to be decoded and
// Node has none, so PNG transparency and the real raster size are browser-side checks
// (the upload path hands the engine a canvas-resampled data URL).
import test from 'node:test'
import assert from 'node:assert/strict'

import { planStickerSheet } from '../src/utils/stickerSheets.js'
import { buildStickerPdf, drawStickerSheet, stickerFileDate } from '../src/utils/stickerPdf.js'

// A 1×1 JPEG, the smallest raster jsPDF can embed without a canvas.
const TINY_JPEG =
  '/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHRofHh0a' +
  'HBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/wAALCAABAAEBAREA/8QAFAABAAAAAAAA' +
  'AAAAAAAAAAAACf/EABQQAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQEAAD8AKp//2Q=='

const jpegImage = { dataUrl: `data:image/jpeg;base64,${TINY_JPEG}`, format: 'JPEG' }

function stubDocument() {
  const calls = { addPage: 0, rect: [], addImage: [] }
  return {
    calls,
    addPage() {
      calls.addPage++
    },
    setDrawColor() {},
    setLineWidth() {},
    rect(x, y, w, h, style) {
      calls.rect.push({ x, y, w, h, style })
    },
    addImage(dataUrl, format, x, y, w, h, alias) {
      calls.addImage.push({ dataUrl, format, x, y, w, h, alias })
    },
  }
}

test('every label gets one black cut rectangle and one image placement', () => {
  const plan = planStickerSheet(
    [
      { id: 'a', name: 'A', widthStuds: 2, depthStuds: 4, copies: 3 },
      { id: 'b', name: 'B', widthStuds: 1, depthStuds: 1, copies: 5 },
    ],
    { borderMm: 0.6, paddingMm: 0.6 },
  )
  const doc = stubDocument()
  drawStickerSheet(doc, plan, { a: jpegImage, b: jpegImage })

  assert.equal(doc.calls.rect.length, plan.count)
  assert.equal(doc.calls.addImage.length, plan.count)

  // The stroke is drawn OUTSIDE the label, so its inner edge is the cutting line: the sticker
  // keeps exactly the piece's millimetres and the black sits in the scrap around it.
  for (const [index, rect] of doc.calls.rect.entries()) {
    const cell = plan.cells[index]
    assert.equal(rect.style, 'S')
    assert.equal(rect.x, cell.x - 0.3)
    assert.equal(rect.y, cell.y - 0.3)
    assert.equal(rect.w, cell.w + 0.6)
    assert.equal(rect.h, cell.h + 0.6)
  }

  // The artwork fills the graphic area (the raster already has that aspect ratio), and the
  // raster is stored once per sticker: one distinct alias, reused by every copy.
  for (const [index, image] of doc.calls.addImage.entries()) {
    const cell = plan.cells[index]
    assert.equal(image.x, cell.graphic.x)
    assert.equal(image.y, cell.graphic.y)
    assert.equal(image.w, cell.graphic.w)
    assert.equal(image.h, cell.graphic.h)
    assert.equal(image.format, 'JPEG')
    assert.equal(image.alias, String(cell.stickerId))
  }
  assert.deepEqual([...new Set(doc.calls.addImage.map((i) => i.alias))], ['a', 'b'])
})

test('a rotated label uses the pre-rotated raster and its own alias', () => {
  // A 32-stud-wide label (256 mm) only fits the default sheet turned by 90°.
  const plan = planStickerSheet(
    [{ id: 'wide', name: 'Wide', widthStuds: 32, depthStuds: 4, copies: 1 }],
    { allowRotate: true },
  )
  assert.equal(plan.cells[0].rotated, true)

  const doc = stubDocument()
  drawStickerSheet(doc, plan, {
    wide: {
      dataUrl: 'data:image/jpeg;base64,upright',
      format: 'JPEG',
      rotatedDataUrl: 'data:image/jpeg;base64,rotated',
      rotatedFormat: 'JPEG',
    },
  })
  assert.equal(doc.calls.addImage[0].dataUrl, 'data:image/jpeg;base64,rotated')
  assert.equal(doc.calls.addImage[0].alias, 'wide-r')
  // …and the graphic area follows the turned box.
  assert.equal(doc.calls.addImage[0].w, plan.cells[0].graphic.w)
})

test('a missing artwork still leaves the cut border on the sheet', () => {
  const plan = planStickerSheet([{ id: 'x', name: 'X', widthStuds: 1, depthStuds: 1, copies: 2 }])
  const doc = stubDocument()
  drawStickerSheet(doc, plan, {})
  assert.equal(doc.calls.rect.length, 2)
  assert.equal(doc.calls.addImage.length, 0)
})

test('a real jsPDF document is a PDF with one page per packed page', () => {
  const onePage = buildStickerPdf(
    planStickerSheet([{ id: 'a', name: 'A', widthStuds: 2, depthStuds: 2, copies: 4 }]),
    { a: jpegImage },
  )
  const onePageOut = onePage.output()
  assert.ok(onePageOut.startsWith('%PDF-'), 'the output starts with the PDF magic header')
  assert.equal(onePage.getNumberOfPages(), 1)

  // 700 copies of a 24×32 stud label (192×256 mm) cannot fit one page.
  const bigSelection = planStickerSheet([{ id: 'big', name: 'Big', widthStuds: 24, depthStuds: 32, copies: 2 }])
  const twoPages = buildStickerPdf({ ...bigSelection }, { big: jpegImage })
  assert.ok(bigSelection.pages.length >= 2)
  assert.equal(twoPages.getNumberOfPages(), bigSelection.pages.length)
})

test('the download file name is dated', () => {
  assert.equal(stickerFileDate(new Date(2026, 10, 17)), '2026-11-17')
})
