<script setup>
import { ref } from 'vue'

// Scale: 100 px = 1 m
// Table: 2 m × 0.8 m  →  200 px × 80 px
// Snap grid: 20 px = 20 cm
const SCALE    = 100
const TABLE_W  = 2 * SCALE       // 200 px
const TABLE_H  = 0.8 * SCALE     // 80 px
const SNAP     = 20              // px
const CANVAS_W = 3000            // 30 m wide
const CANVAS_H = 2000            // 20 m tall

const tables = ref([])
let nextId = 1

const draggingId = ref(null)

function addTable() {
  const offset = (tables.value.length % 8) * SNAP * 2
  tables.value.push({
    id: nextId,
    label: `Table ${nextId}`,
    x: SNAP + offset,
    y: SNAP + offset,
  })
  nextId++
}

function removeTable(id) {
  tables.value = tables.value.filter(t => t.id !== id)
}

// Plain object — not reactive, doesn't need to be
let _drag = null

function startDrag(e, table) {
  if (e.button !== 0) return
  e.preventDefault()
  draggingId.value = table.id
  _drag = {
    id: table.id,
    startMouseX: e.clientX,
    startMouseY: e.clientY,
    startX: table.x,
    startY: table.y,
  }
  window.addEventListener('mousemove', onMove)
  window.addEventListener('mouseup', onUp)
}

function onMove(e) {
  if (!_drag) return
  const t = tables.value.find(t => t.id === _drag.id)
  if (!t) return
  const rawX = _drag.startX + (e.clientX - _drag.startMouseX)
  const rawY = _drag.startY + (e.clientY - _drag.startMouseY)
  t.x = Math.max(0, Math.min(CANVAS_W - TABLE_W, Math.round(rawX / SNAP) * SNAP))
  t.y = Math.max(0, Math.min(CANVAS_H - TABLE_H, Math.round(rawY / SNAP) * SNAP))
}

function onUp() {
  _drag = null
  draggingId.value = null
  window.removeEventListener('mousemove', onMove)
  window.removeEventListener('mouseup', onUp)
}
</script>

<template>
  <div class="planner-page">
    <div class="planner-header">
      <h1>Table Planner</h1>
      <div class="toolbar">
        <button class="primary" @click="addTable">+ Add Table</button>
        <span class="hint">Drag tables to position them &mdash; snaps to 20 cm grid.</span>
      </div>
    </div>

    <p v-if="tables.length === 0" class="empty-hint">
      No tables yet. Click <strong>+ Add Table</strong> to place your first beer table (2 m &times; 0.8 m).
    </p>

    <div class="canvas-wrap">
      <div
        class="canvas"
        :style="{ width: CANVAS_W + 'px', height: CANVAS_H + 'px' }"
      >
        <div
          v-for="t in tables"
          :key="t.id"
          class="table-item"
          :class="{ active: draggingId === t.id }"
          :style="{ left: t.x + 'px', top: t.y + 'px', width: TABLE_W + 'px', height: TABLE_H + 'px' }"
          @mousedown="startDrag($event, t)"
        >
          <button class="remove-btn" @mousedown.stop @click="removeTable(t.id)" title="Remove">✕</button>
          <span class="table-label">{{ t.label }}</span>
          <span class="table-dims">2 m &times; 0.8 m</span>
        </div>

        <div class="scale-bar">
          <div class="scale-line"></div>
          <span>1 m</span>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.planner-page {
  display: flex;
  flex-direction: column;
  height: calc(100vh - 52px);
  padding: 1rem 1.5rem 0;
  box-sizing: border-box;
}

.planner-header {
  display: flex;
  align-items: baseline;
  gap: 1.5rem;
  margin-bottom: 0.75rem;
  flex-wrap: wrap;
}

.planner-header h1 {
  margin: 0;
}

.toolbar {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.hint {
  font-size: 0.85rem;
  color: #666;
}

.empty-hint {
  font-size: 0.9rem;
  color: #666;
  margin-bottom: 0.75rem;
}

.canvas-wrap {
  flex: 1;
  overflow: auto;
  border: 1px solid #ccc;
  border-radius: 6px;
  background: #e8ecf0;
  margin-bottom: 1rem;
}

.canvas {
  position: relative;
  background-color: #fff;
  /* Major grid lines every 100 px = 1 m (darker) */
  /* Minor grid lines every 20 px = 20 cm (lighter) */
  background-image:
    linear-gradient(to right,  #9aa8bb 1px, transparent 1px),
    linear-gradient(to bottom, #9aa8bb 1px, transparent 1px),
    linear-gradient(to right,  #dde4ef 1px, transparent 1px),
    linear-gradient(to bottom, #dde4ef 1px, transparent 1px);
  background-size: 100px 100px, 100px 100px, 20px 20px, 20px 20px;
}

/* ── Table ───────────────────────────────────────────────────── */
.table-item {
  position: absolute;
  background: linear-gradient(160deg, #a0714f 0%, #7a5030 100%);
  border: 2px solid #4e3010;
  border-radius: 4px;
  cursor: grab;
  user-select: none;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 3px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.25);
  transition: box-shadow 0.1s;
}

.table-item.active {
  cursor: grabbing;
  box-shadow: 0 8px 20px rgba(0, 0, 0, 0.4);
  z-index: 10;
}

.table-label {
  font-size: 0.75rem;
  font-weight: 700;
  color: #fff;
  pointer-events: none;
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.4);
}

.table-dims {
  font-size: 0.63rem;
  color: rgba(255, 255, 255, 0.7);
  pointer-events: none;
}

.remove-btn {
  position: absolute;
  top: 3px;
  right: 4px;
  background: rgba(0, 0, 0, 0.2);
  border: none;
  border-radius: 3px;
  color: #fff;
  font-size: 0.55rem;
  width: 15px;
  height: 15px;
  padding: 0;
  line-height: 1;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
}

.remove-btn:hover {
  background: rgba(200, 30, 30, 0.75);
}

/* ── Scale bar ───────────────────────────────────────────────── */
.scale-bar {
  position: absolute;
  bottom: 14px;
  right: 18px;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 2px;
  font-size: 0.7rem;
  color: #555;
  pointer-events: none;
}

.scale-line {
  width: 100px; /* = 1 m at current scale */
  height: 3px;
  background: #555;
  border-left: 2px solid #555;
  border-right: 2px solid #555;
}
</style>
