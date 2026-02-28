<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getRoom, getAllTemplates, saveRoomLayout } from '../../api/tableplanner.js'

const route = useRoute()
const router = useRouter()
const roomId = route.params.id

// Scale: 100 px = 1 m → 1 px = 1 cm
const SCALE = 1         // 1 px per cm
const SNAP = 1          // 1 cm snap
const BORDER_SNAP = 20  // cm — magnetism threshold for edge-to-edge snap

const room = ref(null)
const templates = ref([])
const placedTables = ref([])
const savedLayoutJson = ref('')
const saveSuccess = ref(false)
const loading = ref(true)
const showGrid = ref(true)

// Map from templateId → template data for display
const templateMap = computed(() => {
  const m = {}
  for (const t of templates.value) m[t.id] = t
  return m
})

const canvasWidth = computed(() => room.value ? room.value.widthCm * SCALE : 0)
const canvasHeight = computed(() => room.value ? room.value.depthCm * SCALE : 0)

const isDirty = computed(() =>
  JSON.stringify(placedTables.value.map(serialise)) !== savedLayoutJson.value
)

function serialise(p) {
  return { instanceId: p.instanceId, templateId: p.templateId, xCm: p.xCm, yCm: p.yCm }
}

// ── Load ──────────────────────────────────────────────────────────────────────
onMounted(async () => {
  const [r, tpls] = await Promise.all([getRoom(roomId), getAllTemplates()])
  room.value = r
  templates.value = tpls
  placedTables.value = r.layout.map(p => ({
    instanceId: p.instanceId,
    templateId: p.templateId,
    xCm: p.xCm,
    yCm: p.yCm,
    overlapping: false,
  }))
  savedLayoutJson.value = JSON.stringify(placedTables.value.map(serialise))
  loading.value = false
})

// ── Add from palette ──────────────────────────────────────────────────────────
function addFromTemplate(tpl) {
  placedTables.value.push({
    instanceId: crypto.randomUUID(),
    templateId: tpl.id,
    xCm: 20,
    yCm: 20,
    overlapping: false,
  })
}

// ── Remove ────────────────────────────────────────────────────────────────────
function removeTable(instanceId) {
  placedTables.value = placedTables.value.filter(p => p.instanceId !== instanceId)
}

// ── Drag ─────────────────────────────────────────────────────────────────────
const draggingId = ref(null)
let _drag = null

function startDrag(e, placed) {
  if (e.button !== 0) return
  e.preventDefault()
  draggingId.value = placed.instanceId
  _drag = {
    instanceId: placed.instanceId,
    startMouseX: e.clientX,
    startMouseY: e.clientY,
    startX: placed.xCm,
    startY: placed.yCm,
    lastValidX: placed.xCm,
    lastValidY: placed.yCm,
  }
  window.addEventListener('mousemove', onMove)
  window.addEventListener('mouseup', onUp)
}

function onMove(e) {
  if (!_drag) return
  const p = placedTables.value.find(t => t.instanceId === _drag.instanceId)
  if (!p || !room.value) return
  const tpl = templateMap.value[p.templateId]
  if (!tpl) return

  const cw = tpl.widthCm
  const cd = tpl.depthCm

  // Step 1+2: raw position → 1 cm snap → room-bounds clamp
  const rawX = _drag.startX + (e.clientX - _drag.startMouseX) / SCALE
  const rawY = _drag.startY + (e.clientY - _drag.startMouseY) / SCALE
  let cx = Math.max(0, Math.min(room.value.widthCm - cw, Math.round(rawX / SNAP) * SNAP))
  let cy = Math.max(0, Math.min(room.value.depthCm - cd, Math.round(rawY / SNAP) * SNAP))

  // Step 3: border-to-border snap (per axis, independently)
  const others = placedTables.value.filter(t => t.instanceId !== _drag.instanceId)
  let bestDX = BORDER_SNAP + 1
  let bestDY = BORDER_SNAP + 1
  let snapX = cx
  let snapY = cy

  for (const other of others) {
    const otpl = templateMap.value[other.templateId]
    if (!otpl) continue
    const nx = other.xCm, ny = other.yCm, nw = otpl.widthCm, nd = otpl.depthCm

    for (const xc of [nx - cw, nx + nw, nx, nx + nw - cw]) {
      const d = Math.abs(cx - xc)
      if (d < bestDX) { bestDX = d; snapX = xc }
    }
    for (const yc of [ny - cd, ny + nd, ny, ny + nd - cd]) {
      const d = Math.abs(cy - yc)
      if (d < bestDY) { bestDY = d; snapY = yc }
    }
  }

  // Wall snap candidates
  for (const xc of [0, room.value.widthCm - cw]) {
    const d = Math.abs(cx - xc)
    if (d < bestDX) { bestDX = d; snapX = xc }
  }
  for (const yc of [0, room.value.depthCm - cd]) {
    const d = Math.abs(cy - yc)
    if (d < bestDY) { bestDY = d; snapY = yc }
  }

  if (bestDX <= BORDER_SNAP) cx = snapX
  if (bestDY <= BORDER_SNAP) cy = snapY

  // Re-clamp after snap
  cx = Math.max(0, Math.min(room.value.widthCm - cw, cx))
  cy = Math.max(0, Math.min(room.value.depthCm - cd, cy))

  // Step 4: AABB overlap check
  const hasOverlap = others.some(other => {
    const otpl = templateMap.value[other.templateId]
    if (!otpl) return false
    return cx < other.xCm + otpl.widthCm &&
           cx + cw > other.xCm &&
           cy < other.yCm + otpl.depthCm &&
           cy + cd > other.yCm
  })

  if (hasOverlap) {
    p.overlapping = true
    // Don't update position — stay at last valid
  } else {
    p.overlapping = false
    p.xCm = cx
    p.yCm = cy
    _drag.lastValidX = cx
    _drag.lastValidY = cy
  }
}

function onUp() {
  if (_drag) {
    const p = placedTables.value.find(t => t.instanceId === _drag.instanceId)
    if (p) p.overlapping = false
  }
  _drag = null
  draggingId.value = null
  window.removeEventListener('mousemove', onMove)
  window.removeEventListener('mouseup', onUp)
}

onUnmounted(() => {
  window.removeEventListener('mousemove', onMove)
  window.removeEventListener('mouseup', onUp)
})

// ── Save ──────────────────────────────────────────────────────────────────────
async function saveLayout() {
  await saveRoomLayout(roomId, placedTables.value.map(serialise))
  savedLayoutJson.value = JSON.stringify(placedTables.value.map(serialise))
  saveSuccess.value = true
  setTimeout(() => { saveSuccess.value = false }, 2500)
}
</script>

<template>
  <div class="planner-page">
    <div class="planner-header">
      <button class="back-btn" @click="router.push('/table-planner')">&larr; Table Planner</button>
      <h1 v-if="room">{{ room.name }}</h1>
      <div class="header-right">
        <button class="toggle-btn" :class="{ active: showGrid }" @click="showGrid = !showGrid" title="Toggle grid">Grid</button>
        <span v-if="saveSuccess" class="save-ok">Layout saved!</span>
        <button
          class="primary save-btn"
          :class="{ dirty: isDirty }"
          :disabled="!isDirty"
          @click="saveLayout"
        >Save Layout</button>
      </div>
    </div>

    <div v-if="loading" class="loading">Loading…</div>

    <template v-else>
      <!-- Template palette -->
      <div class="palette">
        <span class="palette-label">Templates:</span>
        <button
          v-for="t in templates"
          :key="t.id"
          class="chip"
          :style="{ background: t.color }"
          @click="addFromTemplate(t)"
          :title="`${t.widthCm} cm × ${t.depthCm} cm`"
        >{{ t.description }}</button>
        <span v-if="templates.length === 0" class="empty-palette">No templates — create some in Table Planner.</span>
      </div>

      <!-- Canvas -->
      <div class="canvas-wrap">
        <div
          class="canvas"
          :class="{ 'canvas--grid': showGrid }"
          :style="{ width: canvasWidth + 'px', height: canvasHeight + 'px' }"
        >
          <div
            v-for="p in placedTables"
            :key="p.instanceId"
            class="table-item"
            :class="{ active: draggingId === p.instanceId, 'table-item--overlap': p.overlapping }"
            :style="{
              left: p.xCm * SCALE + 'px',
              top: p.yCm * SCALE + 'px',
              width: (templateMap[p.templateId]?.widthCm ?? 200) * SCALE + 'px',
              height: (templateMap[p.templateId]?.depthCm ?? 80) * SCALE + 'px',
              background: templateMap[p.templateId]?.color ?? '#8b6340',
            }"
            @mousedown="startDrag($event, p)"
          >
            <button class="remove-btn" @mousedown.stop @click="removeTable(p.instanceId)" title="Remove">✕</button>
            <span class="table-label">{{ templateMap[p.templateId]?.description ?? '?' }}</span>
            <span class="table-dims" v-if="templateMap[p.templateId]">
              {{ templateMap[p.templateId].widthCm / 100 }} m &times; {{ templateMap[p.templateId].depthCm / 100 }} m
            </span>
          </div>

          <div class="scale-bar">
            <div class="scale-line"></div>
            <span>1 m</span>
          </div>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped>
.planner-page {
  display: flex;
  flex-direction: column;
  height: calc(100vh - 52px);
  padding: 0.75rem 1.25rem 0;
  box-sizing: border-box;
}

.planner-header {
  display: flex;
  align-items: center;
  gap: 1rem;
  margin-bottom: 0.6rem;
  flex-wrap: wrap;
}

.planner-header h1 {
  margin: 0;
  flex: 1;
}

.back-btn {
  background: #f0f0f0;
  border: 1px solid #ccc;
  border-radius: 4px;
  padding: 0.3rem 0.65rem;
  font-size: 0.85rem;
  cursor: pointer;
}

.back-btn:hover { background: #e0e0e0; }

.header-right {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.save-ok {
  color: #2a7a2a;
  font-size: 0.85rem;
  font-weight: 600;
}

.save-btn {
  background: #888;
  color: #fff;
  border: none;
  border-radius: 4px;
  padding: 0.35rem 0.9rem;
  cursor: not-allowed;
  transition: background 0.15s;
}

.save-btn.dirty {
  background: #3a6ea5;
  cursor: pointer;
}

.save-btn.dirty:hover { background: #2e5a8a; }

.toggle-btn {
  background: #f0f0f0;
  border: 1px solid #ccc;
  border-radius: 4px;
  padding: 0.3rem 0.65rem;
  font-size: 0.8rem;
  cursor: pointer;
  color: #666;
}

.toggle-btn.active {
  background: #dde8f5;
  border-color: #3a6ea5;
  color: #3a6ea5;
  font-weight: 600;
}

.toggle-btn:hover { background: #e0e0e0; }
.toggle-btn.active:hover { background: #ccdaf0; }

.loading {
  color: #888;
  padding: 1rem 0;
}

/* ── Palette ─────────────────────────────────────────────────────────────── */
.palette {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.5rem;
  margin-bottom: 0.6rem;
}

.palette-label {
  font-size: 0.85rem;
  color: #555;
  font-weight: 600;
}

.chip {
  border: 2px solid rgba(0,0,0,0.25);
  border-radius: 5px;
  padding: 0.3rem 0.65rem;
  font-size: 0.8rem;
  font-weight: 600;
  color: #fff;
  text-shadow: 0 1px 2px rgba(0,0,0,0.4);
  cursor: pointer;
  user-select: none;
}

.chip:hover { filter: brightness(1.1); }

.empty-palette {
  font-size: 0.8rem;
  color: #999;
}

/* ── Canvas ───────────────────────────────────────────────────────────────── */
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
}

.canvas--grid {
  background-image:
    linear-gradient(to right,  #9aa8bb 1px, transparent 1px),
    linear-gradient(to bottom, #9aa8bb 1px, transparent 1px),
    linear-gradient(to right,  #dde4ef 1px, transparent 1px),
    linear-gradient(to bottom, #dde4ef 1px, transparent 1px);
  background-size: 100px 100px, 100px 100px, 20px 20px, 20px 20px;
}

/* ── Table item ───────────────────────────────────────────────────────────── */
.table-item {
  position: absolute;
  border: 2px solid rgba(0,0,0,0.3);
  border-radius: 4px;
  cursor: grab;
  user-select: none;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 2px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.2);
  transition: box-shadow 0.1s;
}

.table-item.active {
  cursor: grabbing;
  box-shadow: 0 8px 20px rgba(0,0,0,0.4);
  z-index: 10;
}

.table-item--overlap {
  border-color: rgba(200, 30, 30, 0.8);
  box-shadow: 0 0 0 2px rgba(200, 30, 30, 0.4);
}

.table-label {
  font-size: 0.72rem;
  font-weight: 700;
  color: #fff;
  pointer-events: none;
  text-shadow: 0 1px 2px rgba(0,0,0,0.5);
  text-align: center;
  padding: 0 4px;
}

.table-dims {
  font-size: 0.6rem;
  color: rgba(255,255,255,0.75);
  pointer-events: none;
}

.remove-btn {
  position: absolute;
  top: 3px;
  right: 4px;
  background: rgba(0,0,0,0.2);
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

.remove-btn:hover { background: rgba(200,30,30,0.75); }

/* ── Scale bar ────────────────────────────────────────────────────────────── */
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
  width: 100px;
  height: 3px;
  background: #555;
  border-left: 2px solid #555;
  border-right: 2px solid #555;
}
</style>
