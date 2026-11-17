// Unit tests for the sticker-sheet engine: 1:1 A4 packing of stud-sized labels.
// These are the numbers the printed sheet depends on, so they are pinned here.
import test from 'node:test'
import assert from 'node:assert/strict'

import {
  DEFAULT_OPTIONS,
  PAGE,
  STUD_MM,
  StickerFitError,
  cutSpacing,
  planStickerSheet,
  resolveOptions,
  stickerSizeMm,
} from '../src/utils/stickerSheets.js'

const EPS = 1e-9

const oneByOne = { id: 's1', name: 'Tile 1×1', widthStuds: 1, depthStuds: 1 }

const copies = (plan) => plan.cells.length

/** Columns and rows actually used on the first page. */
function gridOf(plan, pageIndex = 0) {
  const cells = plan.cells.filter((c) => c.page === pageIndex)
  const xs = new Set(cells.map((c) => c.x.toFixed(6)))
  const ys = new Set(cells.map((c) => c.y.toFixed(6)))
  return { cols: xs.size, rows: ys.size, cells: cells.length }
}

test('a stud is 8 mm, so a 2×4 tile is a 16×32 mm label', () => {
  assert.equal(STUD_MM, 8)
  assert.deepEqual(stickerSizeMm({ widthStuds: 2, depthStuds: 4 }, resolveOptions()), { w: 16, h: 32 })
  // growMm enlarges the cut box on every side.
  assert.deepEqual(
    stickerSizeMm({ widthStuds: 2, depthStuds: 4 }, resolveOptions({ growMm: 0.5 })),
    { w: 17, h: 33 },
  )
})

test('1×1 tiles with no border and no gap fill the page 24 × 35', () => {
  const plan = planStickerSheet([{ ...oneByOne, copies: 1000 }], {
    marginMm: 8,
    borderMm: 0,
    paddingMm: 0,
    gapMm: 0,
  })
  assert.deepEqual(gridOf(plan), { cols: 24, rows: 35, cells: 840 })
  assert.equal(plan.pages.length, 2)
  assert.equal(plan.count, 1000)
})

test('the default options give 18 × 27 labels per page', () => {
  const plan = planStickerSheet([{ ...oneByOne, copies: 1 }])
  // The plan would hold 27 rows and 18 columns of 8 mm labels with a 2.5 mm gap.
  assert.equal(plan.step, 2.5)
  const full = planStickerSheet([{ ...oneByOne, copies: 5000 }])
  assert.deepEqual(gridOf(full), { cols: 18, rows: 27, cells: 486 })
})

test('every requested copy is placed exactly once', () => {
  const plan = planStickerSheet(
    [
      { id: 'a', name: 'A', widthStuds: 2, depthStuds: 4, copies: 7 },
      { id: 'b', name: 'B', widthStuds: 1, depthStuds: 2, copies: 23 },
      { id: 'c', name: 'C', widthStuds: 4, depthStuds: 4, copies: 1 },
    ],
    { gapMm: 2, allowRotate: true },
  )
  assert.equal(copies(plan), 31)
  const seen = new Set(plan.cells.map((c) => `${c.stickerId}#${c.copy}`))
  assert.equal(seen.size, 31)
  for (const cell of plan.cells) {
    assert.ok(cell.copy >= 0)
  }
  assert.deepEqual(
    plan.perSticker.map((s) => [s.id, s.copies]).sort(),
    [['a', 7], ['b', 23], ['c', 1]],
  )
})

test('no two labels overlap', () => {
  const plan = planStickerSheet(
    [
      { id: 'a', name: 'A', widthStuds: 2, depthStuds: 4, copies: 40 },
      { id: 'b', name: 'B', widthStuds: 1, depthStuds: 1, copies: 200 },
      { id: 'c', name: 'C', widthStuds: 3, depthStuds: 2, copies: 15 },
    ],
    { gapMm: 1.5, allowRotate: true },
  )
  for (const page of plan.pages) {
    const cells = page.rows.flatMap((r) => r.cells)
    for (let i = 0; i < cells.length; i++) {
      for (let j = i + 1; j < cells.length; j++) {
        const a = cells[i]
        const b = cells[j]
        const apart =
          a.x + a.w <= b.x + EPS ||
          b.x + b.w <= a.x + EPS ||
          a.y + a.h <= b.y + EPS ||
          b.y + b.h <= a.y + EPS
        assert.ok(apart, `cells ${i} and ${j} overlap on page ${page.index}`)
      }
    }
  }
})

test('every label stays inside the printable content box', () => {
  const plan = planStickerSheet(
    [
      { id: 'a', name: 'A', widthStuds: 2, depthStuds: 8, copies: 30 },
      { id: 'b', name: 'B', widthStuds: 1, depthStuds: 1, copies: 100 },
    ],
    { marginMm: 12, gapMm: 3, allowRotate: true },
  )
  const { content } = plan
  for (const cell of plan.cells) {
    assert.ok(cell.x >= content.x - EPS)
    assert.ok(cell.y >= content.y - EPS)
    assert.ok(cell.x + cell.w <= content.x + content.w + EPS)
    assert.ok(cell.y + cell.h <= content.y + content.h + EPS)
    // …and inside the physical page, obviously.
    assert.ok(cell.x + cell.w <= PAGE.width + EPS)
    assert.ok(cell.y + cell.h <= PAGE.height + EPS)
  }
})

test('the same selection always produces the same sheet', () => {
  const items = [
    { id: 'b', name: 'Bee', widthStuds: 2, depthStuds: 2, copies: 9 },
    { id: 'a', name: 'Ant', widthStuds: 1, depthStuds: 4, copies: 12 },
    { id: 'c', name: 'Cow', widthStuds: 2, depthStuds: 4, copies: 3 },
  ]
  const first = JSON.stringify(planStickerSheet(items, { allowRotate: true }))
  const second = JSON.stringify(planStickerSheet(items.slice().reverse(), { allowRotate: true }))
  assert.equal(first, second)
})

test('rotation is off by default and used only as a fallback when enabled', () => {
  const items = [
    { id: 'a', name: 'A', widthStuds: 1, depthStuds: 8, copies: 20 },
    { id: 'b', name: 'B', widthStuds: 2, depthStuds: 4, copies: 20 },
  ]
  const upright = planStickerSheet(items)
  assert.equal(upright.cells.some((c) => c.rotated), false)
  // With rotation allowed some copy may be turned, but never an upright one that already fits.
  const rotated = planStickerSheet(items, { allowRotate: true, gapMm: 4 })
  for (const cell of rotated.cells) {
    if (cell.rotated) {
      assert.ok(Math.abs(cell.w - cell.h) > EPS, 'only non-square labels can be rotated')
      assert.ok(Math.abs(cell.w - cell.h) > EPS)
    }
  }
})

test('the artwork area is the cut box inset by border + padding', () => {
  const plan = planStickerSheet([{ ...oneByOne, copies: 1 }], {
    borderMm: 0.6,
    paddingMm: 0.6,
  })
  const cell = plan.cells[0]
  const near = (actual, expected) => assert.ok(Math.abs(actual - expected) < EPS, `${actual} ≉ ${expected}`)
  near(cell.graphic.x - cell.x, 1.2)
  near(cell.graphic.y - cell.y, 1.2)
  near(cell.w - cell.graphic.w, 2.4)
  near(cell.h - cell.graphic.h, 2.4)

  // The inner border is the configurable part: 0 means the artwork runs to the black border.
  const tight = planStickerSheet([{ ...oneByOne, copies: 1 }], { borderMm: 0.6, paddingMm: 0 })
  near(tight.cells[0].graphic.x - tight.cells[0].x, 0.6)
  near(tight.cells[0].graphic.w - tight.cells[0].w, -1.2)

  // The cut box itself is the label: nothing is drawn outside it.
  assert.deepEqual(cell.cut, { x: cell.x, y: cell.y, w: cell.w, h: cell.h })
})

test('the gap between cuts is at least twice the border, so the borders never touch', () => {
  assert.equal(cutSpacing(resolveOptions({ borderMm: 0.6, gapMm: 0.2 })), 1.2)
  assert.equal(cutSpacing(resolveOptions({ borderMm: 0.6, gapMm: 2.5 })), 2.5)
  const plan = planStickerSheet([{ ...oneByOne, copies: 400 }], { borderMm: 1, gapMm: 0 })
  assert.equal(plan.step, 2)
  // Two neighbours are exactly one step apart.
  const row = plan.pages[0].rows[0].cells
  assert.equal(row[1].x - (row[0].x + row[0].w), 2)
})

test('options are clamped to their accepted range and rounded to a tenth', () => {
  const opts = resolveOptions({
    marginMm: 2,
    borderMm: 99,
    paddingMm: -3,
    gapMm: 40,
    growMm: 5,
    allowRotate: 1,
  })
  assert.equal(opts.marginMm, 5)
  assert.equal(opts.borderMm, 3)
  assert.equal(opts.paddingMm, 0)
  assert.equal(opts.gapMm, 10)
  assert.equal(opts.growMm, 1)
  assert.equal(opts.allowRotate, true)
  assert.equal(resolveOptions({ gapMm: 1.237 }).gapMm, 1.2)
  assert.deepEqual(resolveOptions(), DEFAULT_OPTIONS)
})

test('growMm enlarges the labels and therefore fits fewer per page', () => {
  const plain = planStickerSheet([{ ...oneByOne, copies: 5000 }], { growMm: 0 })
  const grown = planStickerSheet([{ ...oneByOne, copies: 5000 }], { growMm: 0.5 })
  const perPage = (plan) => plan.pages[0].rows.reduce((n, r) => n + r.cells.length, 0)
  assert.ok(perPage(grown) < perPage(plain), `${perPage(grown)} vs ${perPage(plain)}`)
  assert.equal(plain.cells[0].w, 8)
  assert.equal(grown.cells[0].w, 9)
})

test('a label that cannot fit the sheet is refused with its name', () => {
  assert.throws(
    () => planStickerSheet([{ id: 'x', name: 'Huge 32×32', widthStuds: 32, depthStuds: 32, copies: 1 }]),
    (err) => {
      assert.ok(err instanceof StickerFitError)
      assert.deepEqual(err.stickers, ['Huge 32×32'])
      return true
    },
  )
  // Doing nothing at all is not an error.
  assert.equal(planStickerSheet([]).count, 0)
  // 24 × 32 studs (192 × 256 mm) is the largest footprint that still fits a default sheet.
  const biggest = planStickerSheet([{ id: 'big', name: 'Big', widthStuds: 24, depthStuds: 32, copies: 1 }])
  assert.equal(biggest.count, 1)
})
