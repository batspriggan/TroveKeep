// Unit tests for the volatile multi-selection and rigid group-drag maths shared by
// RoomPlannerView and AggregateBpPlannerView (`ui/src/utils/selection.js`).
// These are the parts of the feature that do not need a browser: which units a click
// or a marquee selects, and how a multi-unit drag is snapped, clamped and blocked.
// Run with `npm test` (node:test, no extra dependencies).
import test from 'node:test'
import assert from 'node:assert/strict'

import {
  bboxOf,
  closeOverUnits,
  collectUnitKeys,
  nextSelection,
  planRigidDrag,
  rectsOverlap,
} from '../src/utils/selection.js'

// ── Unit closure (selection granularity) ─────────────────────────────────────
// Two table aggregates: aggregate 0 = {t1, t2}, aggregate 1 = {t3}.
const aggregates = [['t1', 't2'], ['t3']]
const aggregateOf = (id) => {
  if (id === 't1' || id === 't2') return aggregates[0]
  if (id === 't3') return aggregates[1]
  return null
}

test('closeOverUnits selects the whole aggregate from one of its tables', () => {
  assert.deepEqual([...closeOverUnits(['t1'], aggregateOf)].sort(), ['t1', 't2'])
  // Already closed and idempotent.
  assert.deepEqual([...closeOverUnits(['t1', 't2'], aggregateOf)].sort(), ['t1', 't2'])
  // Unknown ids (a table deleted meanwhile) are dropped, never invented.
  assert.deepEqual([...closeOverUnits(['gone'], aggregateOf)], [])
  assert.deepEqual([...closeOverUnits(['t3', 'gone'], aggregateOf)], ['t3'])
})

// ── Click semantics: replace / toggle / add ───────────────────────────────────
const unit = (...ids) => new Set(ids)

test('nextSelection: a plain click replaces the selection with that unit', () => {
  const current = unit('t1', 't2')
  const next = nextSelection(current, unit('t3'))
  assert.deepEqual([...next], ['t3'])
  // Immutable: the current Set is left untouched (the views assign the new one).
  assert.deepEqual([...current].sort(), ['t1', 't2'])
})

test('nextSelection: Shift adds without removing', () => {
  const next = nextSelection(unit('t1', 't2'), unit('t3'), { add: true })
  assert.deepEqual([...next].sort(), ['t1', 't2', 't3'])
  // Adding an already selected unit is a no-op.
  assert.deepEqual([...nextSelection(next, unit('t3'), { add: true })].sort(), ['t1', 't2', 't3'])
})

test('nextSelection: Ctrl/Cmd toggles the whole unit', () => {
  // Not selected → added.
  assert.deepEqual([...nextSelection(unit('t1', 't2'), unit('t3'), { toggle: true })].sort(),
    ['t1', 't2', 't3'])
  // Fully selected → removed.
  assert.deepEqual([...nextSelection(unit('t1', 't2', 't3'), unit('t3'), { toggle: true })].sort(),
    ['t1', 't2'])
  // Partially selected unit → completed, never left half-selected.
  const partial = nextSelection(unit('t1'), unit('t1', 't2'), { toggle: true })
  assert.deepEqual([...partial].sort(), ['t1', 't2'])
})

// ── Marquee hits ──────────────────────────────────────────────────────────────
const rect = (x1, y1, x2, y2) => ({ x1, y1, x2, y2 })

// A MOC instance "m1" made of two plates, plus one standalone plate "p3".
const plates = [
  { instanceId: 'p1', unit: 'm1', box: rect(0, 0, 32, 32) },
  { instanceId: 'p2', unit: 'm1', box: rect(32, 0, 64, 32) },
  { instanceId: 'p3', unit: 'p3', box: rect(100, 100, 132, 132) },
]
const plateRect = (p) => p.box
const plateUnit = (p) => p.unit

test('collectUnitKeys: touching one plate selects its whole MOC instance', () => {
  const hits = collectUnitKeys(rect(8, 8, 16, 16), plates, plateUnit, plateRect)
  assert.deepEqual([...hits], ['m1'])
})

test('collectUnitKeys: a rect covering both units returns both keys once', () => {
  const hits = collectUnitKeys(rect(-10, -10, 200, 200), plates, plateUnit, plateRect)
  assert.deepEqual([...hits].sort(), ['m1', 'p3'])
})

test('collectUnitKeys: only touching an edge does not select (strict intersection)', () => {
  // Right edge of p1 (=32) is exactly the marquee's left edge.
  assert.equal(collectUnitKeys(rect(32, 0, 64, 32), [plates[0]], plateUnit, plateRect).size, 0)
})

test('collectUnitKeys: a plate without geometry (unknown baseplate) is skipped', () => {
  const noGeom = [{ instanceId: 'px', unit: 'mx' }]
  const hits = collectUnitKeys(rect(0, 0, 100, 100), noGeom, plateUnit, () => null)
  assert.equal(hits.size, 0)
})

// ── Helpers ───────────────────────────────────────────────────────────────────
test('bboxOf returns the union of the boxes and null for an empty list', () => {
  assert.equal(bboxOf([]), null)
  assert.deepEqual(
    bboxOf([{ x: 10, y: 20, w: 30, h: 40 }, { x: 50, y: 0, w: 10, h: 10 }]),
    { minX: 10, minY: 0, maxX: 60, maxY: 60 },
  )
})

test('rectsOverlap is strict: touching edges is not an overlap', () => {
  assert.equal(rectsOverlap(0, 0, 32, 32, 32, 0, 32, 32), false)
  assert.equal(rectsOverlap(0, 0, 32, 32, 31, 0, 32, 32), true)
  assert.equal(rectsOverlap(0, 0, 32, 32, 0, 0, 32, 32), true)
})

// ── Rigid group drag ──────────────────────────────────────────────────────────
const members = [
  { id: 't1', x: 100, y: 100, w: 80, h: 40 },
  { id: 't2', x: 180, y: 100, w: 80, h: 40 },
]
const ids = (plan) => plan.positions.map(p => p.id)

test('planRigidDrag moves every member by ONE snapped delta (grid 8 mm)', () => {
  // 19 mm of raw pointer travel snaps to 16 mm on the 8 mm stud grid.
  const plan = planRigidDrag({ startPositions: members, deltaX: 19, deltaY: -13, snap: 8 })
  assert.equal(plan.blocked, false)
  assert.equal(plan.dx, 16)
  assert.equal(plan.dy, -16)
  assert.deepEqual(ids(plan), ['t1', 't2'])
  assert.deepEqual(plan.positions, [
    { id: 't1', x: 116, y: 84 },
    { id: 't2', x: 196, y: 84 },
  ])
  // Rigid: the offset between members is unchanged.
  assert.equal(plan.positions[1].x - plan.positions[0].x, members[1].x - members[0].x)
  assert.equal(plan.positions[1].y - plan.positions[0].y, members[1].y - members[0].y)
})

test('planRigidDrag snaps on the 0.8 cm room grid too (1 cm of travel → 0.8 cm)', () => {
  const plan = planRigidDrag({ startPositions: members, deltaX: 1, deltaY: 0, snap: 0.8 })
  assert.equal(plan.dx, 0.8)
  assert.equal(plan.dy, 0)
  assert.equal(plan.positions[0].x, 100.8)
})

test('planRigidDrag without snap and without bounds applies the raw delta (baseplate canvas)', () => {
  const plan = planRigidDrag({
    startPositions: [{ id: 'p1', x: 0, y: 0, w: 32, h: 32 }],
    deltaX: -40,
    deltaY: 13,
  })
  assert.equal(plan.blocked, false)
  assert.deepEqual(plan.positions, [{ id: 'p1', x: -40, y: 13 }])
})

test('planRigidDrag clamps the whole group inside the room, one delta per axis', () => {
  // Group spans 100..260 in a 300 cm wide room; +50 would reach 310.
  const plan = planRigidDrag({
    startPositions: members,
    deltaX: 50,
    deltaY: 0,
    snap: 0.8,
    bounds: { width: 300, depth: 400 },
  })
  assert.equal(plan.blocked, false)
  assert.equal(plan.dx, 40)                    // 50 requested, 40 until the right wall
  assert.equal(plan.positions[1].x + members[1].w, 300)
  assert.equal(plan.positions[1].x - plan.positions[0].x, 80)
})

test('planRigidDrag stops the group at the left/top walls and stays rigid', () => {
  const plan = planRigidDrag({
    startPositions: members,
    deltaX: -500,
    deltaY: -500,
    snap: 0.8,
    bounds: { width: 300, depth: 400 },
  })
  assert.equal(plan.positions[0].x, 0)
  assert.equal(plan.positions[0].y, 0)
  assert.equal(plan.positions[1].x, 80)
  assert.equal(plan.positions[1].y, 0)
})

test('planRigidDrag blocks the whole group when a member would overlap a non-selected unit', () => {
  const obstacle = { x: 260, y: 100, w: 80, h: 40 }
  const plan = planRigidDrag({
    startPositions: members,
    deltaX: 40,
    deltaY: 0,
    snap: 0.8,
    bounds: { width: 600, depth: 400 },
    others: [obstacle],
  })
  assert.equal(plan.blocked, true)
  // The proposal is still reported so a caller can show *where* it was blocked, but a
  // blocked plan must never be applied.
  assert.deepEqual(plan.positions, [
    { id: 't1', x: 140, y: 100 },
    { id: 't2', x: 220, y: 100 },
  ])
})

test('planRigidDrag allows the group to slide flush against a non-selected unit', () => {
  // t2 ends at 260; the obstacle starts exactly there, so a +0 move stays valid and a
  // real overlap is the only thing that blocks.
  const plan = planRigidDrag({
    startPositions: members,
    deltaX: 0,
    deltaY: 0,
    others: [{ x: 260, y: 100, w: 80, h: 40 }],
  })
  assert.equal(plan.blocked, false)
  const touching = planRigidDrag({
    startPositions: members,
    deltaX: 8,
    deltaY: 0,
    snap: 8,
    others: [{ x: 260, y: 100, w: 80, h: 40 }],
  })
  assert.equal(touching.blocked, true)
})
