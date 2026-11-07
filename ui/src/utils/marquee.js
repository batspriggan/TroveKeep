// Marquee (rubber-band) selection maths, shared by the room planner and the
// aggregate baseplate planner.
//
// All rectangles use "canvas px": the same coordinate space as the inline
// `left/top/width/height` styles of the elements on the canvas. That space is
// zoom-independent (the canvas is scaled with a CSS transform, so the pointer
// coordinates must be divided by the zoom factor before they get here) and
// scroll-independent (they are relative to the canvas, not to the viewport).

/** A drag shorter than this is a click, not a marquee. */
export const MARQUEE_THRESHOLD_PX = 3

/** Order two dragged corners into a { x1, y1, x2, y2 } rectangle with x1 ≤ x2, y1 ≤ y2. */
export function normaliseMarquee(ax, ay, bx, by) {
  return {
    x1: Math.min(ax, bx),
    y1: Math.min(ay, by),
    x2: Math.max(ax, bx),
    y2: Math.max(ay, by),
  }
}

/**
 * Strict rectangle intersection: a shared edge is NOT an intersection, so merely
 * grazing an element with the marquee does not select it.
 */
export function rectsIntersect(a, b) {
  return a.x1 < b.x2 && a.x2 > b.x1 && a.y1 < b.y2 && a.y2 > b.y1
}

/**
 * Uniform wall clamp for a rigid group drag. Given the bounding box the group would
 * occupy at the proposed position, return the ONE `{ dx, dy }` shift to apply to every
 * member so the group stays inside the room. A single delta per axis keeps the group
 * rigid (no internal deformation) and stops it at the walls.
 *
 * A group wider than the room is infeasible: the result is one of the two bounds
 * (best effort, no crash).
 */
export function clampGroupShift(bbox, roomWidth, roomDepth) {
  return {
    dx: Math.min(Math.max(0, -bbox.minX), roomWidth - bbox.maxX),
    dy: Math.min(Math.max(0, -bbox.minY), roomDepth - bbox.maxY),
  }
}
