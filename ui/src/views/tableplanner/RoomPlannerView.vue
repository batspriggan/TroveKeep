<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getRoom, getAllTemplates, saveRoomLayout, updateRoom, getAllBaseplates } from '../../api/tableplanner.js'
import { generateRoomPdf } from '../../utils/roomPdf.js'

const route = useRoute()
const router = useRouter()
const roomId = route.params.id

// Scale: 100 px = 1 m → 1 px = 1 cm
const SCALE = 1         // 1 px per cm
const SNAP = 0.8        // 0.8 cm = 1 stud (8 mm)
const BORDER_SNAP = 20  // cm — magnetism threshold for edge-to-edge snap
const MIN_ZOOM = 0.25
const MAX_ZOOM = 4

const room = ref(null)
const templates = ref([])
const placedTables = ref([])
const savedLayoutJson = ref('')
const savedAggSelectionsJson = ref('[]')
const saveSuccess = ref(false)
const pdfExporting = ref(false)
const loading = ref(true)
const renameMode = ref(false)
const renameInput = ref('')
const renameError = ref('')

const showGrid = ref(true)
const zoom = ref(1)
const canvasWrapEl = ref(null)

const selectedAggregateId = ref(null)   // integer index into aggregates.value, or null
const baseplates = ref([])
const aggSelections = ref({})           // aggIdx → canonical bpKey e.g. "16x32"

// Map from templateId → template data for display
const templateMap = computed(() => {
  const m = {}
  for (const t of templates.value) m[t.id] = t
  return m
})

function effW(p) {
  const tpl = templateMap.value[p.templateId]
  if (!tpl) return 0
  return p.rotation % 180 === 0 ? tpl.widthCm : tpl.depthCm
}

function effH(p) {
  const tpl = templateMap.value[p.templateId]
  if (!tpl) return 0
  return p.rotation % 180 === 0 ? tpl.depthCm : tpl.widthCm
}

const canvasWidth = computed(() => room.value ? room.value.widthCm * SCALE : 0)
const canvasHeight = computed(() => room.value ? room.value.depthCm * SCALE : 0)

const isDirty = computed(() =>
  JSON.stringify(placedTables.value.map(serialise)) !== savedLayoutJson.value ||
  JSON.stringify(buildAggSelectionsForSave()) !== savedAggSelectionsJson.value
)

function serialise(p) {
  return { instanceId: p.instanceId, templateId: p.templateId, xCm: p.xCm, yCm: p.yCm, rotation: p.rotation ?? 0 }
}

// Stable aggregate identity: lexicographically smallest instanceId in the group
function buildAggSelectionsForSave() {
  return aggregates.value
    .map((group, idx) => {
      const bpKey = aggSelections.value[idx]
      if (!bpKey) return null
      const repId = [...group].sort()[0]
      return { representativeId: repId, bpKey }
    })
    .filter(Boolean)
}

function restoreAggSelections(savedSelections) {
  const newSel = {}
  for (const saved of savedSelections) {
    const idx = aggregates.value.findIndex(g => g.includes(saved.representativeId))
    if (idx >= 0) newSel[idx] = saved.bpKey
  }
  aggSelections.value = newSel
}

// ── Aggregate detection ────────────────────────────────────────────────────────
function rangeOverlaps(a1, a2, b1, b2) {
  return Math.min(a2, b2) - Math.max(a1, b1) > 0
}

function areAdjacent(a, wA, hA, b, wB, hB) {
  const T = 0.5  // cm tolerance
  const xAdj =
    (Math.abs((a.xCm + wA) - b.xCm) < T ||
     Math.abs((b.xCm + wB) - a.xCm) < T) &&
    rangeOverlaps(a.yCm, a.yCm + hA, b.yCm, b.yCm + hB)
  const yAdj =
    (Math.abs((a.yCm + hA) - b.yCm) < T ||
     Math.abs((b.yCm + hB) - a.yCm) < T) &&
    rangeOverlaps(a.xCm, a.xCm + wA, b.xCm, b.xCm + wB)
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
      const wA = a.rotation % 180 === 0 ? tplA.widthCm : tplA.depthCm
      const hA = a.rotation % 180 === 0 ? tplA.depthCm : tplA.widthCm
      for (let j = 0; j < n; j++) {
        if (visited[j]) continue
        const b = tables[j], tplB = tMap[b.templateId]
        if (!tplB) continue
        const wB = b.rotation % 180 === 0 ? tplB.widthCm : tplB.depthCm
        const hB = b.rotation % 180 === 0 ? tplB.depthCm : tplB.widthCm
        if (areAdjacent(a, wA, hA, b, wB, hB)) { visited[j] = true; queue.push(j) }
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

// Bounding box of the currently selected aggregate (canvas coordinates, cm)
const selectedAggregateBBox = computed(() => {
  if (selectedAggregateId.value === null) return null
  const group = aggregates.value[selectedAggregateId.value]
  if (!group?.length) return null
  let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity
  for (const id of group) {
    const t = placedTables.value.find(p => p.instanceId === id)
    if (!t) continue
    const w = effW(t), h = effH(t)
    minX = Math.min(minX, t.xCm);      minY = Math.min(minY, t.yCm)
    maxX = Math.max(maxX, t.xCm + w);  maxY = Math.max(maxY, t.yCm + h)
  }
  return { minX, minY, maxX, maxY }
})

// ── Distinct baseplates by canonical dimension ─────────────────────────────────
const distinctBaseplates = computed(() => {
  const seen = new Map()
  for (const bp of baseplates.value) {
    const w = Math.min(bp.widthStuds, bp.depthStuds)
    const d = Math.max(bp.widthStuds, bp.depthStuds)
    const key = `${w}x${d}`
    if (!seen.has(key)) seen.set(key, { key, widthStuds: w, depthStuds: d, type: bp.type })
  }
  return [...seen.values()]
})

// Standard/Road plates and Custom plates with stud counts that are multiples of 8:
// studs×8 − 0.2 mm (0.1 mm clearance each side, per LEGO spec).
// Custom plates with non-standard stud counts: studs×8 − 2 mm.
function isStdGeom(bp) { return bp.type !== 'Custom' || (bp.widthStuds % 8 === 0 && bp.depthStuds % 8 === 0) }
function bpMmW(bp) { return bp ? bp.widthStuds * 8 - (isStdGeom(bp) ? 0.2 : 2) : '' }
function bpMmH(bp) { return bp ? bp.depthStuds * 8 - (isStdGeom(bp) ? 0.2 : 2) : '' }

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
    const w = t.rotation % 180 === 0 ? tpl.widthCm : tpl.depthCm
    const h = t.rotation % 180 === 0 ? tpl.depthCm : tpl.widthCm
    return { x1: t.xCm*10, y1: t.yCm*10, x2: (t.xCm+w)*10, y2: (t.yCm+h)*10 }
  })
  const minX = Math.min(...rects.map(r => r.x1))
  const minY = Math.min(...rects.map(r => r.y1))
  const maxX = Math.max(...rects.map(r => r.x2))
  const maxY = Math.max(...rects.map(r => r.y2))

  const pw = bpMmW(bp)
  const pd = bpMmH(bp)
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
    const w = t.rotation % 180 === 0 ? tpl.widthCm : tpl.depthCm
    const h = t.rotation % 180 === 0 ? tpl.depthCm : tpl.widthCm
    if (t.xCm < minX) minX = t.xCm
    if (t.yCm < minY) minY = t.yCm
    if (t.xCm+w > maxX) maxX = t.xCm+w
    if (t.yCm+h > maxY) maxY = t.yCm+h
  }
  const w = Math.round(maxX - minX), d = Math.round(maxY - minY)
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
    rotation: p.rotation ?? 0,
    overlapping: false,
  }))
  savedLayoutJson.value = JSON.stringify(placedTables.value.map(serialise))
  restoreAggSelections(r.aggregateSelections ?? [])
  savedAggSelectionsJson.value = JSON.stringify(buildAggSelectionsForSave())
  loading.value = false
  canvasWrapEl.value?.addEventListener('wheel', onWheelZoom, { passive: false })
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
        if (!templateMap.value[other.templateId]) return false
        const ow = effW(other), oh = effH(other)
        return x < other.xCm + ow &&
               x + tw > other.xCm &&
               y < other.yCm + oh &&
               y + td > other.yCm
      })
      if (!blocker) return { xCm: x, yCm: y }
      x = blocker.xCm + effW(blocker)
    }
  }
  return { xCm: 0, yCm: 0 } // fallback: no free space found
}

function generateUUID() {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID()
  }
  // Fallback for non-secure contexts (HTTP)
  return '10000000-1000-4000-8000-100000000000'.replace(/[018]/g, c =>
    (c ^ (crypto.getRandomValues(new Uint8Array(1))[0] & (15 >> (c / 4)))).toString(16)
  )
}

function addFromTemplate(tpl) {
  const { xCm, yCm } = findFreePosition(tpl.widthCm, tpl.depthCm)
  placedTables.value.push({
    instanceId: generateUUID(),
    templateId: tpl.id,
    xCm,
    yCm,
    rotation: 0,
    overlapping: false,
  })
}

// ── Remove ────────────────────────────────────────────────────────────────────
function removeTable(instanceId) {
  placedTables.value = placedTables.value.filter(p => p.instanceId !== instanceId)
}

function rotateTable(p) {
  const tpl = templateMap.value[p.templateId]
  if (!tpl || !room.value) return
  p.rotation = (p.rotation + 90) % 360
  const nw = p.rotation % 180 === 0 ? tpl.widthCm : tpl.depthCm
  const nh = p.rotation % 180 === 0 ? tpl.depthCm : tpl.widthCm
  p.xCm = Math.max(0, Math.min(room.value.widthCm - nw, p.xCm))
  p.yCm = Math.max(0, Math.min(room.value.depthCm - nh, p.yCm))
}

function rotateAggregate(aggIdx) {
  if (!room.value) return
  const group = aggregates.value[aggIdx]
  if (!group?.length) return

  const tables = group.map(id => placedTables.value.find(p => p.instanceId === id)).filter(Boolean)

  // Bounding box of the group
  let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity
  for (const t of tables) {
    const w = effW(t), h = effH(t)
    minX = Math.min(minX, t.xCm);      minY = Math.min(minY, t.yCm)
    maxX = Math.max(maxX, t.xCm + w);  maxY = Math.max(maxY, t.yCm + h)
  }
  const cx = (minX + maxX) / 2, cy = (minY + maxY) / 2

  // Compute new position and rotation for each table (90° CW around group center)
  const updates = tables.map(t => {
    const w = effW(t), h = effH(t)
    const ocx = t.xCm + w / 2, ocy = t.yCm + h / 2
    // 90° CW: new center = (cx + (ocy - cy), cy - (ocx - cx))
    const ncx = cx + (ocy - cy)
    const ncy = cy - (ocx - cx)
    const newRot = (t.rotation + 90) % 360
    const tpl = templateMap.value[t.templateId]
    const nw = newRot % 180 === 0 ? tpl.widthCm : tpl.depthCm
    const nh = newRot % 180 === 0 ? tpl.depthCm : tpl.widthCm
    return { t, newX: ncx - nw / 2, newY: ncy - nh / 2, newRot, nw, nh }
  })

  // New bounding box after rotation
  let nMinX = Infinity, nMinY = Infinity, nMaxX = -Infinity, nMaxY = -Infinity
  for (const u of updates) {
    nMinX = Math.min(nMinX, u.newX);       nMinY = Math.min(nMinY, u.newY)
    nMaxX = Math.max(nMaxX, u.newX + u.nw); nMaxY = Math.max(nMaxY, u.newY + u.nh)
  }

  // Translate to keep group within room bounds
  const anchorX = Math.max(0, Math.min(room.value.widthCm - (nMaxX - nMinX), nMinX))
  const anchorY = Math.max(0, Math.min(room.value.depthCm - (nMaxY - nMinY), nMinY))
  const shiftX = anchorX - nMinX, shiftY = anchorY - nMinY

  for (const u of updates) {
    u.t.rotation = u.newRot
    u.t.xCm = parseFloat((u.newX + shiftX).toFixed(2))
    u.t.yCm = parseFloat((u.newY + shiftY).toFixed(2))
  }
}

// ── Drag ─────────────────────────────────────────────────────────────────────
const draggingId = ref(null)
let _drag = null

function getEventCoords(e) {
  if (e.touches?.length) return { clientX: e.touches[0].clientX, clientY: e.touches[0].clientY }
  if (e.changedTouches?.length) return { clientX: e.changedTouches[0].clientX, clientY: e.changedTouches[0].clientY }
  return { clientX: e.clientX, clientY: e.clientY }
}

function beginDrag(placed, startClientX, startClientY, forceDetach) {
  const aggIdx = aggregateMap.value[placed.instanceId]
  const aggGroup = (aggIdx != null) ? aggregates.value[aggIdx] : [placed.instanceId]
  const isGroup = aggGroup.length > 1 && !forceDetach
  draggingId.value = placed.instanceId
  _drag = {
    instanceId: placed.instanceId,
    startMouseX: startClientX, startMouseY: startClientY,
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
}

// Mouse drag
function startDrag(e, placed) {
  if (e.button !== 0) return
  e.preventDefault()
  beginDrag(placed, e.clientX, e.clientY, e.altKey)
  window.addEventListener('mousemove', onMove)
  window.addEventListener('mouseup', onUp)
}

// Touch drag — long press (500 ms) arms detach mode before the drag commits
const longPressArmedId = ref(null)
let _longPressTimer = null
let _touchPending = null

function onTouchStart(e, placed) {
  if (e.touches.length !== 1) return
  const touch = e.touches[0]
  _touchPending = { placed, clientX: touch.clientX, clientY: touch.clientY }
  longPressArmedId.value = null
  _longPressTimer = setTimeout(() => {
    longPressArmedId.value = placed.instanceId
    navigator.vibrate?.(30)
  }, 500)
  window.addEventListener('touchmove', onTouchMovePending, { passive: false })
  window.addEventListener('touchend', onTouchEndPending)
}

function onTouchMovePending(e) {
  if (!_touchPending) return
  const touch = e.touches[0]
  const dx = touch.clientX - _touchPending.clientX
  const dy = touch.clientY - _touchPending.clientY
  if (!longPressArmedId.value && Math.hypot(dx, dy) < 5) return

  e.preventDefault()
  clearTimeout(_longPressTimer)
  _longPressTimer = null
  window.removeEventListener('touchmove', onTouchMovePending)
  window.removeEventListener('touchend', onTouchEndPending)

  const forceDetach = !!longPressArmedId.value
  longPressArmedId.value = null
  const { placed, clientX, clientY } = _touchPending
  _touchPending = null

  beginDrag(placed, clientX, clientY, forceDetach)
  window.addEventListener('touchmove', onMove, { passive: false })
  window.addEventListener('touchend', onUp)
  onMove(e)
}

function onTouchEndPending() {
  clearTimeout(_longPressTimer)
  _longPressTimer = null
  longPressArmedId.value = null
  _touchPending = null
  window.removeEventListener('touchmove', onTouchMovePending)
  window.removeEventListener('touchend', onTouchEndPending)
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

  const cw = p.rotation % 180 === 0 ? tpl.widthCm : tpl.depthCm
  const cd = p.rotation % 180 === 0 ? tpl.depthCm : tpl.widthCm

  // Step 1+2: raw position → 1 cm snap → room-bounds clamp
  const { clientX, clientY } = getEventCoords(e)
  const rawX = _drag.startX + (clientX - _drag.startMouseX) / (SCALE * zoom.value)
  const rawY = _drag.startY + (clientY - _drag.startMouseY) / (SCALE * zoom.value)
  let cx = parseFloat((Math.max(0, Math.min(room.value.widthCm - cw, Math.round(rawX / SNAP) * SNAP))).toFixed(2))
  let cy = parseFloat((Math.max(0, Math.min(room.value.depthCm - cd, Math.round(rawY / SNAP) * SNAP))).toFixed(2))

  // Step 3: border-to-border snap (per axis, independently)
  const others = placedTables.value.filter(t => t.instanceId !== _drag.instanceId)
  let bestDX = BORDER_SNAP + 1
  let bestDY = BORDER_SNAP + 1
  let snapX = cx
  let snapY = cy

  for (const other of others) {
    if (!templateMap.value[other.templateId]) continue
    const nx = other.xCm, ny = other.yCm, nw = effW(other), nd = effH(other)

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
    if (!templateMap.value[other.templateId]) return false
    return cx < other.xCm + effW(other) &&
           cx + cw > other.xCm &&
           cy < other.yCm + effH(other) &&
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

  const { clientX: mx, clientY: my } = getEventCoords(e)
  const rawDx = (mx - _drag.startMouseX) / (SCALE * zoom.value)
  const rawDy = (my - _drag.startMouseY) / (SCALE * zoom.value)
  const snapDx = parseFloat((Math.round(rawDx / SNAP) * SNAP).toFixed(2))
  const snapDy = parseFloat((Math.round(rawDy / SNAP) * SNAP).toFixed(2))

  const proposed = gsp.map(sp => ({
    instanceId: sp.instanceId,
    newX: sp.startX + snapDx,
    newY: sp.startY + snapDy,
  }))

  // Bounding box → clamp correction (uniform shift)
  let clampDx = 0, clampDy = 0
  for (const pr of proposed) {
    const t = placedTables.value.find(p => p.instanceId === pr.instanceId)
    if (!templateMap.value[t.templateId]) continue
    const tw = effW(t), th = effH(t)
    if (pr.newX < 0) clampDx = Math.max(clampDx, -pr.newX)
    if (pr.newX + tw > room.value.widthCm)
      clampDx = Math.min(clampDx, room.value.widthCm - pr.newX - tw)
    if (pr.newY < 0) clampDy = Math.max(clampDy, -pr.newY)
    if (pr.newY + th > room.value.depthCm)
      clampDy = Math.min(clampDy, room.value.depthCm - pr.newY - th)
  }
  for (const pr of proposed) { pr.newX += clampDx; pr.newY += clampDy }

  // Overlap check vs non-group tables
  const hasOverlap = proposed.some(pr => {
    const t = placedTables.value.find(p => p.instanceId === pr.instanceId)
    if (!templateMap.value[t.templateId]) return false
    const tw = effW(t), th = effH(t)
    return nonGroup.some(other => {
      if (!templateMap.value[other.templateId]) return false
      return pr.newX < other.xCm + effW(other) && pr.newX + tw > other.xCm &&
             pr.newY < other.yCm + effH(other) && pr.newY + th > other.yCm
    })
  })

  if (!hasOverlap) {
    for (const pr of proposed) {
      const t = placedTables.value.find(p => p.instanceId === pr.instanceId)
      if (t) {
        t.xCm = parseFloat(pr.newX.toFixed(2))
        t.yCm = parseFloat(pr.newY.toFixed(2))
      }
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
    const { clientX, clientY } = getEventCoords(e)
    const moved = Math.abs(clientX - _drag.startMouseX) + Math.abs(clientY - _drag.startMouseY)
    if (moved < 5) {
      const idx = aggregateMap.value[_drag.instanceId]
      selectedAggregateId.value = idx ?? null
    }
  }
  _drag = null
  draggingId.value = null
  window.removeEventListener('mousemove', onMove)
  window.removeEventListener('mouseup', onUp)
  window.removeEventListener('touchmove', onMove)
  window.removeEventListener('touchend', onUp)
}

function onCanvasClick(e) {
  if (e.target === e.currentTarget) selectedAggregateId.value = null
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

onUnmounted(() => {
  window.removeEventListener('mousemove', onMove)
  window.removeEventListener('mouseup', onUp)
  window.removeEventListener('touchmove', onMove)
  window.removeEventListener('touchend', onUp)
  window.removeEventListener('touchmove', onTouchMovePending)
  window.removeEventListener('touchend', onTouchEndPending)
  clearTimeout(_longPressTimer)
  canvasWrapEl.value?.removeEventListener('wheel', onWheelZoom)
})

// ── Rename ────────────────────────────────────────────────────────────────────
function startRename() {
  renameInput.value = room.value.name
  renameError.value = ''
  renameMode.value = true
}

function cancelRename() {
  renameMode.value = false
  renameError.value = ''
}

async function saveRename() {
  renameError.value = ''
  if (!renameInput.value.trim()) { renameError.value = 'Name is required.'; return }
  try {
    const updated = await updateRoom(roomId, {
      name: renameInput.value.trim(),
      widthCm: room.value.widthCm,
      depthCm: room.value.depthCm,
      version: room.value.version,
    })
    room.value.name = updated.name
    room.value.version = updated.version
    renameMode.value = false
  } catch (err) {
    renameError.value = err.message
  }
}

// ── Export PDF ────────────────────────────────────────────────────────────────
function exportPdf() {
  pdfExporting.value = true
  try {
    generateRoomPdf(room.value, templates.value, placedTables.value, aggregates.value)
  } finally {
    pdfExporting.value = false
  }
}

// ── Save ──────────────────────────────────────────────────────────────────────
async function saveLayout() {
  const aggSels = buildAggSelectionsForSave()
  const updated = await saveRoomLayout(roomId, placedTables.value.map(serialise), aggSels, room.value.version)
  savedLayoutJson.value = JSON.stringify(placedTables.value.map(serialise))
  savedAggSelectionsJson.value = JSON.stringify(aggSels)
  if (updated) room.value.version = updated.version
  saveSuccess.value = true
  setTimeout(() => { saveSuccess.value = false }, 2500)
}
</script>

<template>
  <div class="planner-page">
    <div class="planner-header">
      <button class="back-btn" @click="router.push('/table-planner')">&larr; Table Planner</button>
      <template v-if="room">
        <template v-if="renameMode">
          <input class="rename-input" v-model="renameInput" @keyup.enter="saveRename" @keyup.esc="cancelRename" />
          <button class="small-btn" @click="saveRename">Save</button>
          <button class="small-btn" @click="cancelRename">Cancel</button>
          <span v-if="renameError" class="rename-error">{{ renameError }}</span>
        </template>
        <template v-else>
          <h1>{{ room.name }}</h1>
          <button class="rename-btn" @click="startRename" title="Rename room">✎</button>
        </template>
      </template>
      <div class="header-right">
        <button class="toggle-btn" :class="{ active: showGrid }" @click="showGrid = !showGrid" title="Toggle grid">Grid</button>
        <div class="zoom-controls">
          <button class="zoom-btn" @click="zoomOut" :disabled="zoom <= MIN_ZOOM" title="Zoom out">−</button>
          <button class="zoom-reset" @click="resetZoom" title="Reset zoom">{{ Math.round(zoom * 100) }}%</button>
          <button class="zoom-btn" @click="zoomIn" :disabled="zoom >= MAX_ZOOM" title="Zoom in">+</button>
        </div>
        <span v-if="saveSuccess" class="save-ok">Layout saved!</span>
        <button class="pdf-btn" :disabled="pdfExporting" @click="exportPdf" title="Export layout as PDF">
          {{ pdfExporting ? 'Exporting…' : 'Export PDF' }}
        </button>
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
          :title="`${t.widthCm}x${t.depthCm} cm`"
        >{{ t.description }}</button>
        <span v-if="templates.length === 0" class="empty-palette">No templates — create some in Table Planner.</span>
        <span class="palette-hint">Alt+drag (or long press on touch) to detach a table from its group</span>
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
                    ({{ bpMmW(distinctBaseplates.find(b => b.key === aggSelections[idx])) }} mm
                    &times;
                    {{ bpMmH(distinctBaseplates.find(b => b.key === aggSelections[idx])) }} mm each)
                  </span>
                </template>
                <span v-else class="calc-hint">—</span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Canvas -->
      <div class="canvas-wrap" ref="canvasWrapEl">
        <div class="canvas-zoom-container" :style="{ width: canvasWidth * zoom + 'px', height: canvasHeight * zoom + 'px' }">
        <div
          class="canvas"
          :class="{ 'canvas--grid': showGrid }"
          :style="{ width: canvasWidth + 'px', height: canvasHeight + 'px', transform: `scale(${zoom})`, transformOrigin: 'top left' }"
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
              'table-item--lp-armed': longPressArmedId === p.instanceId,
            }"
            :style="{
              left: p.xCm * SCALE + 'px',
              top: p.yCm * SCALE + 'px',
              width: effW(p) * SCALE + 'px',
              height: effH(p) * SCALE + 'px',
              background: templateMap[p.templateId]?.color ?? '#8b6340',
            }"
            @mousedown="startDrag($event, p)"
            @touchstart.prevent="onTouchStart($event, p)"
          >
            <button class="remove-btn" @mousedown.stop @click="removeTable(p.instanceId)" title="Remove">✕</button>
            <button class="rotate-btn" @mousedown.stop @click="rotateTable(p)" title="Rotate 90°">⟳</button>
            <span class="table-label">{{ templateMap[p.templateId]?.description ?? '?' }}</span>
            <span class="table-dims" v-if="templateMap[p.templateId]">
              {{ effW(p) }}x{{ effH(p) }} cm
            </span>
          </div>

          <!-- Aggregate bounding-box overlay -->
          <div
            v-if="selectedAggregateId !== null && selectedAggregateBBox"
            class="agg-bbox"
            :style="{
              left:   selectedAggregateBBox.minX * SCALE + 'px',
              top:    selectedAggregateBBox.minY * SCALE + 'px',
              width:  (selectedAggregateBBox.maxX - selectedAggregateBBox.minX) * SCALE + 'px',
              height: (selectedAggregateBBox.maxY - selectedAggregateBBox.minY) * SCALE + 'px',
            }"
          >
            <button
              class="agg-rotate-handle"
              @mousedown.stop
              @click.stop="rotateAggregate(selectedAggregateId)"
              title="Rotate group 90°"
            >⟳</button>
          </div>

          <div class="scale-bar">
            <div class="scale-line"></div>
            <span>1 m</span>
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
  flex: 1;
  padding: 0.75rem 1.25rem 0;
  box-sizing: border-box;
  overflow: hidden;
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

.pdf-btn {
  background: #f0f0f0;
  border: 1px solid #ccc;
  border-radius: 4px;
  padding: 0.35rem 0.9rem;
  font-size: 0.85rem;
  cursor: pointer;
  color: #444;
  transition: background 0.15s;
}

.pdf-btn:hover:not(:disabled) { background: #e0e0e0; }
.pdf-btn:disabled { color: #aaa; cursor: default; }

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

.zoom-controls {
  display: flex;
  align-items: center;
  gap: 0;
  border: 1px solid #ccc;
  border-radius: 4px;
  overflow: hidden;
}

.zoom-btn {
  background: #f0f0f0;
  border: none;
  padding: 0.3rem 0.6rem;
  font-size: 1rem;
  line-height: 1;
  cursor: pointer;
  color: #333;
  font-weight: 600;
  min-width: 28px;
}

.zoom-btn:hover:not(:disabled) { background: #e0e0e0; }
.zoom-btn:disabled { color: #bbb; cursor: default; }

.zoom-reset {
  background: #f8f8f8;
  border: none;
  border-left: 1px solid #ccc;
  border-right: 1px solid #ccc;
  padding: 0.3rem 0.5rem;
  font-size: 0.78rem;
  cursor: pointer;
  color: #555;
  min-width: 46px;
  text-align: center;
}

.zoom-reset:hover { background: #e8e8e8; }

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

.canvas-zoom-container {
  position: relative;
}

.canvas {
  position: relative;
  background-color: #fff;
}

.canvas--grid {
  /* major lines every 32 studs = 25.6 cm = 25.6 px; minor every 8 studs = 6.4 px */
  background-image:
    linear-gradient(to right,  #9aa8bb 1px, transparent 1px),
    linear-gradient(to bottom, #9aa8bb 1px, transparent 1px),
    linear-gradient(to right,  #dde4ef 1px, transparent 1px),
    linear-gradient(to bottom, #dde4ef 1px, transparent 1px);
  background-size: 25.6px 25.6px, 25.6px 25.6px, 6.4px 6.4px, 6.4px 6.4px;
}

/* ── Table item ───────────────────────────────────────────────────────────── */
.table-item {
  position: absolute;
  border: 2px solid rgba(0,0,0,0.3);
  border-radius: 4px;
  cursor: grab;
  user-select: none;
  touch-action: none;
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

@keyframes lp-armed-pulse {
  0%   { box-shadow: 0 0 0 0   rgba(255, 160, 0, 0.9), 0 2px 8px rgba(0,0,0,0.2); }
  60%  { box-shadow: 0 0 0 10px rgba(255, 160, 0, 0.4), 0 2px 8px rgba(0,0,0,0.2); }
  100% { box-shadow: 0 0 0 14px rgba(255, 160, 0, 0),   0 2px 8px rgba(0,0,0,0.2); }
}

.table-item--lp-armed {
  border-color: rgba(255, 160, 0, 0.9);
  animation: lp-armed-pulse 0.4s ease-out forwards;
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

/* ── Rotate button ────────────────────────────────────────────────────────── */
.rotate-btn {
  position: absolute;
  top: 3px;
  left: 4px;
  background: rgba(0,0,0,0.2);
  border: none;
  border-radius: 3px;
  color: #fff;
  font-size: 0.65rem;
  width: 15px;
  height: 15px;
  padding: 0;
  line-height: 1;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
}

.rotate-btn:hover { background: rgba(30,90,200,0.65); }

/* ── Rename ───────────────────────────────────────────────────────────────── */
.rename-input {
  padding: 0.25rem 0.5rem;
  border: 1px solid #3a6ea5;
  border-radius: 4px;
  font-size: 1rem;
  font-weight: 600;
  min-width: 160px;
}

.rename-btn {
  background: none;
  border: none;
  cursor: pointer;
  font-size: 1rem;
  color: #aaa;
  padding: 0 2px;
  line-height: 1;
}

.rename-btn:hover { color: #3a6ea5; }

.rename-error {
  color: #c00;
  font-size: 0.82rem;
}

.small-btn {
  background: #f0f0f0;
  border: 1px solid #ccc;
  border-radius: 4px;
  padding: 0.2rem 0.5rem;
  font-size: 0.82rem;
  cursor: pointer;
}

.small-btn:hover { background: #e0e0e0; }

/* ── Aggregate bounding-box overlay ──────────────────────────────────────── */
.agg-bbox {
  position: absolute;
  border: 2px dashed #1a90d0;
  border-radius: 3px;
  pointer-events: none;
  z-index: 8;
}

.agg-rotate-handle {
  position: absolute;
  top: 0;
  right: 0;
  transform: translate(50%, -50%);
  width: 22px;
  height: 22px;
  background: #1a90d0;
  border: 2px solid #fff;
  border-radius: 50%;
  color: #fff;
  font-size: 0.9rem;
  line-height: 1;
  cursor: pointer;
  pointer-events: all;
  display: flex;
  align-items: center;
  justify-content: center;
  box-shadow: 0 2px 6px rgba(0,0,0,0.35);
  transition: background 0.1s, transform 0.1s;
}

.agg-rotate-handle:hover {
  background: #0d6ea0;
  transform: translate(50%, -50%) scale(1.15);
}
</style>
