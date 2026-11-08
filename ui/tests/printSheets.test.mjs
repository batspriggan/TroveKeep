// Unit tests for the shared print-sheet geometry: A4 tiling for multi-page exports and
// the baseplate placeholder grid drawn on the planners' PDFs.
import test from 'node:test'
import assert from 'node:assert/strict'

import {
  gridSlots,
  marginForTile,
  parseBpKey,
  planPlateGrid,
  planSheets,
  plateGeometry,
  sheetClip,
  sheetGrid,
} from '../src/utils/printSheets.js'

test('planSheets keeps a landscape diagram on a landscape page', () => {
  const land = planSheets(4000, 2500, 1, 15)
  assert.equal(land.landscape, true)
  assert.equal(land.pageW, 297)
  assert.equal(land.pageH, 210)

  const port = planSheets(2500, 4000, 1, 15)
  assert.equal(port.landscape, false)
  assert.equal(port.pageW, 210)
  assert.equal(port.pageH, 297)
})

test('a 2×2 export renders one sheet the size of four A4 pages, then cuts it', () => {
  const plan = planSheets(4000, 2500, 2, 10)
  // The big sheet: A4 (landscape) × 2 in both directions.
  assert.equal(plan.sheetW, 594)
  assert.equal(plan.sheetH, 420)
  // Each printed page still gets exactly one A4 viewport.
  assert.deepEqual(plan.viewport, { x: 10, y: 10, w: 277, h: 190 })
})

test('sheetClip slices the big sheet into A4-sized pieces, in reading order', () => {
  const plan = planSheets(4000, 2500, 2, 10)
  const tl = sheetClip(plan, 0)
  const tr = sheetClip(plan, 1)
  const bl = sheetClip(plan, 2)
  const br = sheetClip(plan, 3)

  assert.deepEqual([tl.col, tl.row], [0, 0])
  assert.deepEqual([tr.col, tr.row], [1, 0])
  assert.deepEqual([bl.col, bl.row], [0, 1])
  assert.deepEqual([br.col, br.row], [1, 1])

  // Every slice is exactly the viewport size — that is what makes it a cut, not a scale.
  for (const c of [tl, tr, bl, br]) {
    assert.equal(c.rect.w, plan.viewport.w)
    assert.equal(c.rect.h, plan.viewport.h)
  }
  assert.deepEqual(tl.rect, { x: plan.viewport.x, y: plan.viewport.y, w: plan.viewport.w, h: plan.viewport.h })
  assert.equal(tr.rect.x, plan.viewport.x + plan.viewport.w)
  assert.equal(bl.rect.y, plan.viewport.y + plan.viewport.h)
})

test('sheetGrid keeps every page when the drawing fills them all', () => {
  const plan = planSheets(4000, 4000, 3, 10)
  const grid = sheetGrid(plan, { x: 10, y: 25, w: 570, h: 831 })
  assert.deepEqual([grid.cols, grid.rows], [3, 3])
  assert.deepEqual(gridSlots(grid), [0, 1, 2, 3, 4, 5, 6, 7, 8])
})

test('sheetGrid drops a last column or row the drawing only clips', () => {
  const plan = planSheets(4000, 4000, 3, 10)
  // 395 mm wide = 2 full slices (380) + 15 mm of a third: dropped.
  const grid = sheetGrid(plan, { x: 10, y: 25, w: 395, h: 554 })
  assert.deepEqual([grid.cols, grid.rows], [2, 2])
  assert.deepEqual(gridSlots(grid), [0, 1, 3, 4])
})

test('sheetGrid keeps a page that is more than half covered', () => {
  const plan = planSheets(4000, 4000, 3, 10)
  // One full slice is 190 mm: 290 mm reaches 100 mm into the second page, over half.
  const grid = sheetGrid(plan, { x: 10, y: 25, w: 290, h: 200 })
  assert.equal(grid.cols, 2)
  // 250 mm reaches only 60 mm into it: dropped, and the smaller grid is kept.
  assert.equal(sheetGrid(plan, { x: 10, y: 25, w: 250, h: 200 }).cols, 1)
})

test('sheetGrid keeps the full limit for a drawing smaller than one page', () => {
  const plan = planSheets(4000, 4000, 3, 10)
  // 300 mm does not fill a single 277 mm-high page, but shrinking to 1×2 would not help:
  // the smaller grid is still full, so it is kept.
  const grid = sheetGrid(plan, { x: 10, y: 25, w: 300, h: 300 })
  assert.deepEqual([grid.cols, grid.rows], [2, 1])
})

test('margins shrink once the export is glued from several sheets', () => {
  assert.equal(marginForTile(1), 15)
  assert.equal(marginForTile(2), 10)
  assert.equal(marginForTile(3), 10)
})

test('parseBpKey reads the canonical stud pair and rejects anything else', () => {
  assert.deepEqual(parseBpKey('16x32'), { widthStuds: 16, depthStuds: 32 })
  assert.equal(parseBpKey(''), null)
  assert.equal(parseBpKey('16'), null)
  assert.equal(parseBpKey('16×32'), null)
  assert.equal(parseBpKey('0x32'), null)
  assert.equal(parseBpKey(null), null)
})

test('plateGeometry applies the 0.2 mm clearance per side for standard geometry', () => {
  // 32 studs × 8 mm − 0.2 mm — the size `calcPlateCount` counts with.
  assert.deepEqual(plateGeometry(32, 32, 'Standard'), { widthMm: 255.8, depthMm: 255.8 })
  assert.deepEqual(plateGeometry(16, 32, 'Road'), { widthMm: 127.8, depthMm: 255.8 })
})

test('plateGeometry falls back to the 2 mm clearance for non-standard Custom plates', () => {
  // 16 and 40 studs are both multiples of 8, so only a size off the grid is custom.
  assert.deepEqual(plateGeometry(15, 41, 'Custom'), { widthMm: 118, depthMm: 326 })
  // A Custom plate whose studs are multiples of 8 is regular geometry after all.
  assert.deepEqual(plateGeometry(16, 32, 'Custom'), { widthMm: 127.8, depthMm: 255.8 })
})

test('planPlateGrid lays out only whole plates that fit', () => {
  const plate = plateGeometry(32, 32, 'Standard')
  // 8 × 4 plates exactly.
  const exact = planPlateGrid({ xMm: 0, yMm: 0, widthMm: 2046.4, depthMm: 1023.2 }, plate)
  assert.equal(exact.tiles.length, 8 * 4)
  for (const t of exact.tiles) {
    assert.ok(Math.abs(t.widthMm - plate.widthMm) < 1e-9)
    assert.ok(Math.abs(t.depthMm - plate.depthMm) < 1e-9)
  }
})

test('planPlateGrid drops a plate that would hang over the edge instead of truncating it', () => {
  const plate = { widthMm: 100, depthMm: 100 }
  // 250 × 130 fits only 2 × 1 whole plates: the 50 mm and 30 mm leftovers stay empty.
  const grid = planPlateGrid({ xMm: 0, yMm: 0, widthMm: 250, depthMm: 130 }, plate)
  assert.equal(grid.tiles.length, 2)
  for (const t of grid.tiles) {
    assert.equal(t.widthMm, 100)
    assert.equal(t.depthMm, 100)
  }
})

test('planPlateGrid centres the plates on the area, splitting the leftover evenly', () => {
  const plate = { widthMm: 100, depthMm: 100 }
  // 2 plates in 250 → 50 mm leftover → 25 mm on each side; 1 row in 130 → 15 mm top and bottom.
  const grid = planPlateGrid({ xMm: 0, yMm: 0, widthMm: 250, depthMm: 130 }, plate)
  assert.deepEqual(grid.tiles.map(t => [t.xMm, t.yMm]), [[25, 15], [125, 15]])
})

test('planPlateGrid keeps the area origin when centring', () => {
  const grid = planPlateGrid({ xMm: 100, yMm: 50, widthMm: 150, depthMm: 150 }, { widthMm: 100, depthMm: 100 })
  // Nothing fits in either direction: a single plate would overhang.
  assert.equal(grid.tiles.length, 1)
  assert.deepEqual([grid.tiles[0].xMm, grid.tiles[0].yMm], [125, 75])
})

test('planPlateGrid returns nothing when a plate is larger than the area', () => {
  const grid = planPlateGrid({ xMm: 0, yMm: 0, widthMm: 200, depthMm: 100 }, { widthMm: 300, depthMm: 100 })
  assert.deepEqual(grid.tiles, [])
})

test('planPlateGrid tolerates a degenerate area or plate and returns no tiles', () => {
  assert.deepEqual(planPlateGrid({ xMm: 0, yMm: 0, widthMm: 0, depthMm: 100 }, { widthMm: 100, depthMm: 100 }).tiles, [])
  assert.deepEqual(planPlateGrid({ xMm: 0, yMm: 0, widthMm: 100, depthMm: 100 }, { widthMm: 0, depthMm: 100 }).tiles, [])
})
