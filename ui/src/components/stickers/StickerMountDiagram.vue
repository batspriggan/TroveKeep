<script setup>
/**
 * A small, to-scale drawing of what the two label dimensions measure on the piece.
 *
 * The fields of a sticker item are easy to swap (`1 × 6` reads the same either way), so the editor
 * shows the piece and the label on it: the drawing is what tells the operator that a tile measures
 * width × depth on TOP, and a brick stack measures facade × height on the SIDE. Everything is drawn
 * from millimetres, so the proportions are the real ones.
 */
import { computed } from 'vue'
import { STICKER_BRICK_MM, STUD_MM } from '../../utils/stickerSheets.js'

const props = defineProps({
  /** `Tile` | `BrickStack`. */
  mount: { type: String, default: 'Tile' },
  /** Studs along the long side. */
  widthStuds: { type: Number, default: 6 },
  /** Studs across a tile (ignored by a brick stack). */
  depthStuds: { type: Number, default: 2 },
  /** Height in bricks (ignored by a tile). */
  brickHeight: { type: Number, default: 3 },
})

const isStack = computed(() => props.mount === 'BrickStack')

/** Real millimetres of the drawn face: width × depth for a tile, facade × height for a stack. */
const face = computed(() => {
  const w = Math.max(1, Number(props.widthStuds) || 1) * STUD_MM
  const h = isStack.value
    ? Math.max(0.01, Number(props.brickHeight) || 0.01) * STICKER_BRICK_MM
    : Math.max(1, Number(props.depthStuds) || 1) * STUD_MM
  return { w, h }
})

/** Drawing box: 260 × 120; the piece is scaled down to fit with room for the measuring arrows. */
const BOX = { w: 260, h: 120 }
const PAD = 34

const scale = computed(() =>
  Math.min((BOX.w - 2 * PAD) / face.value.w, (BOX.h - 2 * PAD) / face.value.h),
)

const rect = computed(() => {
  const w = face.value.w * scale.value
  const h = face.value.h * scale.value
  return { x: PAD, y: (BOX.h - h) / 2, w, h }
})

/** One stud or one brick, in drawing units — the pitch of the tick marks. */
const pitch = computed(() => (isStack.value ? STICKER_BRICK_MM * scale.value : STUD_MM * scale.value))

const studs = computed(() => Math.max(1, Math.round(Number(props.widthStuds) || 1)))
const levels = computed(() => Math.max(1, Math.round(Number(props.brickHeight) || 1)))

/** Vertical tick positions: one per brick, laid out from the bottom edge upwards. */
const levelTicks = computed(() => {
  const r = rect.value
  const out = []
  for (let i = 1; i < levels.value; i++) out.push(r.y + r.h - i * pitch.value)
  return out
})

const studTicks = computed(() => {
  const r = rect.value
  const out = []
  for (let i = 1; i < studs.value; i++) out.push(r.x + i * pitch.value)
  return out
})

const widthMm = computed(() => Math.round(face.value.w * 100) / 100)
const heightMm = computed(() => Math.round(face.value.h * 100) / 100)
</script>

<template>
  <figure class="mount-diagram">
    <svg :viewBox="`0 0 ${BOX.w} ${BOX.h}`" role="img"
         :aria-label="isStack
           ? `A brick stack seen from the side: width is the facade, height is the number of bricks`
           : `A flat piece seen from above: width and depth are in studs`">
      <defs>
        <marker id="mount-arrow" viewBox="0 0 10 10" refX="9" refY="5"
                markerWidth="6" markerHeight="6" orient="auto-start-reverse">
          <path d="M 0 0 L 10 5 L 0 10 z" fill="currentColor" />
        </marker>
      </defs>

      <!-- The piece: its face is the label itself. -->
      <rect class="face" :x="rect.x" :y="rect.y" :width="rect.w" :height="rect.h" />

      <!-- Studs on a tile (top view): circles at the stud pitch. -->
      <template v-if="!isStack">
        <circle v-for="i in studs" :key="`s${i}`"
                class="stud"
                :cx="rect.x + (i - 0.5) * pitch"
                :cy="rect.y + rect.h / 2"
                r="2.4" />
      </template>
      <!-- Bricks on a stack (side view): a line per brick edge. -->
      <template v-else>
        <line v-for="(y, i) in levelTicks" :key="`l${i}`"
              class="level" :x1="rect.x" :x2="rect.x + rect.w" :y1="y" :y2="y" />
      </template>

      <!-- Horizontal measure (width / facade length). -->
      <line class="arrow" :x1="rect.x" :x2="rect.x + rect.w" :y1="rect.y - 16" :y2="rect.y - 16"
            marker-start="url(#mount-arrow)" marker-end="url(#mount-arrow)" />
      <text class="measure" :x="rect.x + rect.w / 2" :y="rect.y - 22" text-anchor="middle">
        {{ widthStuds }} stud → {{ widthMm }} mm
      </text>

      <!-- Vertical measure (depth / brick height). -->
      <line class="arrow" :x1="rect.x + rect.w + 16" :x2="rect.x + rect.w + 16"
            :y1="rect.y" :y2="rect.y + rect.h"
            marker-start="url(#mount-arrow)" marker-end="url(#mount-arrow)" />
      <text class="measure" :x="rect.x + rect.w + 22" :y="rect.y + rect.h / 2"
            text-anchor="start" dominant-baseline="middle">
        <tspan x="0" y="0">{{ isStack ? `${brickHeight} brick` : `${depthStuds} stud` }}</tspan>
        <tspan x="0" dy="12">→ {{ heightMm }} mm</tspan>
      </text>
    </svg>

    <figcaption>
      <template v-if="isStack">
        The label goes on the <strong>side</strong> of the stack: width is the facade length in
        studs, height is how many bricks are stacked (9.6 mm each).
      </template>
      <template v-else>
        The label goes on <strong>top</strong>: width and depth are both in studs (8 mm each).
      </template>
    </figcaption>
  </figure>
</template>

<style scoped>
.mount-diagram {
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  color: #475569;
}

.mount-diagram svg {
  width: 100%;
  max-width: 260px;
  height: auto;
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

.level {
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
  fill: #334155;
  font-variant-numeric: tabular-nums;
}

figcaption {
  font-size: 0.78rem;
  line-height: 1.35;
  color: #64748b;
}
</style>
