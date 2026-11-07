// Volatile multi-selection and rigid group-drag maths, shared by the two table
// planners (`RoomPlannerView` for table aggregates, `AggregateBpPlannerView` for
// baseplate MOC/set instances).
//
// A selection is a plain `Set` of unit ids kept in component state: it is never part
// of the persisted layout (`Room.Layout` / `PlacedBaseplates` stay per-element) and it
// only ever enables *movement* — rotation and removal stay single-unit actions.
//
// Everything worth proving without a browser lives here: unit closure (one table
// selects its whole aggregate, one plate its whole MOC instance), click semantics,
// marquee hits, and the rigid-group-drag proposal (grid snap + wall clamp + overlap
// blocking).

import { clampGroupShift, rectsIntersect } from './marquee.js'

/** Positions are centimetres/millimetres with 2 decimals of precision. */
export function round2(n) {
  return Math.round(n * 100) / 100
}

/**
 * Strict interior overlap of two axis-aligned boxes given as (x, y, w, h).
 * Touching edges is allowed, exactly like `rectsIntersect`: two units flush against
 * each other do not count as overlapping.
 */
export function rectsOverlap(ax, ay, aw, ah, bx, by, bw, bh) {
  return ax < bx + bw && ax + aw > bx && ay < by + bh && ay + ah > by
}

/**
 * Collapse an arbitrary id set onto whole units: `membersOf(id)` returns every id of
 * the same unit (all tables of a table aggregate, all plates of a MOC instance) or a
 * falsy value for an id that no longer exists. Ids are never invented, so the result
 * is always a subset of the current layout.
 */
export function closeOverUnits(ids, membersOf) {
  const out = new Set()
  for (const id of ids) {
    const members = membersOf(id)
    if (!members) continue
    for (const member of members) out.add(member)
  }
  return out
}

/**
 * Selection after a click on `unitIds` (a whole unit, already closed over its
 * members):
 *  - plain click            → replace the selection with that unit;
 *  - `toggle` (Ctrl/Cmd)    → remove the unit if it was fully selected, else add it;
 *  - `add` (Shift)          → add it, never remove.
 * Always returns a new Set: the views assign it, which is what makes Vue re-render.
 */
export function nextSelection(current, unitIds, { toggle = false, add = false } = {}) {
  const next = new Set(current)
  if (toggle) {
    const allSelected = [...unitIds].every(id => next.has(id))
    for (const id of unitIds) {
      if (allSelected) next.delete(id)
      else next.add(id)
    }
  } else if (add) {
    for (const id of unitIds) next.add(id)
  } else {
    next.clear()
    for (const id of unitIds) next.add(id)
  }
  return next
}

/**
 * Unit keys touched by a marquee rectangle. `rectOf(element)` returns the element's
 * canvas-px rectangle `{ x1, y1, x2, y2 }`; the intersection is strict, so merely
 * grazing an element with the rubber band does not select it.
 *
 * The result holds unit keys only (one key per aggregate / MOC instance): touching a
 * single table of an aggregate or a single plate of a MOC instance selects the whole
 * unit. `null` rectangles (unknown template / baseplate) are skipped.
 */
export function collectUnitKeys(rect, elements, unitKeyOf, rectOf) {
  const out = new Set()
  for (const el of elements) {
    const r = rectOf(el)
    if (r && rectsIntersect(rect, r)) out.add(unitKeyOf(el))
  }
  return out
}

/**
 * Union bounding box of boxes given as (x, y, w, h) → `{ minX, minY, maxX, maxY }`,
 * or `null` for an empty list. Same shape as the one `clampGroupShift` expects.
 */
export function bboxOf(boxes) {
  let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity
  for (const b of boxes) {
    if (!b) continue
    minX = Math.min(minX, b.x)
    minY = Math.min(minY, b.y)
    maxX = Math.max(maxX, b.x + b.w)
    maxY = Math.max(maxY, b.y + b.h)
  }
  return isFinite(minX) ? { minX, minY, maxX, maxY } : null
}

/**
 * Rigid group drag: every member follows the pointer by ONE delta, so the group can
 * never deform.
 *
 *  - the raw pointer delta is snapped to `snap` (0/undefined = no grid) *before* it is
 *    applied, exactly like the single-unit drags snap their delta;
 *  - with `bounds` ({ width, depth }) the proposed bounding box is clamped with
 *    `clampGroupShift`, i.e. one uniform correction per axis, so the group stops at
 *    the walls instead of spilling out of the room. Without `bounds` there is no
 *    clamp (the baseplate canvas has no walls);
 *  - the move is rejected **as a whole** (`blocked: true`) when any member would
 *    overlap a box in `others`. Callers pass only the *non-dragged* units, so the
 *    group's own members never block each other, and touching edges is allowed.
 *
 * @param {object} spec
 * @param {Array<{ id: string, x: number, y: number, w: number, h: number }>} spec.startPositions
 *        member positions at drag start (x/y/w/h in canvas units, here cm or mm)
 * @param {number} spec.deltaX raw pointer delta, same unit as the positions
 * @param {number} spec.deltaY
 * @param {number} [spec.snap] grid step (0/undefined disables snapping)
 * @param {{ width: number, depth: number }|null} [spec.bounds] walls to clamp against
 * @param {Array<{ x: number, y: number, w: number, h: number }>} [spec.others]
 *        obstacles that block the whole group
 * @returns {{ blocked: boolean, dx: number, dy: number,
 *             positions: Array<{ id: string, x: number, y: number }> }}
 *        `positions` are the absolute positions to apply when `blocked` is false;
 *        `dx`/`dy` are the applied (snapped + clamped) deltas, useful for the
 *        caller's own bookkeeping.
 */
export function planRigidDrag({
  startPositions = [],
  deltaX = 0,
  deltaY = 0,
  snap = 0,
  bounds = null,
  others = [],
}) {
  const dx = snap ? round2(Math.round(deltaX / snap) * snap) : deltaX
  const dy = snap ? round2(Math.round(deltaY / snap) * snap) : deltaY

  const proposed = startPositions.map(p => ({ ...p, x: p.x + dx, y: p.y + dy }))
  const bbox = bboxOf(proposed)
  const clamp = (bounds && bbox)
    ? clampGroupShift(bbox, bounds.width, bounds.depth)
    : { dx: 0, dy: 0 }

  // Rounding the applied deltas (not the individual positions) keeps the group rigid
  // and avoids float noise such as 40.00000000000002 after the clamp correction.
  const finalDx = round2(dx + clamp.dx)
  const finalDy = round2(dy + clamp.dy)
  const moved = startPositions.map(p => ({
    id: p.id, x: p.x + finalDx, y: p.y + finalDy, w: p.w, h: p.h,
  }))

  const blocked = moved.some(p => others.some(o =>
    rectsOverlap(p.x, p.y, p.w, p.h, o.x, o.y, o.w, o.h)))

  return {
    blocked,
    dx: finalDx,
    dy: finalDy,
    positions: moved.map(p => ({ id: p.id, x: p.x, y: p.y })),
  }
}
