// Unit tests for the marquee / rigid-group-drag maths shared by RoomPlannerView and
// AggregateBpPlannerView. Run with `npm test` (node:test, no extra dependencies).
import test from 'node:test'
import assert from 'node:assert/strict'

import {
  MARQUEE_THRESHOLD_PX,
  normaliseMarquee,
  rectsIntersect,
  clampGroupShift,
} from '../src/utils/marquee.js'

test('normaliseMarquee orders the two dragged corners', () => {
  const expected = { x1: 10, y1: 20, x2: 110, y2: 220 }
  // Every drag direction yields the same normalised rectangle.
  assert.deepEqual(normaliseMarquee(10, 20, 110, 220), expected)
  assert.deepEqual(normaliseMarquee(110, 220, 10, 20), expected)
  assert.deepEqual(normaliseMarquee(110, 20, 10, 220), expected)
  assert.deepEqual(normaliseMarquee(10, 220, 110, 20), expected)
})

test('normaliseMarquee keeps a collapsed drag tiny (threshold decides, not the rect)', () => {
  assert.deepEqual(normaliseMarquee(5, 5, 5, 5), { x1: 5, y1: 5, x2: 5, y2: 5 })
})

test('MARQUEE_THRESHOLD_PX is the 3 px designed threshold', () => {
  assert.equal(MARQUEE_THRESHOLD_PX, 3)
})

test('rectsIntersect detects real interior overlap', () => {
  const a = { x1: 0, y1: 0, x2: 100, y2: 100 }
  assert.equal(rectsIntersect(a, { x1: 50, y1: 50, x2: 150, y2: 150 }), true)
  // Fully contained.
  assert.equal(rectsIntersect(a, { x1: 10, y1: 10, x2: 20, y2: 20 }), true)
  // Marquee inside the element.
  assert.equal(rectsIntersect({ x1: 10, y1: 10, x2: 20, y2: 20 }, a), true)
})

test('rectsIntersect is strict: touching an edge is not a hit', () => {
  const a = { x1: 0, y1: 0, x2: 100, y2: 100 }
  // Right edge of `a` touches the left edge of the marquee.
  assert.equal(rectsIntersect(a, { x1: 100, y1: 0, x2: 200, y2: 100 }), false)
  // Bottom edge of `a` touches the top edge of the marquee.
  assert.equal(rectsIntersect(a, { x1: 0, y1: 100, x2: 100, y2: 200 }), false)
  // Disjoint and a zero-area rectangle sitting on the border.
  assert.equal(rectsIntersect(a, { x1: 101, y1: 0, x2: 200, y2: 100 }), false)
  assert.equal(rectsIntersect(a, { x1: 100, y1: 50, x2: 100, y2: 60 }), false)
})

test('clampGroupShift leaves a group already inside the room untouched', () => {
  const bbox = { minX: 10, minY: 10, maxX: 110, maxY: 210 }
  assert.deepEqual(clampGroupShift(bbox, 300, 400), { dx: 0, dy: 0 })
  // Exactly against the walls is still valid.
  assert.deepEqual(clampGroupShift({ minX: 0, minY: 0, maxX: 300, maxY: 400 }, 300, 400), { dx: 0, dy: 0 })
})

test('clampGroupShift pushes a group back inside the left/top walls', () => {
  const bbox = { minX: -8, minY: -2, maxX: 92, maxY: 98 }
  assert.deepEqual(clampGroupShift(bbox, 300, 400), { dx: 8, dy: 2 })
})

test('clampGroupShift pulls a group back inside the right/bottom walls', () => {
  const bbox = { minX: 250, minY: 340, maxX: 305, maxY: 404 }
  assert.deepEqual(clampGroupShift(bbox, 300, 400), { dx: -5, dy: -4 })
})

test('clampGroupShift corrects both axes independently with ONE rigid delta', () => {
  const bbox = { minX: -5, minY: 350, maxX: 60, maxY: 410 }
  assert.deepEqual(clampGroupShift(bbox, 300, 400), { dx: 5, dy: -10 })
})

test('clampGroupShift handles both walls violated on the same axis (group wider than the room)', () => {
  const bbox = { minX: -10, minY: 0, maxX: 320, maxY: 50 }
  // Best effort: a single delta, so the result is clamped to one of the two walls.
  const { dx } = clampGroupShift(bbox, 300, 400)
  assert.equal(dx, -20) // right wall wins: maxX is brought back to roomWidth
  assert.equal(bbox.minX + dx, -30)
})

test('clampGroupShift never shifts a group past the far wall', () => {
  for (const bbox of [
    { minX: -3, minY: 0, maxX: 60, maxY: 50 },
    { minX: 270, minY: 380, maxX: 330, maxY: 430 },
    { minX: 40, minY: 40, maxX: 140, maxY: 140 },
  ]) {
    const { dx, dy } = clampGroupShift(bbox, 300, 400)
    // Applying the delta to every member keeps the group rigid, and at least one
    // edge lands on (or inside) a wall.
    assert.ok(bbox.minX + dx >= 0 || bbox.maxX + dx <= 300)
    assert.ok(bbox.minY + dy >= 0 || bbox.maxY + dy <= 400)
  }
})
