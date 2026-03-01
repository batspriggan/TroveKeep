<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getRoom, getAllTemplates, saveRoomLayout, getAllBaseplates } from '../../api/tableplanner.js'

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

const selectedAggregateId = ref(null)   // integer index into aggregates.value, or null
const baseplates = ref([])
const aggSelections = ref({})           // aggIdx → canonical bpKey e.g. "16x32"

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

// ── Aggregate detection ────────────────────────────────────────────────────────
function rangeOverlaps(a1, a2, b1, b2) {
  return Math.min(a2, b2) - Math.max(a1, b1) > 0
}

function areAdjacent(a, tplA, b, tplB) {
  const T = 0.5  // cm tolerance
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

// Each aggregate = array of instanceIds (BFS)
const aggregates = computed(() => {
  const tables = placedTables.value
  const tMap   = templateMap.value
  const n = tables.length
  if (n === 0) return []
  const visited = new Array(n).fill(false)
  const result  = []
  for (let i = 0; i < n; i++) {
    if (visited[i]) continue
    const group = [], queue = [i]
    visited[i] = true
    while (queue.length) {
      const cur = queue.shift()
      group.push(tables[cur].instanceId)
      const a = tables[cur], tplA = tMap[a.templateId]
      if (!tplA) continue
      for (let j = 0; j < n; j++) {
        if (visited[j]) continue
        const b = tables[j], tplB = tMap[b.templateId]
        if (!tplB) continue
        if (areAdjacent(a, tplA, b, tplB)) { visited[j] = true; queue.push(j) }
      }
    }
    result.push(group)
  }
  return result
})

// instanceId → aggregate index
const aggregateMap = computed(() => {
  const map = {}
  aggregates.value.forEach((g, i) => { for (const id of g) map[id] = i })
  return map
})

// ── Distinct baseplates by canonical dimension ─────────────────────────────────
const distinctBaseplates = computed(() => {
  const seen = new Map()
  for (const bp of baseplates.value) {
    const w = Math.min(bp.widthStuds, bp.depthStuds)
    const d = Math.max(bp.widthStuds, bp.depthStuds)
    const key = `${w}x${d}`
    if (!seen.has(key)) seen.set(key, { key, widthStuds: w, depthStuds: d })
  }
  return [...seen.values()]
})

// ── Plate count per aggregate ──────────────────────────────────────────────────
function calcPlateCount(aggIdx, bpKey) {
  const bp = distinctBaseplates.value.find(b => b.key === bpKey)
  if (!bp) return null
  const group = aggregates.value[aggIdx]
  if (!group?.length) return null
  const tMap = templateMap.value

  // Table rects in mm (1 cm = 10 mm)
  const rects = group.map(id => {
    const t = placedTables.value.find(p => p.instanceId === id)
    const tpl = tMap[t.templateId]
    return { x1: t.xCm*10, y1: t.yCm*10, x2: (t.xCm+tpl.widthCm)*10, y2: (t.yCm+tpl.depthCm)*10 }
  })
  const minX = Math.min(...rects.map(r => r.x1))
  const minY = Math.min(...rects.map(r => r.y1))
  const maxX = Math.max(...rects.map(r => r.x2))
  const maxY = Math.max(...rects.map(r => r.y2))

  const pw = bp.widthStuds * 8 - 2
  const pd = bp.depthStuds * 8 - 2
  if (pw <= 0 || pd <= 0) return 0

  const nX = Math.ceil((maxX - minX) / pw)
  const nY = Math.ceil((maxY - minY) / pd)
  let count = 0
  for (let ix = 0; ix < nX; ix++) {
    for (let iy = 0; iy < nY; iy++) {
      const px1 = minX + ix*pw, py1 = minY + iy*pd
      const px2 = px1+pw,       py2 = py1+pd
      let covered = 0
      for (const r of rects) {
        const ox1 = Math.max(px1,r.x1), oy1 = Math.max(py1,r.y1)
        const ox2 = Math.min(px2,r.x2), oy2 = Math.min(py2,r.y2)
        if (ox2 > ox1 && oy2 > oy1) covered += (ox2-ox1)*(oy2-oy1)
      }
      if (covered >= pw*pd - 0.01) count++
    }
  }
  return count
}

function aggregateLabel(idx) {
  const group = aggregates.value[idx]
  if (!group) return ''
  const tMap = templateMap.value
  let minX=Infinity, minY=Infinity, maxX=-Infinity, maxY=-Infinity
  for (const id of group) {
    const t = placedTables.value.find(p => p.instanceId === id)
    const tpl = tMap[t?.templateId]
    if (!t || !tpl) continue
    if (t.xCm < minX) minX = t.xCm
    if (t.yCm < minY) minY = t.yCm
    if (t.xCm+tpl.widthCm > maxX) maxX = t.xCm+tpl.widthCm
    if (t.yCm+tpl.depthCm > maxY) maxY = t.yCm+tpl.depthCm
  }
  const w = maxX-minX, d = maxY-minY
  return `Group ${idx+1} — ${group.length} table(s) · ${w}×${d} cm`
}

// ── Load ──────────────────────────────────────────────────────────────────────
onMounted(async () => {
  const [r, tpls, bps] = await Promise.all([getRoom(roomId), getAllTemplates(), getAllBaseplates()])
  room.value = r
  templates.value = tpls
  baseplates.value = bps
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
function findFreePosition(tw, td) {
  const roomW = room.value.widthCm
  const roomD = room.value.depthCm
  if (tw > roomW || td > roomD) return { xCm: 0, yCm: 0 }

  for (let y = 0; y <= roomD - td; y += SNAP) {
    let x = 0
    while (x <= roomW - tw) {
      const blocker = placedTables.value.find(other => {
        const otpl = templateMap.value[other.templateId]
        if (!otpl) return false
        return x < other.xCm + otpl.widthCm &&
               x + tw > other.xCm &&
               y < other.yCm + otpl.depthCm &&
               y + td > other.yCm
      })
      if (!blocker) return { xCm: x, yCm: y }
      // Jump x past the right edge of the blocker
      const otpl = templateMap.value[blocker.templateId]
      x = blocker.xCm + otpl.widthCm
    }
  }
  return { xCm: 0, yCm: 0 } // fallback: no free space found
}

function addFromTemplate(tpl) {
  const { xCm, yCm } = findFreePosition(tpl.widthCm, tpl.depthCm)
  placedTables.value.push({
    instanceId: crypto.randomUUID(),
    templateId: tpl.id,
    xCm,
    yCm,
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
  const aggIdx = aggregateMap.value[placed.instanceId]
  const aggGroup = (aggIdx != null) ? aggregates.value[aggIdx] : [placed.instanceId]
  const isGroup = aggGroup.length > 1 && !e.altKey
  _drag = {
    instanceId: placed.instanceId,
    startMouseX: e.clientX, startMouseY: e.clientY,
    startX: placed.xCm, startY: placed.yCm,
    lastValidX: placed.xCm, lastValidY: placed.yCm,
    isGroup,
    groupStartPositions: isGroup
      ? aggGroup.map(id => {
          const t = placedTables.value.find(p => p.instanceId === id)
          return { instanceId: id, startX: t.xCm, startY: t.yCm }
        })
      : null,
    groupInstanceIds: isGroup ? aggGroup : null,
  }
  window.addEventListener('mousemove', onMove)
  window.addEventListener('mouseup', onUp)
}

function onMove(e) {
  if (!_drag) return
  _drag.isGroup ? onMoveGroup(e) : onMoveSingle(e)
}

function onMoveSingle(e) {
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

function onMoveGroup(e) {
  if (!room.value) return
  const gsp = _drag.groupStartPositions
  const groupIds = new Set(_drag.groupInstanceIds)
  const nonGroup = placedTables.value.filter(t => !groupIds.has(t.instanceId))

  const rawDx = (e.clientX - _drag.startMouseX) / SCALE
  const rawDy = (e.clientY - _drag.startMouseY) / SCALE
  const snapDx = Math.round(rawDx / SNAP) * SNAP
  const snapDy = Math.round(rawDy / SNAP) * SNAP

  const proposed = gsp.map(sp => ({
    instanceId: sp.instanceId,
    newX: sp.startX + snapDx,
    newY: sp.startY + snapDy,
  }))

  // Bounding box → clamp correction (uniform shift)
  let clampDx = 0, clampDy = 0
  for (const pr of proposed) {
    const t = placedTables.value.find(p => p.instanceId === pr.instanceId)
    const tpl = templateMap.value[t.templateId]
    if (!tpl) continue
    if (pr.newX < 0) clampDx = Math.max(clampDx, -pr.newX)
    if (pr.newX + tpl.widthCm > room.value.widthCm)
      clampDx = Math.min(clampDx, room.value.widthCm - pr.newX - tpl.widthCm)
    if (pr.newY < 0) clampDy = Math.max(clampDy, -pr.newY)
    if (pr.newY + tpl.depthCm > room.value.depthCm)
      clampDy = Math.min(clampDy, room.value.depthCm - pr.newY - tpl.depthCm)
  }
  for (const pr of proposed) { pr.newX += clampDx; pr.newY += clampDy }

  // Overlap check vs non-group tables
  const hasOverlap = proposed.some(pr => {
    const t = placedTables.value.find(p => p.instanceId === pr.instanceId)
    const tpl = templateMap.value[t.templateId]
    if (!tpl) return false
    return nonGroup.some(other => {
      const otpl = templateMap.value[other.templateId]
      if (!otpl) return false
      return pr.newX < other.xCm + otpl.widthCm && pr.newX + tpl.widthCm > other.xCm &&
             pr.newY < other.yCm + otpl.depthCm && pr.newY + tpl.depthCm > other.yCm
    })
  })

  if (!hasOverlap) {
    for (const pr of proposed) {
      const t = placedTables.value.find(p => p.instanceId === pr.instanceId)
      if (t) { t.xCm = pr.newX; t.yCm = pr.newY }
    }
    const main = proposed.find(p => p.instanceId === _drag.instanceId)
    if (main) { _drag.lastValidX = main.newX; _drag.lastValidY = main.newY }
  }
}

function onUp(e) {
  if (_drag) {
    const ids = _drag.isGroup ? _drag.groupInstanceIds : [_drag.instanceId]
    for (const id of ids) {
      const t = placedTables.value.find(p => p.instanceId === id)
      if (t) t.overlapping = false
    }
    const moved = Math.abs(e.clientX - _drag.startMouseX) + Math.abs(e.clientY - _drag.startMouseY)
    if (moved < 5) {
      const idx = aggregateMap.value[_drag.instanceId]
      selectedAggregateId.value = idx ?? null
    }
  }
  _drag = null
  draggingId.value = null
  window.removeEventListener('mousemove', onMove)
  window.removeEventListener('mouseup', onUp)
}

function onCanvasClick(e) {
  if (e.target === e.currentTarget) selectedAggregateId.value = null
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
        <span class="palette-hint">Alt+drag to detach a table from its group</span>
      </div>

      <!-- Plate Calculator -->
      <div v-if="distinctBaseplates.length > 0 && aggregates.length > 0" class="calc-bar">
        <table class="calc-table">
          <thead>
            <tr>
              <th>Aggregate</th>
              <th>Baseplate</th>
              <th>Fits</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(group, idx) in aggregates" :key="idx">
              <td class="calc-agg-name">{{ aggregateLabel(idx) }}</td>
              <td>
                <select class="calc-select" v-model="aggSelections[idx]">
                  <option value="">— select —</option>
                  <option v-for="bp in distinctBaseplates" :key="bp.key" :value="bp.key">
                    {{ bp.widthStuds }}×{{ bp.depthStuds }} studs
                  </option>
                </select>
              </td>
              <td class="calc-fits">
                <template v-if="aggSelections[idx]">
                  <strong class="calc-result">{{ calcPlateCount(idx, aggSelections[idx]) }}</strong>
                  <span class="calc-plate-info">
                    ({{ distinctBaseplates.find(b => b.key === aggSelections[idx])?.widthStuds * 8 - 2 }} mm
                    &times;
                    {{ distinctBaseplates.find(b => b.key === aggSelections[idx])?.depthStuds * 8 - 2 }} mm each)
                  </span>
                </template>
                <span v-else class="calc-hint">—</span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Canvas -->
      <div class="canvas-wrap">
        <div
          class="canvas"
          :class="{ 'canvas--grid': showGrid }"
          :style="{ width: canvasWidth + 'px', height: canvasHeight + 'px' }"
          @click="onCanvasClick"
        >
          <div
            v-for="p in placedTables"
            :key="p.instanceId"
            class="table-item"
            :class="{
              active: draggingId === p.instanceId,
              'table-item--overlap': p.overlapping,
              'table-item--selected': selectedAggregateId !== null && aggregateMap[p.instanceId] === selectedAggregateId,
            }"
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
              {{ templateMap[p.templateId].widthCm }} cm &times; {{ templateMap[p.templateId].depthCm }} cm
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

.palette-hint {
  font-size: 0.75rem;
  color: #aaa;
  margin-left: auto;
  white-space: nowrap;
}

/* ── Plate Calculator ─────────────────────────────────────────────────────── */
.calc-bar {
  background: #f3f5f8; border: 1px solid #d0d5de; border-radius: 6px;
  margin-bottom: 0.6rem; font-size: 0.85rem; overflow-x: auto;
}
.calc-table {
  width: 100%; border-collapse: collapse;
}
.calc-table th, .calc-table td {
  text-align: left; padding: 0.3rem 0.65rem; border-bottom: 1px solid #e0e3ea;
}
.calc-table th {
  font-weight: 600; color: #555; background: #eaecf2; font-size: 0.8rem;
}
.calc-table tr:last-child td { border-bottom: none; }
.calc-agg-name { color: #333; white-space: nowrap; }
.calc-select {
  padding: 0.2rem 0.4rem; border: 1px solid #ccc; border-radius: 4px;
  font-size: 0.82rem; background: #fff;
}
.calc-fits { white-space: nowrap; min-width: 60px; }
.calc-result { font-weight: 700; color: #2a5a2a; font-size: 0.9rem; }
.calc-plate-info { color: #666; font-size: 0.8rem; margin-left: 0.3rem; }
.calc-hint { color: #aaa; }

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

.table-item--selected {
  border-color: #1a90d0;
  border-width: 3px;
  box-shadow: 0 0 0 2px rgba(26,144,208,0.35), 0 2px 8px rgba(0,0,0,0.2);
}
.table-item--selected.active {
  box-shadow: 0 0 0 2px rgba(26,144,208,0.5), 0 8px 20px rgba(0,0,0,0.4);
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
