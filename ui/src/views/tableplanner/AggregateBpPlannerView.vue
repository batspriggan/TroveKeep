<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getRoom, getAllTemplates, getAllBaseplates, getBaseplateImageUrl, saveAggregateBpLayout } from '../../api/tableplanner.js'

const route = useRoute()
const router = useRouter()
const roomId = route.params.roomId
const repId  = route.params.repId

// px per mm at zoom=1  (1 stud=8mm; 32×32 plate = 256mm = 128px)
const SCALE = 0.5
const PADDING_MM = 80     // canvas padding around the aggregate bounding box
const SNAP_MM = 12        // edge-to-edge snap threshold in mm
const MIN_ZOOM = 0.25
const MAX_ZOOM = 4

const room      = ref(null)
const templates = ref([])
const baseplates = ref([])
const loading   = ref(true)
const saveSuccess = ref(false)
const zoom = ref(1)
const canvasWrapEl = ref(null)

// Placed baseplates: { instanceId, baseplateId, xMm, yMm, rotation }
// xMm/yMm are relative to aggregate bounding-box origin (top-left)
const placedPlates = ref([])
const savedJson    = ref('[]')

const isDirty = computed(() => JSON.stringify(serialisePlates()) !== savedJson.value)

function serialisePlates() {
  return placedPlates.value.map(p => ({
    instanceId: p.instanceId, baseplateId: p.baseplateId,
    xMm: p.xMm, yMm: p.yMm, rotation: p.rotation,
  }))
}

// ── Template / baseplate maps ─────────────────────────────────────────────────
const templateMap = computed(() => {
  const m = {}
  for (const t of templates.value) m[t.id] = t
  return m
})

const baseplateMap = computed(() => {
  const m = {}
  for (const b of baseplates.value) m[b.id] = b
  return m
})

// ── BFS aggregate detection (same as RoomPlannerView) ─────────────────────────
function rangeOverlaps(a1, a2, b1, b2) { return Math.min(a2, b2) - Math.max(a1, b1) > 0 }

function areAdjacent(a, tplA, b, tplB) {
  const T = 0.5
  const xAdj =
    (Math.abs((a.xCm + tplA.widthCm) - b.xCm) < T ||
     Math.abs((b.xCm + tplB.widthCm) - a.xCm) < T) &&
    rangeOverlaps(a.yCm, a.yCm + tplA.depthCm, b.yCm, b.yCm + tplB.depthCm)
  const yAdj =
    (Math.abs((a.yCm + tplA.depthCm) - b.yCm) < T ||
     Math.abs((b.yCm + tplB.depthCm) - a.yCm) < T) &&
    rangeOverlaps(a.xCm, a.xCm + tplA.widthCm, b.xCm, b.xCm + tplB.widthCm)
  return xAdj || yAdj
}

function computeAggregates(layout, tMap) {
  const n = layout.length
  if (n === 0) return []
  const visited = new Array(n).fill(false)
  const result = []
  for (let i = 0; i < n; i++) {
    if (visited[i]) continue
    const group = [], queue = [i]
    visited[i] = true
    while (queue.length) {
      const cur = queue.shift()
      group.push(layout[cur].instanceId)
      const a = layout[cur], tplA = tMap[a.templateId]
      if (!tplA) continue
      for (let j = 0; j < n; j++) {
        if (visited[j]) continue
        const b = layout[j], tplB = tMap[b.templateId]
        if (!tplB) continue
        if (areAdjacent(a, tplA, b, tplB)) { visited[j] = true; queue.push(j) }
      }
    }
    result.push(group)
  }
  return result
}

// Tables belonging to this aggregate
const aggTables = computed(() => {
  if (!room.value) return []
  const tMap = templateMap.value
  const aggs = computeAggregates(room.value.layout, tMap)
  const group = aggs.find(g => [...g].sort()[0] === repId)
  if (!group) return []
  return room.value.layout.filter(p => group.includes(p.instanceId))
})

// Aggregate bounding box in mm
const aggBounds = computed(() => {
  const tMap = templateMap.value
  let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity
  for (const t of aggTables.value) {
    const tpl = tMap[t.templateId]
    if (!tpl) continue
    if (t.xCm < minX) minX = t.xCm
    if (t.yCm < minY) minY = t.yCm
    if (t.xCm + tpl.widthCm > maxX) maxX = t.xCm + tpl.widthCm
    if (t.yCm + tpl.depthCm > maxY) maxY = t.yCm + tpl.depthCm
  }
  if (!isFinite(minX)) return { minXmm: 0, minYmm: 0, widthMm: 0, heightMm: 0 }
  return { minXmm: minX * 10, minYmm: minY * 10, widthMm: (maxX - minX) * 10, heightMm: (maxY - minY) * 10 }
})

const canvasWidth  = computed(() => aggBounds.value.widthMm  + 2 * PADDING_MM)
const canvasHeight = computed(() => aggBounds.value.heightMm + 2 * PADDING_MM)

// Table silhouette position on canvas (mm)
function tableCanvasX(t) { return t.xCm * 10 - aggBounds.value.minXmm + PADDING_MM }
function tableCanvasY(t) { return t.yCm * 10 - aggBounds.value.minYmm + PADDING_MM }

// ── Baseplate dimensions ───────────────────────────────────────────────────────
// Physical baseplate size: studs*8 - 2 mm (1mm border on each side, no studs there)
function bpNaturalW(bp) { return bp.widthStuds  * 8 - 2 }
function bpNaturalH(bp) { return bp.depthStuds  * 8 - 2 }

function effectiveW(bp, rotation) {
  return (rotation === 0 || rotation === 180) ? bpNaturalW(bp) : bpNaturalH(bp)
}
function effectiveH(bp, rotation) {
  return (rotation === 0 || rotation === 180) ? bpNaturalH(bp) : bpNaturalW(bp)
}

// Image style inside plate div — handles rotation of image to match plate orientation
function imageStyle(rotation, nw, nh) {
  // nw/nh = natural pixel dimensions (SCALE applied)
  if (rotation === 0 || rotation === 180) {
    return { width: '100%', height: '100%', transform: rotation ? 'rotate(180deg)' : 'none' }
  }
  // 90° / 270°: container is nh×nw; image is nw×nh; center + rotate to fill
  return {
    width: `${nw}px`, height: `${nh}px`,
    position: 'absolute', left: '50%', top: '50%',
    transform: `translate(-50%, -50%) rotate(${rotation}deg)`,
  }
}

// ── Load ──────────────────────────────────────────────────────────────────────
onMounted(async () => {
  const [r, tpls, bps] = await Promise.all([getRoom(roomId), getAllTemplates(), getAllBaseplates()])
  room.value = r
  templates.value = tpls
  baseplates.value = bps

  // Restore saved layout for this aggregate
  const saved = r.aggregateBpLayouts?.find(l => l.representativeId === repId)
  placedPlates.value = (saved?.placedBaseplates ?? []).map(p => ({ ...p }))
  savedJson.value = JSON.stringify(serialisePlates())
  loading.value = false

  canvasWrapEl.value?.addEventListener('wheel', onWheelZoom, { passive: false })
})

onUnmounted(() => {
  window.removeEventListener('mousemove', onMove)
  window.removeEventListener('mouseup', onUp)
  canvasWrapEl.value?.removeEventListener('wheel', onWheelZoom)
})

// ── Add from palette ──────────────────────────────────────────────────────────
function generateUUID() {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function')
    return crypto.randomUUID()
  return '10000000-1000-4000-8000-100000000000'.replace(/[018]/g, c =>
    (c ^ (crypto.getRandomValues(new Uint8Array(1))[0] & (15 >> (c / 4)))).toString(16))
}

function findFreePosition(ew, eh) {
  const STUD = 8
  const maxX = aggBounds.value.widthMm + PADDING_MM + ew
  const maxY = aggBounds.value.heightMm + PADDING_MM + eh

  // Candidate positions: stud grid + exact edges of already-placed plates
  const xs = new Set(), ys = new Set()
  for (let x = 0; x <= maxX; x += STUD) xs.add(x)
  for (let y = 0; y <= maxY; y += STUD) ys.add(y)
  for (const other of placedPlates.value) {
    const obp = baseplateMap.value[other.baseplateId]
    if (!obp) continue
    xs.add(other.xMm + effectiveW(obp, other.rotation)) // right edge of other
    ys.add(other.yMm + effectiveH(obp, other.rotation)) // bottom edge of other
  }

  const sortedX = [...xs].filter(x => x >= 0).sort((a, b) => a - b)
  const sortedY = [...ys].filter(y => y >= 0).sort((a, b) => a - b)

  for (const y of sortedY) {
    for (const x of sortedX) {
      if (!hasOverlap(x, y, ew, eh, null)) return { xMm: x, yMm: y }
    }
  }
  return { xMm: 0, yMm: 0 }
}

function addBaseplate(bp) {
  const ew = effectiveW(bp, 0)
  const eh = effectiveH(bp, 0)
  const { xMm, yMm } = findFreePosition(ew, eh)
  placedPlates.value.push({
    instanceId: generateUUID(),
    baseplateId: bp.id,
    xMm,
    yMm,
    rotation: 0,
  })
}

// ── Remove ────────────────────────────────────────────────────────────────────
function removePlate(instanceId) {
  placedPlates.value = placedPlates.value.filter(p => p.instanceId !== instanceId)
  if (selectedId.value === instanceId) selectedId.value = null
}

// ── Selection ─────────────────────────────────────────────────────────────────
const selectedId = ref(null)

function selectPlate(e, plate) {
  selectedId.value = plate.instanceId
}

function onCanvasClick(e) {
  if (e.target === e.currentTarget) selectedId.value = null
}

function rotateSelected() {
  const p = placedPlates.value.find(p => p.instanceId === selectedId.value)
  if (p) p.rotation = (p.rotation + 90) % 360
}

function onKeyDown(e) {
  if (selectedId.value && (e.key === 'r' || e.key === 'R') && !e.ctrlKey && !e.metaKey) {
    rotateSelected()
  }
  if (selectedId.value && (e.key === 'Delete' || e.key === 'Backspace')) {
    removePlate(selectedId.value)
  }
}

// ── Drag ─────────────────────────────────────────────────────────────────────
const draggingId = ref(null)
let _drag = null

function startDrag(e, plate) {
  if (e.button !== 0) return
  e.preventDefault()
  selectedId.value = plate.instanceId
  draggingId.value = plate.instanceId
  _drag = {
    instanceId: plate.instanceId,
    startMouseX: e.clientX, startMouseY: e.clientY,
    startX: plate.xMm, startY: plate.yMm,
  }
  window.addEventListener('mousemove', onMove)
  window.addEventListener('mouseup', onUp)
}

function onMove(e) {
  if (!_drag) return
  const p = placedPlates.value.find(t => t.instanceId === _drag.instanceId)
  if (!p) return
  const bp = baseplateMap.value[p.baseplateId]
  if (!bp) return

  const ew = effectiveW(bp, p.rotation)
  const eh = effectiveH(bp, p.rotation)

  // Raw new position in mm (canvas mm = xMm + PADDING_MM)
  const rawCanvasX = (_drag.startX + PADDING_MM) + (e.clientX - _drag.startMouseX) / (SCALE * zoom.value)
  const rawCanvasY = (_drag.startY + PADDING_MM) + (e.clientY - _drag.startMouseY) / (SCALE * zoom.value)

  // Snap to other plates' edges
  const { x: snappedX, y: snappedY, xSnapped, ySnapped } = snapPlate(rawCanvasX, rawCanvasY, ew, eh, p.instanceId)

  // Convert back to aggregate-relative mm: edge-snap takes priority; otherwise stud-grid (8mm)
  const STUD = 8
  const newXmm = xSnapped ? Math.round(snappedX - PADDING_MM) : Math.round((snappedX - PADDING_MM) / STUD) * STUD
  const newYmm = ySnapped ? Math.round(snappedY - PADDING_MM) : Math.round((snappedY - PADDING_MM) / STUD) * STUD
  if (!hasOverlap(newXmm, newYmm, ew, eh, p.instanceId)) {
    p.xMm = newXmm
    p.yMm = newYmm
  }
}

function rectsOverlap(ax, ay, aw, ah, bx, by, bw, bh) {
  // Touching edges (ax+aw === bx etc.) is allowed — only strict interior overlap counts
  return ax < bx + bw && ax + aw > bx && ay < by + bh && ay + ah > by
}

function hasOverlap(xMm, yMm, ew, eh, excludeId) {
  for (const other of placedPlates.value) {
    if (other.instanceId === excludeId) continue
    const obp = baseplateMap.value[other.baseplateId]
    if (!obp) continue
    const ow = effectiveW(obp, other.rotation)
    const oh = effectiveH(obp, other.rotation)
    if (rectsOverlap(xMm, yMm, ew, eh, other.xMm, other.yMm, ow, oh)) return true
  }
  return false
}

function snapPlate(canvasX, canvasY, ew, eh, excludeId) {
  const others = placedPlates.value.filter(p => p.instanceId !== excludeId)
  let bestDX = SNAP_MM + 1, bestDY = SNAP_MM + 1
  let snapX = canvasX, snapY = canvasY

  for (const other of others) {
    const obp = baseplateMap.value[other.baseplateId]
    if (!obp) continue
    const ow = effectiveW(obp, other.rotation)
    const oh = effectiveH(obp, other.rotation)
    const ox = other.xMm + PADDING_MM
    const oy = other.yMm + PADDING_MM

    for (const xc of [ox - ew, ox + ow, ox, ox + ow - ew]) {
      const d = Math.abs(canvasX - xc)
      if (d < bestDX) { bestDX = d; snapX = xc }
    }
    for (const yc of [oy - eh, oy + oh, oy, oy + oh - eh]) {
      const d = Math.abs(canvasY - yc)
      if (d < bestDY) { bestDY = d; snapY = yc }
    }
  }

  // Snap to aggregate bounding box origin
  for (const xc of [PADDING_MM, PADDING_MM + aggBounds.value.widthMm - ew]) {
    const d = Math.abs(canvasX - xc)
    if (d < bestDX) { bestDX = d; snapX = xc }
  }
  for (const yc of [PADDING_MM, PADDING_MM + aggBounds.value.heightMm - eh]) {
    const d = Math.abs(canvasY - yc)
    if (d < bestDY) { bestDY = d; snapY = yc }
  }

  return {
    x: bestDX <= SNAP_MM ? snapX : canvasX,
    y: bestDY <= SNAP_MM ? snapY : canvasY,
    xSnapped: bestDX <= SNAP_MM,
    ySnapped: bestDY <= SNAP_MM,
  }
}

function onUp() {
  _drag = null
  draggingId.value = null
  window.removeEventListener('mousemove', onMove)
  window.removeEventListener('mouseup', onUp)
}

// ── Zoom ──────────────────────────────────────────────────────────────────────
function zoomIn()    { zoom.value = Math.min(MAX_ZOOM, +(zoom.value * 1.25).toFixed(4)) }
function zoomOut()   { zoom.value = Math.max(MIN_ZOOM, +(zoom.value / 1.25).toFixed(4)) }
function resetZoom() { zoom.value = 1 }

function onWheelZoom(e) {
  if (!e.ctrlKey && !e.metaKey) return
  e.preventDefault()
  const factor = e.deltaY > 0 ? 1 / 1.1 : 1.1
  zoom.value = Math.max(MIN_ZOOM, Math.min(MAX_ZOOM, +(zoom.value * factor).toFixed(4)))
}

// ── Save ──────────────────────────────────────────────────────────────────────
async function save() {
  await saveAggregateBpLayout(roomId, repId, serialisePlates())
  savedJson.value = JSON.stringify(serialisePlates())
  saveSuccess.value = true
  setTimeout(() => { saveSuccess.value = false }, 2500)
}
</script>

<template>
  <div class="planner-page" @keydown="onKeyDown" tabindex="0">
    <div class="planner-header">
      <button class="back-btn" @click="router.push('/table-planner/baseplates')">&larr; Baseplate Planner</button>
      <h1 v-if="room">{{ room.name }} — Baseplate Layout</h1>
      <div class="header-right">
        <div class="zoom-controls">
          <button class="zoom-btn" @click="zoomOut" :disabled="zoom <= MIN_ZOOM" title="Zoom out">−</button>
          <button class="zoom-reset" @click="resetZoom" title="Reset zoom">{{ Math.round(zoom * 100) }}%</button>
          <button class="zoom-btn" @click="zoomIn" :disabled="zoom >= MAX_ZOOM" title="Zoom in">+</button>
        </div>
        <span v-if="saveSuccess" class="save-ok">Saved!</span>
        <button class="save-btn" :class="{ dirty: isDirty }" :disabled="!isDirty" @click="save">Save</button>
      </div>
    </div>

    <div v-if="loading" class="loading">Loading…</div>

    <template v-else-if="aggTables.length === 0">
      <p class="empty">Aggregate not found. It may have changed in the room planner.</p>
    </template>

    <template v-else>
      <!-- Palette -->
      <div class="palette">
        <span class="palette-label">Baseplates:</span>
        <button
          v-for="bp in baseplates"
          :key="bp.id"
          class="chip"
          :title="`${bp.widthStuds}×${bp.depthStuds} studs · ${bpNaturalW(bp)}×${bpNaturalH(bp)} mm`"
          @click="addBaseplate(bp)"
        >{{ bp.name || `${bp.widthStuds}×${bp.depthStuds}` }}</button>
        <span v-if="baseplates.length === 0" class="empty-palette">No baseplates — add some in Table Planner.</span>
        <span class="palette-hint">R = rotate selected · Del = remove · Ctrl+scroll = zoom</span>
      </div>

      <!-- Selected plate toolbar -->
      <div v-if="selectedId" class="toolbar">
        <button class="tool-btn" @click="rotateSelected">↺ Rotate 90°</button>
        <button class="tool-btn danger" @click="removePlate(selectedId)">✕ Remove</button>
      </div>
      <div v-else class="toolbar toolbar--placeholder">&nbsp;</div>

      <!-- Canvas -->
      <div class="canvas-wrap" ref="canvasWrapEl">
        <div class="canvas-zoom-container" :style="{ width: canvasWidth * zoom + 'px', height: canvasHeight * zoom + 'px' }">
          <div
            class="canvas"
            :style="{ width: canvasWidth + 'px', height: canvasHeight + 'px', transform: `scale(${zoom})`, transformOrigin: 'top left' }"
            @click="onCanvasClick"
          >
            <!-- Table silhouettes -->
            <div
              v-for="t in aggTables"
              :key="t.instanceId"
              class="table-silhouette"
              :style="{
                left:   tableCanvasX(t)  * SCALE + 'px',
                top:    tableCanvasY(t)  * SCALE + 'px',
                width:  (templateMap[t.templateId]?.widthCm ?? 0) * 10 * SCALE + 'px',
                height: (templateMap[t.templateId]?.depthCm  ?? 0) * 10 * SCALE + 'px',
              }"
            >
              <span class="silhouette-label">{{ templateMap[t.templateId]?.description ?? '' }}</span>
            </div>

            <!-- Placed baseplates -->
            <div
              v-for="p in placedPlates"
              :key="p.instanceId"
              class="plate"
              :class="{
                'plate--selected': selectedId === p.instanceId,
                'plate--dragging': draggingId === p.instanceId,
              }"
              :style="{
                left:   (p.xMm + PADDING_MM) * SCALE + 'px',
                top:    (p.yMm + PADDING_MM) * SCALE + 'px',
                width:  effectiveW(baseplateMap[p.baseplateId], p.rotation) * SCALE + 'px',
                height: effectiveH(baseplateMap[p.baseplateId], p.rotation) * SCALE + 'px',
                background: baseplateMap[p.baseplateId]?.legoColorRgb
                  ? '#' + baseplateMap[p.baseplateId].legoColorRgb
                  : '#9ac',
              }"
              @mousedown="startDrag($event, p)"
              @click.stop="selectPlate($event, p)"
            >
              <!-- Baseplate image -->
              <img
                v-if="baseplateMap[p.baseplateId]?.imageCached"
                :src="getBaseplateImageUrl(p.baseplateId)"
                class="plate-img"
                :style="imageStyle(
                  p.rotation,
                  bpNaturalW(baseplateMap[p.baseplateId]) * SCALE,
                  bpNaturalH(baseplateMap[p.baseplateId]) * SCALE
                )"
                draggable="false"
              />
              <span class="plate-label">
                {{ baseplateMap[p.baseplateId]?.widthStuds }}×{{ baseplateMap[p.baseplateId]?.depthStuds }}
                <template v-if="p.rotation"> · {{ p.rotation }}°</template>
              </span>
            </div>
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
  outline: none;
}

.planner-header {
  display: flex;
  align-items: center;
  gap: 1rem;
  margin-bottom: 0.6rem;
  flex-wrap: wrap;
}

.planner-header h1 { margin: 0; flex: 1; font-size: 1.15rem; }

.back-btn {
  background: #f0f0f0;
  border: 1px solid #ccc;
  border-radius: 4px;
  padding: 0.3rem 0.65rem;
  font-size: 0.85rem;
  cursor: pointer;
}
.back-btn:hover { background: #e0e0e0; }

.header-right { display: flex; align-items: center; gap: 0.75rem; }

.save-ok { color: #2a7a2a; font-size: 0.85rem; font-weight: 600; }

.save-btn {
  background: #888; color: #fff; border: none; border-radius: 4px;
  padding: 0.35rem 0.9rem; cursor: not-allowed; transition: background 0.15s;
}
.save-btn.dirty { background: #3a6ea5; cursor: pointer; }
.save-btn.dirty:hover { background: #2e5a8a; }

/* ── Zoom ─────────────────────────────────────────────────────────────────── */
.zoom-controls { display: flex; align-items: center; border: 1px solid #ccc; border-radius: 4px; overflow: hidden; }

.zoom-btn {
  background: #f0f0f0; border: none; padding: 0.3rem 0.6rem;
  font-size: 1rem; line-height: 1; cursor: pointer; color: #333; font-weight: 600; min-width: 28px;
}
.zoom-btn:hover:not(:disabled) { background: #e0e0e0; }
.zoom-btn:disabled { color: #bbb; cursor: default; }

.zoom-reset {
  background: #f8f8f8; border: none; border-left: 1px solid #ccc; border-right: 1px solid #ccc;
  padding: 0.3rem 0.5rem; font-size: 0.78rem; cursor: pointer; color: #555; min-width: 46px; text-align: center;
}
.zoom-reset:hover { background: #e8e8e8; }

/* ── Palette ─────────────────────────────────────────────────────────────── */
.palette {
  display: flex; align-items: center; flex-wrap: wrap; gap: 0.4rem; margin-bottom: 0.4rem;
}

.palette-label { font-size: 0.85rem; color: #555; font-weight: 600; }

.chip {
  background: #6a8dae; border: 2px solid rgba(0,0,0,0.2); border-radius: 5px;
  padding: 0.25rem 0.55rem; font-size: 0.78rem; font-weight: 600; color: #fff;
  text-shadow: 0 1px 2px rgba(0,0,0,0.4); cursor: pointer; user-select: none;
  white-space: nowrap;
}
.chip:hover { filter: brightness(1.12); }

.empty-palette { font-size: 0.8rem; color: #999; }

.palette-hint { font-size: 0.72rem; color: #aaa; margin-left: auto; white-space: nowrap; }

/* ── Toolbar ─────────────────────────────────────────────────────────────── */
.toolbar {
  display: flex; align-items: center; gap: 0.5rem; margin-bottom: 0.4rem; min-height: 30px;
}
.toolbar--placeholder { pointer-events: none; }

.tool-btn {
  background: #f0f0f0; border: 1px solid #ccc; border-radius: 4px;
  padding: 0.25rem 0.65rem; font-size: 0.82rem; cursor: pointer;
}
.tool-btn:hover { background: #e4e4e4; }
.tool-btn.danger { color: #c0392b; }
.tool-btn.danger:hover { background: #fde8e8; border-color: #c0392b; }

.loading, .empty { color: #888; padding: 1rem 0; }

/* ── Canvas ───────────────────────────────────────────────────────────────── */
.canvas-wrap {
  flex: 1; overflow: auto; border: 1px solid #ccc; border-radius: 6px;
  background: #e8ecf0; margin-bottom: 1rem;
}

.canvas-zoom-container { position: relative; }

.canvas {
  position: relative;
  background-color: #f7f8fa;
}

/* ── Table silhouette ────────────────────────────────────────────────────── */
.table-silhouette {
  position: absolute;
  background: rgba(100, 120, 160, 0.12);
  border: 1.5px solid rgba(80, 100, 140, 0.25);
  border-radius: 3px;
  display: flex; align-items: center; justify-content: center;
  pointer-events: none;
}

.silhouette-label {
  font-size: 0.6rem; color: rgba(60, 80, 120, 0.4); font-weight: 600;
  text-align: center; pointer-events: none; user-select: none;
}

/* ── Baseplate ───────────────────────────────────────────────────────────── */
.plate {
  position: absolute;
  border: 2px solid rgba(0,0,0,0.25);
  border-radius: 2px;
  cursor: grab;
  user-select: none;
  overflow: hidden;
  display: flex; align-items: center; justify-content: center;
  box-shadow: 0 2px 6px rgba(0,0,0,0.18);
  transition: box-shadow 0.1s;
}

.plate--selected {
  border-color: #1a90d0;
  border-width: 2px;
  box-shadow: 0 0 0 2px rgba(26,144,208,0.45), 0 2px 6px rgba(0,0,0,0.18);
  z-index: 5;
}

.plate--dragging {
  cursor: grabbing;
  box-shadow: 0 6px 18px rgba(0,0,0,0.35);
  z-index: 10;
}

.plate-img {
  display: block;
  object-fit: cover;
}

.plate-label {
  position: absolute;
  font-size: 0.55rem;
  font-weight: 700;
  color: rgba(255,255,255,0.85);
  text-shadow: 0 1px 2px rgba(0,0,0,0.6);
  pointer-events: none;
  z-index: 1;
}
</style>
