// Unit tests for the sticker-sheet engine: 1:1 A4 packing of stud-sized labels.
// These are the numbers the printed sheet depends on, so they are pinned here.
import test from 'node:test'
import assert from 'node:assert/strict'

import {
  DEFAULT_OPTIONS,
  PAGE,
  STICKER_BRICK_MM,
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

/** Bounding box of a set of cells, in mm. */
function boundsOf(cells) {
  const x0 = Math.min(...cells.map((c) => c.x))
  const y0 = Math.min(...cells.map((c) => c.y))
  const x1 = Math.max(...cells.map((c) => c.x + c.w))
  const y1 = Math.max(...cells.map((c) => c.y + c.h))
  return { x0, y0, x1, y1, w: x1 - x0, h: y1 - y0 }
}

/**
 * The packing invariant, gap included: on one page, two cut boxes are at least `step` apart
 * along x or along y. The packer reserves the gap as part of the label it places, so this is
 * stronger than "the boxes do not overlap": the cut lines never get closer than the step.
 */
function assertNoOverlap(plan, label = '') {
  for (const page of plan.pages) {
    const cells = page.cells
    for (let i = 0; i < cells.length; i++) {
      for (let j = i + 1; j < cells.length; j++) {
        const a = cells[i]
        const b = cells[j]
        const apart =
          a.x + a.w + plan.step <= b.x + EPS ||
          b.x + b.w + plan.step <= a.x + EPS ||
          a.y + a.h + plan.step <= b.y + EPS ||
          b.y + b.h + plan.step <= a.y + EPS
        assert.ok(
          apart,
          `${label}cells ${i} and ${j} overlap or break the gap on page ${page.index}`,
        )
      }
    }
  }
}

test('a stud is 8 mm, so a 2×4 tile is a 16×32 mm label', () => {
  assert.equal(STUD_MM, 8)
  assert.equal(STICKER_BRICK_MM, 9.6)
  assert.deepEqual(stickerSizeMm({ widthStuds: 2, depthStuds: 4 }, resolveOptions()), { w: 16, h: 32 })
  // The label size is not an option: no sheet setting can change it.
  assert.deepEqual(
    stickerSizeMm({ widthStuds: 2, depthStuds: 4 }, resolveOptions({ borderMm: 3, paddingMm: 3, gapMm: 10 })),
    { w: 16, h: 32 },
  )
})

test('a brick stack is sized by its facade length and its height in bricks', () => {
  const options = resolveOptions()
  const stack = { mount: 'BrickStack', widthStuds: 8, depthStuds: 1, brickHeight: 6 }

  // 8 studs × 64 mm wide, 6 bricks × 9.6 mm = 57.6 mm high.
  assert.deepEqual(stickerSizeMm(stack, options), { w: 64, h: 57.6 })
  // Still exactly the piece when the sheet options change around it.
  assert.deepEqual(
    stickerSizeMm(stack, resolveOptions({ borderMm: 3, paddingMm: 3, gapMm: 10 })),
    { w: 64, h: 57.6 },
  )

  // 1.33 bricks × 9.6 mm = 12.768 mm, printed at the 2 decimals the backend reports: 12.77 mm.
  assert.equal(
    stickerSizeMm({ mount: 'BrickStack', widthStuds: 4, brickHeight: 1.33 }, options).h,
    12.77,
  )

  // The mount decides the second axis: a stack ignores the depth, a tile ignores the brick height.
  assert.deepEqual(
    stickerSizeMm({ mount: 'BrickStack', widthStuds: 1, depthStuds: 8, brickHeight: 6 }, options),
    { w: 8, h: 57.6 },
  )
  assert.deepEqual(
    stickerSizeMm({ mount: 'Tile', widthStuds: 1, depthStuds: 8, brickHeight: 6 }, options),
    { w: 8, h: 64 },
  )
  // A missing mount is a tile, exactly like the backend default.
  assert.deepEqual(stickerSizeMm({ widthStuds: 2, depthStuds: 4 }, options), { w: 16, h: 32 })
})

test('a brick-stack plan is packed like a tile of the same size', () => {
  const plan = planStickerSheet([
    { id: 'a', name: 'Stack', mount: 'BrickStack', widthStuds: 8, depthStuds: 1, brickHeight: 6, copies: 4 },
    { id: 'b', name: 'Tile', widthStuds: 2, depthStuds: 4, copies: 4 },
  ])
  assert.equal(plan.count, 8)
  assert.equal(plan.pages.length, 1)
  assertNoOverlap(plan, 'stack: ')

  const stacks = plan.cells.filter((cell) => cell.stickerId === 'a')
  assert.equal(stacks.length, 4)
  for (const cell of stacks) {
    assert.equal(cell.w, 64)
    assert.equal(cell.h, 57.6)
    // The artwork area keeps the same inner-border inset as any other label: the black cut
    // border is drawn outside the label, so it steals nothing from the artwork.
    assert.equal(cell.graphic.w, cell.w - 1.2)
    assert.equal(cell.graphic.h, cell.h - 1.2)
  }
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

test('no two labels overlap, and the cut gap is always kept', () => {
  const plan = planStickerSheet(
    [
      { id: 'a', name: 'A', widthStuds: 2, depthStuds: 4, copies: 40 },
      { id: 'b', name: 'B', widthStuds: 1, depthStuds: 1, copies: 200 },
      { id: 'c', name: 'C', widthStuds: 3, depthStuds: 2, copies: 15 },
    ],
    { gapMm: 1.5, allowRotate: true },
  )
  assertNoOverlap(plan, 'mixed: ')
  // Two cut boxes are at least one step apart: no two share a border.
  assert.ok(plan.cells.length > 0)
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

test('the artwork area is the label inset by the inner border only', () => {
  const plan = planStickerSheet([{ ...oneByOne, copies: 1 }], {
    borderMm: 0.6,
    paddingMm: 0.6,
  })
  const cell = plan.cells[0]
  const near = (actual, expected) => assert.ok(Math.abs(actual - expected) < EPS, `${actual} ≉ ${expected}`)
  // The black cut border is drawn OUTSIDE the label (a cutting guide, not sticker), so it steals
  // no room from the artwork: only the inner border insets it.
  near(cell.graphic.x - cell.x, 0.6)
  near(cell.graphic.y - cell.y, 0.6)
  near(cell.w - cell.graphic.w, 1.2)
  near(cell.h - cell.graphic.h, 1.2)

  // The inner border is the configurable part: 0 means the artwork runs to the cut line.
  const tight = planStickerSheet([{ ...oneByOne, copies: 1 }], { borderMm: 0.6, paddingMm: 0 })
  near(tight.cells[0].graphic.x - tight.cells[0].x, 0)
  near(tight.cells[0].graphic.w - tight.cells[0].w, 0)

  // The cut box itself is the label: nothing is drawn inside it beyond the artwork.
  assert.deepEqual(cell.cut, { x: cell.x, y: cell.y, w: cell.w, h: cell.h })
})

test('the gap between cuts is at least twice the border, so the borders never touch', () => {
  assert.equal(cutSpacing(resolveOptions({ borderMm: 0.6, gapMm: 0.2 })), 1.2)
  assert.equal(cutSpacing(resolveOptions({ borderMm: 0.6, gapMm: 2.5 })), 2.5)
  const plan = planStickerSheet([{ ...oneByOne, copies: 400 }], { borderMm: 1, gapMm: 0 })
  assert.equal(plan.step, 2)
  // Two neighbours of the first row are exactly one step apart.
  const first = plan.pages[0].cells[0]
  const next = plan.pages[0].cells.find((c) => Math.abs(c.y - first.y) < EPS && c.x > first.x)
  assert.equal(next.x - (first.x + first.w), 2)
})

test('options are clamped to their accepted range and rounded to a tenth', () => {
  const opts = resolveOptions({
    marginMm: 2,
    borderMm: 99,
    paddingMm: -3,
    gapMm: 40,
    allowRotate: 1,
  })
  assert.equal(opts.marginMm, 5)
  assert.equal(opts.borderMm, 3)
  assert.equal(opts.paddingMm, 0)
  assert.equal(opts.gapMm, 10)
  assert.equal(opts.allowRotate, true)
  assert.equal(resolveOptions({ gapMm: 1.237 }).gapMm, 1.2)
  assert.deepEqual(resolveOptions(), DEFAULT_OPTIONS)
})

test('no sheet option can change the label size', () => {
  const plain = planStickerSheet([{ ...oneByOne, copies: 5000 }], { borderMm: 0, paddingMm: 0 })
  const padded = planStickerSheet([{ ...oneByOne, copies: 5000 }], { borderMm: 3, paddingMm: 3 })
  assert.equal(plain.cells[0].w, 8)
  assert.equal(padded.cells[0].w, 8)
  // Only the artwork area shrinks, and therefore fewer labels fit.
  assert.ok(padded.cells[0].graphic.w < plain.cells[0].graphic.w)
  assert.ok(padded.pages[0].cells.length <= plain.pages[0].cells.length)
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

// The sheet that reproduced the problem (see the task): two 2×4 artworks, one 2×8 and one 2×2,
// 25 labels in total, default options (margin 8, border 0.6, padding 0.6, gap 2.5, no rotation).
const reproducingItems = [
  { id: 'a', name: 'A 2×4', widthStuds: 2, depthStuds: 4, copies: 5 },
  { id: 'b', name: 'B 2×4', widthStuds: 2, depthStuds: 4, copies: 5 },
  { id: 'c', name: 'C 2×8', widthStuds: 2, depthStuds: 8, copies: 5 },
  { id: 'd', name: 'D 2×2', widthStuds: 2, depthStuds: 2, copies: 10 },
]

test('the reproducing sheet fits one page as a tight block instead of three shelves', () => {
  const plan = planStickerSheet(reproducingItems)
  assert.equal(plan.count, 25)
  assert.equal(plan.pages.length, 1)
  assertNoOverlap(plan, 'reproducing: ')

  const labels = plan.cells.reduce((n, c) => n + c.w * c.h, 0)
  assert.equal(labels, 12800) // 5·16·64 + 10·16·32 + 10·16·16, the same 25 labels as before
  const block = boundsOf(plan.cells)
  const density = labels / (block.w * block.h)

  // Bottom edge of the used block: 93 mm here, 125 mm with the old shelf packer (three shelves
  // of 64/32/16 mm). Block density: 12 800 / (182.5 × 85) = 0.8251, against
  // 12 800 / (182.5 × 117) = 0.599 with the shelves.
  //
  // Why not 85 mm / 0.85: those two numbers are not reachable on this dataset by ANY packing.
  // Every label is 16 mm wide and the cut gap is 2.5 mm, so the 194 mm content column holds at
  // most 10 of them; the 25 labels carry 800 mm of height, plus 25 − 10 = 15 mandatory gaps of
  // 2.5 mm = 837.5 mm, i.e. at least 83.75 mm per column, and the discrete label heights push
  // the optimum to 85 mm (5 × (64+16+2.5) beside 5 × (32+32+16+5)). 12 800 / (182.5 × 85) is
  // exactly 0.8251, so this plan is at the arithmetic optimum: the assertions below are the
  // real ceiling, not a target that was lowered because the packer fell short.
  assert.ok(block.y1 <= 93 + EPS, `bottom edge ${block.y1} mm`)
  assert.ok(density >= 0.825, `density ${density}`)
  assert.equal(block.w, 182.5)
  assert.equal(block.h, 85)

  // Every label keeps its own footprint and the artwork inset of the default options.
  for (const cell of plan.cells) {
    assert.equal(cell.cut.x, cell.x)
    assert.equal(cell.graphic.w, cell.w - 1.2)
    assert.equal(cell.graphic.h, cell.h - 1.2)
  }
})

test('the no-overlap / gap invariant holds on mixed datasets, including a paginated one', () => {
  const datasets = [
    // Default options, one artwork repeated: a plain grid, fully deterministic.
    { items: [{ id: 'x', name: 'X', widthStuds: 2, depthStuds: 2, copies: 60 }], options: {} },
    // No gap and no border: labels touch, but still never overlap.
    {
      items: [
        { id: 'a', name: 'A', widthStuds: 1, depthStuds: 1, copies: 300 },
        { id: 'b', name: 'B', widthStuds: 4, depthStuds: 1, copies: 20 },
      ],
      options: { borderMm: 0, paddingMm: 0, gapMm: 0 },
    },
    // Rotation allowed, a thick border and a wide margin.
    {
      items: [
        { id: 'a', name: 'A', widthStuds: 2, depthStuds: 8, copies: 12 },
        { id: 'b', name: 'B', widthStuds: 3, depthStuds: 3, copies: 7 },
        { id: 'c', name: 'C', widthStuds: 8, depthStuds: 2, copies: 9 },
      ],
      options: { allowRotate: true, borderMm: 0.4, marginMm: 12, gapMm: 1.5 },
    },
    // Must span two pages (192 × 256 mm labels, a single one per sheet).
    {
      items: [
        { id: 'big', name: 'Big', widthStuds: 24, depthStuds: 32, copies: 3 },
        { id: 'small', name: 'Small', widthStuds: 2, depthStuds: 2, copies: 4 },
      ],
      options: { gapMm: 2.5 },
    },
    // Found by fuzzing the first version of the skyline: a label that ends up LEFT of an
    // already placed taller neighbour while its gap band crosses it. The height must be read
    // over the whole foot print including the gap, or the neighbour is pushed nowhere and the
    // cut gap shrinks (and the skyline under it can even be lowered).
    {
      items: [
        { id: 'a', name: 'A', widthStuds: 2, depthStuds: 8, copies: 8 },
        { id: 'b', name: 'B', widthStuds: 7, depthStuds: 1, copies: 8 },
        { id: 'c', name: 'C', widthStuds: 1, depthStuds: 1, copies: 6 },
      ],
      options: { gapMm: 5.5, marginMm: 15, borderMm: 1 },
    },
  ]

  for (const [index, dataset] of datasets.entries()) {
    const plan = planStickerSheet(dataset.items, dataset.options)
    assertNoOverlap(plan, `dataset ${index}: `)
    // …and nothing escapes the content box.
    for (const cell of plan.cells) {
      assert.ok(cell.x >= plan.content.x - EPS)
      assert.ok(cell.y >= plan.content.y - EPS)
      assert.ok(cell.x + cell.w <= plan.content.x + plan.content.w + EPS)
      assert.ok(cell.y + cell.h <= plan.content.y + plan.content.h + EPS)
    }
  }

  // The paginated dataset really is paginated, and each copy lands exactly once.
  const paginated = planStickerSheet(datasets[3].items, datasets[3].options)
  assert.ok(paginated.pages.length >= 2)
  assert.equal(paginated.count, 7)
  assert.deepEqual(
    paginated.pages.map((p) => p.cells.length).reduce((a, b) => a + b, 0),
    7,
  )
  assert.deepEqual(
    paginated.cells.map((c) => `${c.stickerId}#${c.copy}`).sort(),
    ['big#0', 'big#1', 'big#2', 'small#0', 'small#1', 'small#2', 'small#3'],
  )
})

test('two consecutive plans are identical', () => {
  const first = planStickerSheet(reproducingItems, { allowRotate: true, gapMm: 1.8 })
  const second = planStickerSheet(reproducingItems, { allowRotate: true, gapMm: 1.8 })
  assert.deepEqual(first, second)
  // The order the items arrive in is not part of the output: the engine sorts them.
  const reversed = planStickerSheet([...reproducingItems].reverse(), { allowRotate: true, gapMm: 1.8 })
  assert.deepEqual(first, reversed)
})

test('rotation stays a fallback: a label that fits upright is never turned', () => {
  const fits = planStickerSheet(
    [
      { id: 'a', name: 'A', widthStuds: 2, depthStuds: 4, copies: 12 },
      { id: 'b', name: 'B', widthStuds: 2, depthStuds: 2, copies: 12 },
      { id: 'c', name: 'C', widthStuds: 1, depthStuds: 8, copies: 6 },
    ],
    { allowRotate: true },
  )
  assert.equal(fits.cells.some((cell) => cell.rotated), false)

  // A 32-stud-wide label (256 mm) does not fit the 194 mm content column upright: only then
  // is it turned, and only that label.
  const forced = planStickerSheet(
    [
      { id: 'w', name: 'Wide', widthStuds: 32, depthStuds: 4, copies: 1 },
      { id: 'n', name: 'Normal', widthStuds: 2, depthStuds: 4, copies: 1 },
    ],
    { allowRotate: true },
  )
  assert.deepEqual(
    forced.cells.filter((cell) => cell.rotated).map((cell) => cell.stickerId),
    ['w'],
  )
  // With rotation off the same label is refused instead of being turned silently.
  assert.throws(
    () => planStickerSheet([{ id: 'w', name: 'Wide', widthStuds: 32, depthStuds: 4, copies: 1 }]),
    (err) => err instanceof StickerFitError && err.stickers.includes('Wide'),
  )
})

test('every unfit label is named once, sorted, and no copy is dropped silently', () => {
  assert.throws(
    () =>
      planStickerSheet([
        { id: 'z', name: 'Zeta huge', widthStuds: 32, depthStuds: 32, copies: 2 },
        { id: 'a', name: 'Alpha huge', widthStuds: 30, depthStuds: 30, copies: 1 },
        { id: 'ok', name: 'Fine', widthStuds: 2, depthStuds: 4, copies: 3 },
      ]),
    (err) => {
      assert.ok(err instanceof StickerFitError)
      assert.deepEqual(err.stickers, ['Alpha huge', 'Zeta huge'])
      assert.match(err.message, /Alpha huge/)
      return true
    },
  )
})

test('a deterministic pseudo-random sweep never breaks the packing invariants', () => {
  // A small LCG instead of Math.random, so a failure here is always reproducible from the seed
  // and two runs of the suite check the same 150 sheets.
  let seed = 20260101
  const random = () => {
    seed = (seed * 1103515245 + 12345) % 2147483648
    return seed / 2147483648
  }

  for (let round = 0; round < 150; round++) {
    const items = Array.from({ length: 1 + Math.floor(random() * 6) }, (_, index) => ({
      id: `r${round}-${index}`,
      name: `Label ${index}`,
      widthStuds: 1 + Math.floor(random() * 8),
      depthStuds: 1 + Math.floor(random() * 8),
      copies: 1 + Math.floor(random() * 10),
    }))
    const options = {
      marginMm: 5 + random() * 15,
      borderMm: random() * 3,
      paddingMm: random() * 3,
      gapMm: random() * 10,
      allowRotate: random() > 0.5,
    }

    let plan
    try {
      plan = planStickerSheet(items, options)
    } catch (error) {
      // Only the documented refusal, and only for labels that really cannot fit any page.
      assert.ok(error instanceof StickerFitError, `round ${round}: ${error.message}`)
      continue
    }

    assertNoOverlap(plan, `round ${round}: `)
    assert.deepEqual(plan, planStickerSheet(items, options), `round ${round}: not deterministic`)
    assert.equal(plan.count, items.reduce((n, item) => n + item.copies, 0), `round ${round}: lost copies`)
    assert.ok(plan.pages.every((page) => page.cells.length > 0), `round ${round}: empty page`)
    for (const cell of plan.cells) {
      assert.ok(cell.page >= 0 && cell.page < plan.pages.length)
      assert.ok(cell.x >= plan.content.x - EPS && cell.y >= plan.content.y - EPS)
      assert.ok(cell.x + cell.w <= plan.content.x + plan.content.w + EPS)
      assert.ok(cell.y + cell.h <= plan.content.y + plan.content.h + EPS)
    }
  }
})
