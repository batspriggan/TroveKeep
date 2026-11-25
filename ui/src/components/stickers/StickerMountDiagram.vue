<script setup>
/**
 * A small, to-scale drawing of what the two label dimensions measure on the piece.
 *
 * The fields of a sticker item are easy to swap (`1 × 6` reads the same either way), so the editor
 * shows the piece and the label on it: the drawing is what tells the operator that a tile measures
 * width × depth on TOP, and a brick stack measures facade × height on the SIDE.
 *
 * Everything is derived from one uniform scale for BOTH axes and both dimensions are labelled with
 * the same format (`6 stud` / `48 mm`), so what the fields say and what the drawing says cannot
 * disagree. The axes are labelled outside the drawing box, never rotated or clipped.
 */
import { computed } from 'vue'
import { STICKER_BRICK_MM, STUD_MM } from '../../utils/stickerSheets.js'

const props = defineProps({
  /** `Tile` | `BrickStack`. */
  mount: { type: String, default: 'Tile' },
  /** Studs along the long side (width, or the facade length of a stack). */
  widthStuds: { type: Number, default: 6 },
  /** Studs across a tile (ignored by a brick stack). */
  depthStuds: { type: Number, default: 2 },
  /** Height in bricks (ignored by a tile). */
  brickHeight: { type: Number, default: 3 },
})

const isStack = computed(() => props.mount === 'BrickStack')

/** The two discrete counts the piece is made of — one per axis. */
const countX = computed(() => Math.max(1, Math.round(Number(props.widthStuds) || 1)))
const countY = computed(() =>
  isStack.value ? Math.max(1, Math.round(Number(props.brickHeight) || 1)) : Math.max(1, Math.round(Number(props.depthStuds) || 1)),
)

/** Dimensions in the user's own unit: studs on both axes for a tile, studs × bricks for a stack. */
const unitX = computed(() => `${countX.value} stud`)
const unitY = computed(() => (isStack.value ? `${countY.value} brick` : `${countY.value} stud`))

/** The resulting millimetres, rounded exactly like `stickerSizeMm` does. */
const round = (value) => Math.round((value + Number.EPSILON) * 100) / 100
const mmX = computed(() => round(countX.value * STUD_MM))
const mmY = computed(() =>
  isStack.value ? round(countY.value * STICKER_BRICK_MM) : round(countY.value * STUD_MM),
)

/**
 * The SVG is drawn in its own units and the aspect ratio follows the piece, so a 6 × 1 brick prints
 * as a long, low box and a 2 × 4 tile as a tall one. `height` is fixed and the viewBox width grows
 * with the piece, up to a point: beyond that the drawing gets shorter instead of wider.
 */
const MAX_W = 420
const MIN_H = 90
const H = 96
const PAD = 14

/** Pixels per millimetre, the same on both axes. */
const scale = computed(() => {
  const sx = (MAX_W - 2 * PAD) / Math.max(1, mmX.value)
  const sy = (H - 2 * PAD) / Math.max(1, mmY.value)
  return Math.min(sx, sy)
})

const rect = computed(() => {
  const w = Math.max(8, mmX.value * scale.value)
  const h = Math.max(6, mmY.value * scale.value)
  const width = Math.min(MAX_W, Math.max(120, w + 2 * PAD))
  return {
    x: (width - w) / 2,
    y: (H - h) / 2,
    w,
    h,
    width,
  }
})

const viewBox = computed(() => `0 0 ${rect.value.width} ${H}`)

/** Stud pitch and brick pitch in drawing units, at that same scale. */
const pitchX = computed(() => (rect.value.w / countX.value))
const pitchY = computed(() => (rect.value.h / countY.value))

/** One tick per stud (top view) or per brick edge (side view). */
const studCentres = computed(() =>
  Array.from({ length: countX.value }, (_, i) => rect.value.x + (i + 0.5) * pitchX.value),
)
const brickEdges = computed(() =>
  Array.from({ length: Math.max(0, countY.value - 1) }, (_, i) => rect.value.y + rect.value.h - (i + 1) * pitchY.value),
)

/** The label text, in one place so both axes read the same. */
const labelX = computed(() => `${unitX.value} · ${mmX.value} mm`)
const labelY = computed(() => `${unitY.value} · ${mmY.value} mm`)
</script>

<template>
  <figure class="mount-diagram">
    <svg :viewBox="viewBox" role="img" preserveAspectRatio="xMidYMid meet"
         :aria-label="isStack
           ? `Brick stack seen from the side: width is ${labelX}, height is ${labelY}`
           : `Flat piece seen from above: width is ${labelX}, depth is ${labelY}`">
      <defs>
        <marker id="mount-arrow" viewBox="0 0 10 10" refX="9" refY="5"
                markerWidth="5" markerHeight="5" orient="auto-start-reverse">
          <path d="M 0 0 L 10 5 L 0 10 z" fill="currentColor" />
        </marker>
      </defs>

      <!-- The piece: its face IS the label, so the box is the sticker's exact shape. -->
      <rect class="face" :x="rect.x" :y="rect.y" :width="rect.w" :height="rect.h" />

      <!-- A tile is seen from above: one circle per stud. -->
      <circle v-for="(cx, i) in studCentres" v-show="!isStack" :key="`s${i}`"
              class="stud" :cx="cx" :cy="rect.y + rect.h / 2"
              :r="Math.min(3, pitchX / 3)" />

      <!-- A stack is seen from the side: one dashed edge per brick. -->
      <line v-for="(y, i) in brickEdges" v-show="isStack" :key="`b${i}`"
            class="brick-edge" :x1="rect.x" :x2="rect.x + rect.w" :y1="y" :y2="y" />

      <!-- Width / facade length, above the piece. -->
      <line class="arrow" :x1="rect.x" :x2="rect.x + rect.w"
            :y1="rect.y - 7" :y2="rect.y - 7"
            marker-start="url(#mount-arrow)" marker-end="url(#mount-arrow)" />
      <text class="measure" :x="rect.x + rect.w / 2" :y="rect.y - 12" text-anchor="middle">
        {{ labelX }}
      </text>
    </svg>

    <!-- The second axis is written beside the drawing, never rotated: a rotated tspan is what made
         the earlier version unreadable and cut off. -->
    <p class="vertical-measure">
      <span class="axis-name">{{ isStack ? 'Height' : 'Depth' }}</span>
      <strong>{{ labelY }}</strong>
    </p>

    <figcaption>
      <template v-if="isStack">
        Label on the <strong>side</strong> of the stack: width is the facade length in studs,
        height is the stacked bricks (9.6 mm each).
      </template>
      <template v-else>
        Label on <strong>top</strong>: width and depth are both in studs (8 mm each).
      </template>
    </figcaption>
  </figure>
</template>

<style scoped>
.mount-diagram {
  margin: 0;
  display: grid;
  grid-template-columns: 1fr auto;
  grid-template-areas:
    'drawing vertical'
    'caption caption';
  gap: 0.2rem 0.6rem;
  align-items: center;
  color: #475569;
}

.mount-diagram svg {
  grid-area: drawing;
  width: 100%;
  max-width: 420px;
  height: 96px;
  color: #64748b;
}

.face {
  fill: #f1f5f9;
  stroke: #334155;
  stroke-width: 1;
}

.stud {
  fill: #cbd5e1;
  stroke: #94a3b8;
  stroke-width: 0.6;
}

.brick-edge {
  stroke: #94a3b8;
  stroke-width: 0.7;
  stroke-dasharray: 3 2;
}

.arrow {
  stroke: currentColor;
  stroke-width: 0.8;
}

.measure {
  font-size: 9px;
  fill: #1e293b;
  font-variant-numeric: tabular-nums;
}

.vertical-measure {
  grid-area: vertical;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
  font-size: 0.78rem;
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
  color: #1e293b;
  border-left: 2px solid #cbd5e1;
  padding-left: 0.5rem;
}

.axis-name {
  font-size: 0.7rem;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: #64748b;
}

figcaption {
  grid-area: caption;
  font-size: 0.78rem;
  line-height: 1.35;
  color: #64748b;
}
</style>
