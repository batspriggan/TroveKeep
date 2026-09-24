<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getRoom, getAllTemplates, saveAggregateBpLayout } from '../../api/tableplanner.js'
import { getAllBaseplates, getBaseplateImageUrl, getPlannerEntities } from '../../api/baseplates.js'
import { getSet } from '../../api/sets.js'
import { generateAggregatePdf } from '../../utils/platePdf.js'

const route = useRoute()
const router = useRouter()
const roomId = route.params.roomId
const repId  = route.params.repId

// px per mm at zoom=1  (1 stud=8mm; 32×32 Standard plate = 255.8mm ≈ 128px)
const SCALE = 0.5
const PADDING_MM = 80     // canvas padding around the aggregate bounding box
const SNAP_MM = 12        // edge-to-edge snap threshold in mm
const MIN_ZOOM = 0.25
const MAX_ZOOM = 4

const room      = ref(null)
const templates = ref([])
const baseplates = ref([])
const plannerEntities = ref([])
const loading   = ref(true)
const saveSuccess = ref(false)
const pdfExporting = ref(false)
const zoom = ref(1)
const canvasWrapEl = ref(null)

// Set/MOC planner gate (contract §F): the planner refuses to open while the
// catalogue contains quarantined rows. Recomputed on every load — never cached.
const blocked = ref(false)
const quarantinedPlates = ref([])
const plannerError = ref('')

// Placed baseplates: { instanceId, placementId, baseplateId, xMm, yMm, rotation, sourceSetId }
// xMm/yMm are relative to aggregate bounding-box origin (top-left)
//
// `placementId` identifies a single placed INSTANCE (a MOC/set block, or null for
// individually placed plates); `sourceSetId` is only the label of which MOC/set the
// instance belongs to. Grouping (outline, label, drag, remove) keys on `placementId`
// so two instances of the same MOC stay independent. Both fields are persisted by the
// API, so placements survive a save/reload exactly (a legacy fallback in `onMounted`
// covers layouts written before `placementId` existed).
const placedPlates   = ref([])
const savedJson      = ref('[]')
const layoutVersion  = ref(null)   // null = never saved; 0 = legacy; 1+ = current

// Owned quantity per set id (for the non-blocking "you own N" warning).
const setQuantities = ref({})

const isDirty  = computed(() => JSON.stringify(serialisePlates()) !== savedJson.value)
const needsFix = computed(() => layoutVersion.value === 0 && placedPlates.value.length > 0)

function serialisePlates() {
  return placedPlates.value.map(p => ({
    instanceId: p.instanceId, baseplateId: p.baseplateId,
    xMm: p.xMm, yMm: p.yMm, rotation: p.rotation,
    sourceSetId: p.sourceSetId ?? null,
    placementId: p.placementId ?? null,
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

// ── Side-panel category grouping ─────────────────────────────────────────────
const CATEGORY_ORDER = ['Standard', 'Road', 'Custom']

const baseplatesByCategory = computed(() => {
  const groups = {}
  for (const bp of baseplates.value) {
    const cat = bp.type ?? 'Standard'
    ;(groups[cat] ??= []).push(bp)
  }
  // Return in a stable order
  return CATEGORY_ORDER
    .filter(c => groups[c])
    .map(c => ({ category: c, items: groups[c] }))
})

// Track which categories are collapsed (by name)
const collapsedCategories = ref(new Set())
// MOC / Sets section collapse state
const collapsedSets = ref(false)

// ── Hover tooltip ─────────────────────────────────────────────────────────────
const hoveredBp   = ref(null)
const tooltipTop  = ref(0)
const tooltipLeft = ref(0)
let _tooltipTimer = null

function showTooltip(e, bp) {
  const rect = e.currentTarget.getBoundingClientRect()
  const top  = Math.min(rect.top, window.innerHeight - 270)
  const left = rect.right + 10
  clearTimeout(_tooltipTimer)
  _tooltipTimer = setTimeout(() => {
    tooltipTop.value  = top
    tooltipLeft.value = left
    hoveredBp.value   = bp
  }, 1000)
}
function hideTooltip() {
  clearTimeout(_tooltipTimer)
  hoveredBp.value = null
}

// ── BFS aggregate detection (same as RoomPlannerView) ─────────────────────────
function rangeOverlaps(a1, a2, b1, b2) { return Math.min(a2, b2) - Math.max(a1, b1) > 0 }

function areAdjacent(a, wA, hA, b, wB, hB) {
  const T = 0.5
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

function computeAggregates(layout, tMap) {
  const n = layout.length
  if (n === 0) return []
  const visited = new Array(n).fill(false)
  const result = []
  for (let i = 0; i < n; i++) {
    if (visited[i]) continue
    const a0 = layout[i]
    if (!tMap[a0.templateId]) { visited[i] = true; continue }
    const group = [], queue = [i]
    visited[i] = true
    while (queue.length) {
      const cur = queue.shift()
      group.push(layout[cur].instanceId)
      const a = layout[cur], tplA = tMap[a.templateId]
      if (!tplA) continue
      const wA = a.rotation % 180 === 0 ? tplA.widthCm : tplA.depthCm
      const hA = a.rotation % 180 === 0 ? tplA.depthCm : tplA.widthCm
      for (let j = 0; j < n; j++) {
        if (visited[j]) continue
        const b = layout[j], tplB = tMap[b.templateId]
        if (!tplB) continue
        const wB = b.rotation % 180 === 0 ? tplB.widthCm : tplB.depthCm
        const hB = b.rotation % 180 === 0 ? tplB.depthCm : tplB.widthCm
        if (areAdjacent(a, wA, hA, b, wB, hB)) { visited[j] = true; queue.push(j) }
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
    const w = t.rotation % 180 === 0 ? tpl.widthCm : tpl.depthCm
    const h = t.rotation % 180 === 0 ? tpl.depthCm : tpl.widthCm
    if (t.xCm < minX) minX = t.xCm
    if (t.yCm < minY) minY = t.yCm
    if (t.xCm + w > maxX) maxX = t.xCm + w
    if (t.yCm + h > maxY) maxY = t.yCm + h
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
// Standard/Road plates and Custom plates with stud counts that are multiples of 8:
// studs×8 − 0.2 mm (0.1 mm clearance each side, per LEGO spec).
// Custom plates with non-standard stud counts: studs×8 − 2 mm.
function isStandardGeometry(bp) {
  return bp.type !== 'Custom' || (bp.widthStuds % 8 === 0 && bp.depthStuds % 8 === 0)
}
function bpNaturalW(bp) { return bp.widthStuds  * 8 - (isStandardGeometry(bp) ? 0.2 : 2) }
function bpNaturalH(bp) { return bp.depthStuds  * 8 - (isStandardGeometry(bp) ? 0.2 : 2) }

function effectiveW(bp, rotation) {
  return (rotation === 0 || rotation === 180) ? bpNaturalW(bp) : bpNaturalH(bp)
}
function effectiveH(bp, rotation) {
  return (rotation === 0 || rotation === 180) ? bpNaturalH(bp) : bpNaturalW(bp)
}

// ── MOC / Set planner entities ────────────────────────────────────────────────
// Sets/MOCs that own baseplate reservations (contract §G). They are NOT plates:
// dropping one decomposes it into its real component plates, all tagged with the
// set id in `sourceSetId`.
const setNameById = computed(() => {
  const m = {}
  for (const e of plannerEntities.value) m[e.setId] = e.name
  return m
})

function entityName(setId) {
  return setNameById.value[setId] ?? 'Set'
}

// Plates carrying a non-null placementId, grouped by instance (NOT by set: two
// instances of the same MOC share `sourceSetId` but have distinct `placementId`).
const plateGroups = computed(() => {
  const groups = new Map()
  for (const p of placedPlates.value) {
    if (!p.placementId) continue
    if (!groups.has(p.placementId)) groups.set(p.placementId, [])
    groups.get(p.placementId).push(p)
  }
  return groups
})

// Per-instance bounding box (in placement mm, i.e. aggregate-relative) for the
// visual outline drawn under the plates. The label comes from the group's set.
const groupOutlines = computed(() => {
  const out = []
  for (const [placementId, plates] of plateGroups.value) {
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity
    for (const p of plates) {
      const bp = baseplateMap.value[p.baseplateId]
      if (!bp) continue
      const w = effectiveW(bp, p.rotation), h = effectiveH(bp, p.rotation)
      minX = Math.min(minX, p.xMm)
      minY = Math.min(minY, p.yMm)
      maxX = Math.max(maxX, p.xMm + w)
      maxY = Math.max(maxY, p.yMm + h)
    }
    if (!isFinite(minX)) continue
    const setId = plates[0]?.sourceSetId ?? null
    out.push({ placementId, setId, name: entityName(setId), xMm: minX, yMm: minY, wMm: maxX - minX, hMm: maxY - minY })
  }
  return out
})

const selectedPlacementId = computed(() => {
  const p = placedPlates.value.find(p => p.instanceId === selectedId.value)
  return p?.placementId ?? null
})

function isGroupSelected(p) {
  return !!p.placementId && p.placementId === selectedPlacementId.value
}

// Group members (same placementId) including the given plate; single plates → [p].
function groupMembers(p) {
  if (!p?.placementId) return p ? [p] : []
  return placedPlates.value.filter(x => x.placementId === p.placementId)
}

// ── Owned-quantity warning (non-blocking, warning only) ────────────────────────
// Counts placed INSTANCES (distinct placementId) per set and compares them with
// the owned quantity of that set. The comparison never blocks a placement.
async function ensureSetQuantity(setId) {
  if (!setId || Object.prototype.hasOwnProperty.call(setQuantities.value, setId)) return
  try {
    const s = await getSet(setId)
    if (s?.id) setQuantities.value = { ...setQuantities.value, [s.id]: s.quantity ?? 1 }
  } catch {
    // Quantity unknown (set deleted / request failed) → no warning for this set.
  }
}

const overUsedSets = computed(() => {
  const instancesBySet = new Map()
  for (const p of placedPlates.value) {
    if (!p.sourceSetId || !p.placementId) continue
    if (!instancesBySet.has(p.sourceSetId)) instancesBySet.set(p.sourceSetId, new Set())
    instancesBySet.get(p.sourceSetId).add(p.placementId)
  }
  const out = []
  for (const [setId, ids] of instancesBySet) {
    const owned = setQuantities.value[setId]
    if (typeof owned === 'number' && ids.size > owned) {
      out.push({ setId, name: entityName(setId), used: ids.size, owned })
    }
  }
  return out
})

// ── placementId derivation for legacy layouts ─────────────────────────────────
// The API now persists `placementId`, so saved layouts round-trip exactly. This
// fallback only covers layouts written before the field existed: plates sharing a
// `sourceSetId` are segmented back into the rigid grids they were created from (the
// same cols×rows layout as `addPlannerEntity`) and each segment becomes one instance
// named `moc:<setId>:<anchorX>_<anchorY>` (anchor = segment min x/y).
function segmentSetInstances(plates, setId) {
  const ent = plannerEntities.value.find(e => e.setId === setId)
  const moduleBp = ent ? baseplateMap.value[ent.moduleBaseplateId] : null
  const cols = ent?.cols > 0 ? ent.cols : 0
  const rows = ent?.rows > 0 ? ent.rows : 0
  const total = Math.max(1, ent?.totalPlates || (cols * rows) || 1)
  const mw = moduleBp ? bpNaturalW(moduleBp) : 0
  const mh = moduleBp ? bpNaturalH(moduleBp) : 0

  const remaining = [...plates]
  const segments = []

  // Greedy: from the top-left-most remaining plate, try to take a full rigid grid.
  while (remaining.length) {
    let anchorIdx = 0
    for (let i = 1; i < remaining.length; i++) {
      const a = remaining[i], b = remaining[anchorIdx]
      if (a.yMm < b.yMm - 0.5 || (Math.abs(a.yMm - b.yMm) < 0.5 && a.xMm < b.xMm)) anchorIdx = i
    }
    const anchor = remaining[anchorIdx]
    let cells = null
    if (cols > 0 && rows > 0 && mw > 0 && mh > 0 && remaining.length >= total) {
      const picked = []
      for (let i = 0; i < total; i++) {
        const x = anchor.xMm + (i % cols) * mw
        const y = anchor.yMm + Math.floor(i / cols) * mh
        const idx = remaining.findIndex(p => Math.abs(p.xMm - x) < 0.5 && Math.abs(p.yMm - y) < 0.5)
        if (idx === -1) { picked.length = 0; break }
        picked.push(idx)
      }
      if (picked.length) cells = picked
    }
    if (cells) {
      // Remove from the end so earlier indices stay valid.
      const taken = cells.map(i => remaining[i])
      for (const i of [...cells].sort((a, b) => b - a)) remaining.splice(i, 1)
      segments.push(taken)
    } else {
      // Plate that does not fit a grid (legacy/manual geometry): own instance.
      remaining.splice(anchorIdx, 1)
      segments.push([anchor])
    }
  }
  return segments
}

function assignDerivedPlacementIds(plates) {
  const bySet = new Map()
  for (const p of plates) {
    if (!p.sourceSetId) continue
    if (!bySet.has(p.sourceSetId)) bySet.set(p.sourceSetId, [])
    bySet.get(p.sourceSetId).push(p)
  }
  for (const [setId, setPlates] of bySet) {
    for (const segment of segmentSetInstances(setPlates, setId)) {
      const anchorX = Math.round(Math.min(...segment.map(p => p.xMm)))
      const anchorY = Math.round(Math.min(...segment.map(p => p.yMm)))
      const pid = `moc:${setId}:${anchorX}_${anchorY}`
      for (const p of segment) p.placementId = pid
    }
  }
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
  const [r, tpls, bps, entities] = await Promise.all([
    getRoom(roomId),
    getAllTemplates(),
    getAllBaseplates(),
    getPlannerEntities().catch(() => []),
  ])
  room.value = r
  templates.value = tpls
  baseplates.value = bps
  plannerEntities.value = entities ?? []

  // Planner gate (contract §F): if any catalogue row is quarantined the canvas
  // must not render at all. Re-evaluated on every load → automatic unblock.
  quarantinedPlates.value = bps.filter(b => b.quarantined === true)
  blocked.value = quarantinedPlates.value.length > 0

  // Restore saved layout for this aggregate. Saved plates keep their `instanceId`,
  // `placementId` (the placed instance) and `sourceSetId` (the MOC it belongs to).
  const saved = r.aggregateBpLayouts?.find(l => l.representativeId === repId)
  placedPlates.value = (saved?.placedBaseplates ?? []).map(p => ({
    ...p,
    sourceSetId: p.sourceSetId ?? null,
    placementId: p.placementId ?? null,
  }))
  layoutVersion.value = saved ? (saved.layoutVersion ?? 0) : null
  // Legacy layouts (saved before `placementId` existed) have MOC plates tagged with a
  // `sourceSetId` but no instance id: derive one so they stay draggable as a group.
  if (placedPlates.value.some(p => p.sourceSetId && !p.placementId)) {
    assignDerivedPlacementIds(placedPlates.value)
  }

  // Owned quantities for the sets present in this layout (warning only).
  const layoutSetIds = [...new Set(placedPlates.value.map(p => p.sourceSetId).filter(Boolean))]
  const sets = await Promise.all(layoutSetIds.map(id => getSet(id).catch(() => null)))
  const quantities = {}
  for (const s of sets) if (s?.id) quantities[s.id] = s.quantity ?? 1
  setQuantities.value = quantities

  savedJson.value = JSON.stringify(serialisePlates())
  loading.value = false

  canvasWrapEl.value?.addEventListener('wheel', onWheelZoom, { passive: false })
  window.addEventListener('mousemove', onPaletteMove)
  window.addEventListener('mouseup', onPaletteUp)
})

onUnmounted(() => {
  window.removeEventListener('mousemove', onMove)
  window.removeEventListener('mouseup', onUp)
  window.removeEventListener('touchmove', onMove)
  window.removeEventListener('touchend', onUp)
  window.removeEventListener('mousemove', onPaletteMove)
  window.removeEventListener('mouseup', onPaletteUp)
  window.removeEventListener('touchmove', onPaletteMove)
  window.removeEventListener('touchend', onPaletteUp)
  canvasWrapEl.value?.removeEventListener('wheel', onWheelZoom)
  clearTimeout(_tooltipTimer)
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
    placementId: null,          // individually placed plate → not a MOC instance
    baseplateId: bp.id,
    xMm,
    yMm,
    rotation: 0,
    sourceSetId: null,
  })
}

// ── MOC / Set placement ───────────────────────────────────────────────────────
// A MOC/set is not a plate: dropping it decomposes into its `totalPlates` real
// component plates (contract §G), laid out cols×rows from the drop point and all
// tagged with `sourceSetId`. The module dimensions come from the catalogue.
function snapToStud(mm) { return Math.round(mm / 8) * 8 }

function addPlannerEntity(entity, at = null) {
  plannerError.value = ''
  const moduleBp = baseplateMap.value[entity.moduleBaseplateId]
  if (!moduleBp) {
    plannerError.value = `Cannot place "${entity.name}": its module baseplate is not in the catalogue.`
    return
  }

  const mw = bpNaturalW(moduleBp)
  const mh = bpNaturalH(moduleBp)
  const cols = Math.max(1, entity.cols || 1)
  const rows = Math.max(1, entity.rows || 1)
  const total = Math.max(1, entity.totalPlates ?? cols * rows)
  const blockW = cols * mw
  const blockH = rows * mh

  const buildCells = (bx, by) => {
    const cells = []
    for (let i = 0; i < total; i++) {
      cells.push({ xMm: bx + (i % cols) * mw, yMm: by + Math.floor(i / cols) * mh })
    }
    return cells
  }

  // Anchor: the requested drop point (snapped to the stud grid) or a free slot.
  let baseX, baseY
  if (at) {
    baseX = snapToStud(at.xMm)
    baseY = snapToStud(at.yMm)
  } else {
    const free = findFreePosition(blockW, blockH)
    baseX = free.xMm
    baseY = free.yMm
  }

  let cells = buildCells(baseX, baseY)
  // Same overlap rule as single plates: the whole block must not sit on other plates.
  if (cells.some(c => hasOverlap(c.xMm, c.yMm, mw, mh, null))) {
    const free = findFreePosition(blockW, blockH)
    baseX = free.xMm
    baseY = free.yMm
    cells = buildCells(baseX, baseY)
    if (cells.some(c => hasOverlap(c.xMm, c.yMm, mw, mh, null))) {
      plannerError.value = `Cannot place "${entity.name}": not enough free space.`
      return
    }
  }

  // One `placementId` for the whole block: it identifies THIS instance. Dropping
  // the same MOC again yields a different placementId, so the two blocks stay
  // independently movable/removable. `sourceSetId` remains only a label.
  const placementId = generateUUID()
  for (const c of cells) {
    placedPlates.value.push({
      instanceId: generateUUID(),
      placementId,
      baseplateId: entity.moduleBaseplateId,
      xMm: c.xMm,
      yMm: c.yMm,
      rotation: 0,
      sourceSetId: entity.setId,
    })
  }
  ensureSetQuantity(entity.setId)
}

// ── Remove (instance-aware) ────────────────────────────────────────────────────
function removePlate(instanceId) {
  const plate = placedPlates.value.find(p => p.instanceId === instanceId)
  // A plate belonging to a placed MOC/set removes the whole instance (placementId).
  const doomed = new Set(
    plate && plate.placementId
      ? placedPlates.value.filter(p => p.placementId === plate.placementId).map(p => p.instanceId)
      : [instanceId]
  )
  placedPlates.value = placedPlates.value.filter(p => !doomed.has(p.instanceId))
  if (selectedId.value && doomed.has(selectedId.value)) selectedId.value = null
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
const draggingGroupId = ref(null)
let _drag = null

function getEventCoords(e) {
  if (e.touches?.length) return { clientX: e.touches[0].clientX, clientY: e.touches[0].clientY }
  if (e.changedTouches?.length) return { clientX: e.changedTouches[0].clientX, clientY: e.changedTouches[0].clientY }
  return { clientX: e.clientX, clientY: e.clientY }
}

function startDrag(e, plate) {
  if (e.type !== 'touchstart' && e.button !== 0) return
  e.preventDefault()
  selectedId.value = plate.instanceId
  draggingId.value = plate.instanceId
  const { clientX, clientY } = getEventCoords(e)
  const members = groupMembers(plate)
  const isGroup = members.length > 1
  draggingGroupId.value = isGroup ? plate.placementId : null
  _drag = {
    instanceId: plate.instanceId,
    startMouseX: clientX, startMouseY: clientY,
    startX: plate.xMm, startY: plate.yMm,
    isGroup,
    groupIds: isGroup ? members.map(p => p.instanceId) : null,
    groupStartPositions: isGroup
      ? members.map(p => ({ instanceId: p.instanceId, startX: p.xMm, startY: p.yMm }))
      : null,
  }
  window.addEventListener('mousemove', onMove)
  window.addEventListener('mouseup', onUp)
  window.addEventListener('touchmove', onMove, { passive: false })
  window.addEventListener('touchend', onUp)
}

// Whole-instance drag: every plate with the same placementId follows the pointer as
// a rigid block (uniform stud-grid shift, no internal deformation, no overlap).
function onMoveGroup(e) {
  const ids = new Set(_drag.groupIds)
  const nonGroup = placedPlates.value.filter(p => !ids.has(p.instanceId))
  const { clientX, clientY } = getEventCoords(e)
  const STUD = 8
  const snapDx = Math.round((clientX - _drag.startMouseX) / (SCALE * zoom.value) / STUD) * STUD
  const snapDy = Math.round((clientY - _drag.startMouseY) / (SCALE * zoom.value) / STUD) * STUD

  const proposed = _drag.groupStartPositions.map(sp => {
    const p = placedPlates.value.find(x => x.instanceId === sp.instanceId)
    const bp = p ? baseplateMap.value[p.baseplateId] : null
    return { p, bp, newX: sp.startX + snapDx, newY: sp.startY + snapDy }
  })

  const hasOv = proposed.some(pr => {
    if (!pr.p || !pr.bp) return false
    return nonGroup.some(other => {
      const obp = baseplateMap.value[other.baseplateId]
      if (!obp) return false
      return rectsOverlap(
        pr.newX, pr.newY, effectiveW(pr.bp, pr.p.rotation), effectiveH(pr.bp, pr.p.rotation),
        other.xMm, other.yMm, effectiveW(obp, other.rotation), effectiveH(obp, other.rotation),
      )
    })
  })
  if (hasOv) return

  for (const pr of proposed) {
    if (!pr.p) continue
    pr.p.xMm = pr.newX
    pr.p.yMm = pr.newY
  }
}

function onMove(e) {
  if (!_drag) return
  if (_drag.isGroup) return onMoveGroup(e)
  const p = placedPlates.value.find(t => t.instanceId === _drag.instanceId)
  if (!p) return
  const bp = baseplateMap.value[p.baseplateId]
  if (!bp) return

  const ew = effectiveW(bp, p.rotation)
  const eh = effectiveH(bp, p.rotation)

  // Raw new position in mm (canvas mm = xMm + PADDING_MM)
  const { clientX, clientY } = getEventCoords(e)
  const rawCanvasX = (_drag.startX + PADDING_MM) + (clientX - _drag.startMouseX) / (SCALE * zoom.value)
  const rawCanvasY = (_drag.startY + PADDING_MM) + (clientY - _drag.startMouseY) / (SCALE * zoom.value)

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
  draggingGroupId.value = null
  window.removeEventListener('mousemove', onMove)
  window.removeEventListener('mouseup', onUp)
  window.removeEventListener('touchmove', onMove)
  window.removeEventListener('touchend', onUp)
}

// ── Palette drag: MOC / Set → canvas ─────────────────────────────────────────
// The baseplate palette is click-to-add; MOC entries support both (click = auto
// place, drag onto the canvas = place at the drop point). The whole group is
// created at once by `addPlannerEntity`.
const paletteDragId = ref(null)
let _paletteDrag = null
let _paletteMoved = false

function onEntityPointerDown(e, entity) {
  if (e.type !== 'touchstart' && e.button !== 0) return
  const { clientX, clientY } = getEventCoords(e)
  _paletteDrag = { entity, startX: clientX, startY: clientY }
  _paletteMoved = false
  paletteDragId.value = entity.setId
  window.addEventListener('mousemove', onPaletteMove)
  window.addEventListener('mouseup', onPaletteUp)
  window.addEventListener('touchmove', onPaletteMove, { passive: true })
  window.addEventListener('touchend', onPaletteUp)
}

function onPaletteMove(e) {
  if (!_paletteDrag) return
  const { clientX, clientY } = getEventCoords(e)
  if (Math.abs(clientX - _paletteDrag.startX) + Math.abs(clientY - _paletteDrag.startY) > 5) {
    _paletteMoved = true
  }
}

function onPaletteUp(e) {
  if (!_paletteDrag) return
  const entity = _paletteDrag.entity
  const moved = _paletteMoved
  const { clientX, clientY } = getEventCoords(e)
  _paletteDrag = null
  _paletteMoved = false
  paletteDragId.value = null
  window.removeEventListener('mousemove', onPaletteMove)
  window.removeEventListener('mouseup', onPaletteUp)
  window.removeEventListener('touchmove', onPaletteMove)
  window.removeEventListener('touchend', onPaletteUp)

  if (!moved) { addPlannerEntity(entity); return } // plain click → auto place

  const wrap = canvasWrapEl.value
  const canvas = wrap?.querySelector('.canvas')
  if (!wrap || !canvas) return
  const wrect = wrap.getBoundingClientRect()
  if (clientX < wrect.left || clientX > wrect.right || clientY < wrect.top || clientY > wrect.bottom) return
  const crect = canvas.getBoundingClientRect()
  const xMm = (clientX - crect.left) / zoom.value / SCALE - PADDING_MM
  const yMm = (clientY - crect.top) / zoom.value / SCALE - PADDING_MM
  addPlannerEntity(entity, { xMm, yMm })
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

// ── Export PDF ────────────────────────────────────────────────────────────────
function exportPdf() {
  pdfExporting.value = true
  try {
    generateAggregatePdf(
      room.value,
      aggTables.value,
      aggBounds.value,
      templateMap.value,
      placedPlates.value,
      baseplateMap.value,
      baseplates.value,
    )
  } finally {
    pdfExporting.value = false
  }
}

// ── Save / Fix ────────────────────────────────────────────────────────────────
async function save() {
  const result = await saveAggregateBpLayout(roomId, repId, serialisePlates())
  const saved = result?.aggregateBpLayouts?.find(l => l.representativeId === repId)
  if (saved) layoutVersion.value = saved.layoutVersion ?? layoutVersion.value
  savedJson.value = JSON.stringify(serialisePlates())
  saveSuccess.value = true
  setTimeout(() => { saveSuccess.value = false }, 2500)
}

async function fixLayout() {
  // Old edge-snapping placed each plate at k×(studs×8−2), accumulating 2k mm of error.
  // Math.ceil(pos/8)*8 only corrects up to 3 plates; at k=4 the error is 8 mm (a full
  // stud multiple) so ceil leaves the position unchanged and plates overlap.
  //
  // Correct approach: infer k = round(pos / old_slot), then set pos = k × new_slot.
  // This works for any k in realistic layouts (fails only beyond ~60 plates in a row).
  for (const p of placedPlates.value) {
    const bp = baseplateMap.value[p.baseplateId]
    if (!bp) continue
    const studW = (p.rotation === 0 || p.rotation === 180) ? bp.widthStuds : bp.depthStuds
    const studH = (p.rotation === 0 || p.rotation === 180) ? bp.depthStuds : bp.widthStuds
    const oldSlotW = studW * 8 - 2
    const oldSlotH = studH * 8 - 2
    if (oldSlotW > 0) p.xMm = Math.round(p.xMm / oldSlotW) * (studW * 8)
    if (oldSlotH > 0) p.yMm = Math.round(p.yMm / oldSlotH) * (studH * 8)
  }
  await save()
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
        <button class="pdf-btn" :disabled="pdfExporting" @click="exportPdf" title="Export layout as PDF">
          {{ pdfExporting ? 'Exporting…' : 'Export PDF' }}
        </button>
        <button class="save-btn" :class="{ dirty: isDirty }" :disabled="!isDirty" @click="save">Save</button>
      </div>
    </div>

    <div v-if="needsFix" class="fix-banner">
      <span>This layout was saved with an older plate-size formula. Positions may be off by up to 2 mm.</span>
      <button class="fix-btn" @click="fixLayout">Fix Plate Positions</button>
    </div>

    <div v-if="loading" class="loading">Loading…</div>

    <!-- ── Quarantine gate (contract §F) ──────────────────────────────── -->
    <template v-else-if="blocked">
      <div class="blocked-screen">
        <h2>Baseplate planner is blocked</h2>
        <p class="blocked-text">
          {{ quarantinedPlates.length }}
          baseplate{{ quarantinedPlates.length === 1 ? '' : 's' }} couldn't be converted
          automatically and must be reconciled before planning.
        </p>
        <ul class="blocked-list">
          <li v-for="bp in quarantinedPlates" :key="bp.id" class="blocked-item">
            <span class="blocked-name">{{ bp.name || 'Baseplate' }}</span>
            <span class="blocked-dims">{{ bp.widthStuds }}×{{ bp.depthStuds }} stud</span>
            <span class="blocked-reason">{{ bp.quarantineReason || 'No reason provided' }}</span>
          </li>
        </ul>
        <router-link class="blocked-link" to="/baseplates">
          Go to the baseplate library to reconcile →
        </router-link>
      </div>
    </template>

    <template v-else-if="aggTables.length === 0">
      <p class="empty">Aggregate not found. It may have changed in the room planner.</p>
    </template>

    <template v-else>
      <div class="planner-body">

        <!-- ── Side panel ──────────────────────────────────────────────── -->
        <div class="side-panel">
          <div class="side-panel-heading">Baseplates</div>
          <p v-if="baseplates.length === 0" class="side-empty">No baseplates — add some in Table Planner.</p>

          <template v-for="group in baseplatesByCategory" :key="group.category">
            <!-- Category header -->
            <button
              class="cat-header"
              @click="collapsedCategories.has(group.category)
                ? collapsedCategories.delete(group.category)
                : collapsedCategories.add(group.category)"
            >
              <span class="cat-chevron">{{ collapsedCategories.has(group.category) ? '▶' : '▼' }}</span>
              <span class="cat-name">{{ group.category }}</span>
              <span class="cat-count">{{ group.items.length }}</span>
            </button>

            <!-- Category items -->
            <template v-if="!collapsedCategories.has(group.category)">
              <button
                v-for="bp in group.items"
                :key="bp.id"
                class="bp-card"
                @click="addBaseplate(bp)"
                @mouseenter="showTooltip($event, bp)"
                @mouseleave="hideTooltip"
              >
                <div class="bp-thumb">
                  <img v-if="bp.imageCached" :src="getBaseplateImageUrl(bp.id)" class="bp-thumb-img" draggable="false" />
                  <div v-else class="bp-thumb-swatch" :style="{ background: bp.legoColorRgb ? '#' + bp.legoColorRgb : '#9ac' }"></div>
                </div>
                <div class="bp-info">
                  <span class="bp-size">{{ bp.widthStuds }}×{{ bp.depthStuds }}</span>
                  <span class="bp-name">{{ bp.name || 'Baseplate' }}</span>
                </div>
              </button>
            </template>
          </template>

          <!-- ── MOC / Sets (contract §G) ─────────────────────────────── -->
          <button
            class="cat-header sets-header"
            @click="collapsedSets = !collapsedSets"
          >
            <span class="cat-chevron">{{ collapsedSets ? '▶' : '▼' }}</span>
            <span class="cat-name">MOC / Sets</span>
            <span class="cat-count">{{ plannerEntities.length }}</span>
          </button>
          <p v-if="!collapsedSets && plannerEntities.length === 0" class="side-empty">
            No sets with baseplates reserved.
          </p>
          <template v-if="!collapsedSets">
            <button
              v-for="ent in plannerEntities"
              :key="ent.setId"
              class="bp-card set-card"
              :class="{ 'set-card--dragging': paletteDragId === ent.setId }"
              :title="`Drag onto the canvas to place ${ent.totalPlates} plate(s)`"
              @mousedown="onEntityPointerDown($event, ent)"
              @touchstart.prevent="onEntityPointerDown($event, ent)"
            >
              <div class="bp-thumb set-thumb">
                <span class="set-thumb-glyph">{{ ent.isMoc ? 'MOC' : 'SET' }}</span>
              </div>
              <div class="bp-info">
                <span class="bp-size">{{ ent.name }}</span>
                <span class="bp-name">{{ ent.cols }}×{{ ent.rows }} · {{ ent.totalPlates }} plates</span>
                <span class="bp-name">{{ ent.footprintWidthStuds }}×{{ ent.footprintDepthStuds }} stud</span>
              </div>
            </button>
          </template>

          <div class="side-hints">
            <span>R — rotate</span>
            <span>Del — remove</span>
            <span>Ctrl+scroll — zoom</span>
          </div>
        </div>

        <!-- ── Canvas area ─────────────────────────────────────────────── -->
        <div class="canvas-area">
          <!-- Selected plate toolbar -->
          <div class="toolbar">
            <template v-if="selectedId">
              <button class="tool-btn" @click="rotateSelected">↺ Rotate 90°</button>
              <button class="tool-btn danger" @click="removePlate(selectedId)">✕ Remove</button>
            </template>
            <!-- Plain placed-plate count (no derived estimate) -->
            <span v-if="placedPlates.length" class="plate-count">{{ placedPlates.length }} placed</span>
          </div>

          <!-- Ownership warning (non-blocking): more instances than owned sets -->
          <div v-for="w in overUsedSets" :key="'own-' + w.setId" class="layout-warning">
            This layout uses {{ w.used }}× {{ w.name }} but you own {{ w.owned }}.
          </div>

          <div v-if="plannerError" class="planner-error">
            {{ plannerError }}
            <button class="planner-error-close" @click="plannerError = ''">✕</button>
          </div>

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
                left:   tableCanvasX(t) * SCALE + 'px',
                top:    tableCanvasY(t) * SCALE + 'px',
                width:  (t.rotation % 180 === 0 ? (templateMap[t.templateId]?.widthCm ?? 0) : (templateMap[t.templateId]?.depthCm ?? 0)) * 10 * SCALE + 'px',
                height: (t.rotation % 180 === 0 ? (templateMap[t.templateId]?.depthCm  ?? 0) : (templateMap[t.templateId]?.widthCm ?? 0)) * 10 * SCALE + 'px',
              }"
            >
              <span class="silhouette-label">{{ templateMap[t.templateId]?.description ?? '' }}</span>
            </div>

            <!-- MOC / Set instance outlines (drawn under the real plates) -->
            <div
              v-for="g in groupOutlines"
              :key="'grp-' + g.placementId"
              class="plate-group"
              :class="{ 'plate-group--selected': g.placementId === selectedPlacementId }"
              :style="{
                left:   (g.xMm + PADDING_MM) * SCALE - 4 + 'px',
                top:    (g.yMm + PADDING_MM) * SCALE - 4 + 'px',
                width:  g.wMm * SCALE + 8 + 'px',
                height: g.hMm * SCALE + 8 + 'px',
              }"
            >
              <span class="plate-group-label">{{ g.name }}</span>
            </div>

            <!-- Placed baseplates -->
            <div
              v-for="p in placedPlates"
              :key="p.instanceId"
              class="plate"
              :class="{
                'plate--selected': selectedId === p.instanceId,
                'plate--group-selected': isGroupSelected(p),
                'plate--dragging': draggingId === p.instanceId || (!!p.placementId && p.placementId === draggingGroupId),
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
              @touchstart.prevent="startDrag($event, p)"
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
                <template v-if="baseplateMap[p.baseplateId]?.name"><br>{{ baseplateMap[p.baseplateId].name }}</template>
              </span>
              <span v-if="p.sourceSetId" class="plate-set-tag">{{ entityName(p.sourceSetId) }}</span>
            </div>
          </div>
        </div>
          </div><!-- /canvas-wrap -->
        </div><!-- /canvas-area -->
      </div><!-- /planner-body -->
    </template>

    <!-- ── Hover tooltip card ───────────────────────────────────────────── -->
    <div
      v-if="hoveredBp"
      class="bp-tooltip"
      :style="{ top: tooltipTop + 'px', left: tooltipLeft + 'px' }"
    >
      <div class="bp-tooltip-thumb">
        <img
          v-if="hoveredBp.imageCached"
          :src="getBaseplateImageUrl(hoveredBp.id)"
          class="bp-tooltip-img"
          draggable="false"
        />
        <div
          v-else
          class="bp-tooltip-swatch"
          :style="{ background: hoveredBp.legoColorRgb ? '#' + hoveredBp.legoColorRgb : '#9ac' }"
        ></div>
      </div>
      <div class="bp-tooltip-body">
        <div class="bp-tooltip-name">{{ hoveredBp.name || 'Baseplate' }}</div>
        <dl class="bp-tooltip-dl">
          <dt>Size</dt>
          <dd>{{ hoveredBp.widthStuds }}×{{ hoveredBp.depthStuds }} studs<br>{{ bpNaturalW(hoveredBp) }}×{{ bpNaturalH(hoveredBp) }} mm</dd>
          <dt>Type</dt>
          <dd>{{ hoveredBp.type }}</dd>
          <dt>Color</dt>
          <dd>
            <span
              v-if="hoveredBp.legoColorRgb"
              class="bp-tooltip-color-dot"
              :style="{ background: '#' + hoveredBp.legoColorRgb }"
            ></span>
            {{ hoveredBp.legoColorName ?? ('#' + hoveredBp.legoColorId) }}
          </dd>
          <dt>Part #</dt>
          <dd>{{ hoveredBp.partNum }}</dd>
        </dl>
      </div>
    </div>
  </div>
</template>

<style scoped>
.planner-page {
  display: flex;
  flex-direction: column;
  flex: 1;
  padding: 0.75rem 1.25rem 0;
  box-sizing: border-box;
  outline: none;
  overflow: hidden;
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

.fix-banner {
  display: flex; align-items: center; gap: 1rem; flex-wrap: wrap;
  background: #fff3cd; border: 1px solid #ffc107; border-radius: 6px;
  padding: 0.5rem 1rem; margin: 0 0 0.5rem;
  font-size: 0.85rem; color: #664d03;
}
.fix-banner span { flex: 1; }
.fix-btn {
  background: #ffc107; color: #000; border: none; border-radius: 4px;
  padding: 0.3rem 0.8rem; cursor: pointer; font-weight: 600; white-space: nowrap;
}
.fix-btn:hover { background: #e0a800; }

.pdf-btn {
  background: #f0f0f0; border: 1px solid #ccc; border-radius: 4px;
  padding: 0.35rem 0.9rem; font-size: 0.85rem; cursor: pointer; color: #444;
  transition: background 0.15s;
}
.pdf-btn:hover:not(:disabled) { background: #e0e0e0; }
.pdf-btn:disabled { color: #aaa; cursor: default; }

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

/* ── Body layout ─────────────────────────────────────────────────────────── */
.planner-body {
  display: flex;
  flex: 1;
  gap: 0;
  overflow: hidden;
}

/* ── Side panel ──────────────────────────────────────────────────────────── */
.side-panel {
  width: 172px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  overflow-y: auto;
  border-right: 1px solid #d0d5db;
  background: #f4f6f8;
  padding: 0.5rem 0.5rem 0.75rem;
  gap: 0.3rem;
}

.side-panel-heading {
  font-size: 0.75rem;
  font-weight: 700;
  color: #667;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  padding: 0.1rem 0.2rem 0.3rem;
  border-bottom: 1px solid #dde;
  margin-bottom: 0.1rem;
}

.side-empty { font-size: 0.78rem; color: #999; padding: 0.3rem 0.2rem; }

/* ── Category header ─────────────────────────────────────────────────────── */
.cat-header {
  display: flex;
  align-items: center;
  gap: 0.3rem;
  width: 100%;
  background: none;
  border: none;
  border-radius: 3px;
  padding: 0.25rem 0.3rem;
  cursor: pointer;
  text-align: left;
  color: #445;
}
.cat-header:hover { background: #eaecf0; }

.cat-chevron { font-size: 0.6rem; color: #889; flex-shrink: 0; }
.cat-name { font-size: 0.72rem; font-weight: 700; text-transform: uppercase; letter-spacing: 0.04em; flex: 1; }
.cat-count { font-size: 0.68rem; color: #99a; background: #e0e4ea; border-radius: 8px; padding: 0 0.35rem; }

.bp-card {
  display: flex;
  align-items: center;
  gap: 0.45rem;
  width: 100%;
  background: #fff;
  border: 1px solid #dde;
  border-radius: 5px;
  padding: 0.35rem 0.4rem;
  cursor: pointer;
  text-align: left;
  transition: border-color 0.12s, box-shadow 0.12s;
}
.bp-card:hover {
  border-color: #3a6ea5;
  box-shadow: 0 0 0 2px rgba(58,110,165,0.15);
}

.bp-thumb {
  width: 44px;
  height: 44px;
  flex-shrink: 0;
  border-radius: 3px;
  overflow: hidden;
  background: #e8ecf0;
  display: flex;
  align-items: center;
  justify-content: center;
}
.bp-thumb-img {
  width: 100%;
  height: 100%;
  object-fit: contain;
  display: block;
}
.bp-thumb-swatch { width: 100%; height: 100%; }

.bp-info {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  min-width: 0;
}
.bp-size {
  font-size: 0.78rem;
  font-weight: 700;
  color: #334;
  white-space: nowrap;
}
.bp-name {
  font-size: 0.72rem;
  color: #667;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.side-hints {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  margin-top: auto;
  padding-top: 0.6rem;
  border-top: 1px solid #dde;
}
.side-hints span { font-size: 0.68rem; color: #aab; }

/* ── Canvas area ─────────────────────────────────────────────────────────── */
.canvas-area {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  padding-left: 0.75rem;
}

/* ── Toolbar ─────────────────────────────────────────────────────────────── */
.toolbar {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  min-height: 32px;
  margin-bottom: 0.35rem;
}

.tool-btn {
  background: #f0f0f0; border: 1px solid #ccc; border-radius: 4px;
  padding: 0.25rem 0.65rem; font-size: 0.82rem; cursor: pointer;
}
.tool-btn:hover { background: #e4e4e4; }
.tool-btn.danger { color: #c0392b; }
.tool-btn.danger:hover { background: #fde8e8; border-color: #c0392b; }

.plate-count { margin-left: auto; font-size: 0.78rem; color: #778; font-weight: 600; }

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
  touch-action: none;
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

/* ── Hover tooltip card ───────────────────────────────────────────────────── */
.bp-tooltip {
  position: fixed;
  z-index: 200;
  width: 220px;
  background: #fff;
  border: 1px solid #d0d5db;
  border-radius: 8px;
  box-shadow: 0 8px 24px rgba(0,0,0,0.18);
  overflow: hidden;
  pointer-events: none;
}

.bp-tooltip-thumb {
  width: 100%;
  height: 130px;
  background: #e8ecf0;
  display: flex;
  align-items: center;
  justify-content: center;
  overflow: hidden;
}
.bp-tooltip-img {
  width: 100%;
  height: 100%;
  object-fit: contain;
  display: block;
}
.bp-tooltip-swatch {
  width: 100%;
  height: 100%;
}

.bp-tooltip-body {
  padding: 0.6rem 0.7rem 0.65rem;
}

.bp-tooltip-name {
  font-size: 0.82rem;
  font-weight: 700;
  color: #223;
  margin-bottom: 0.45rem;
  line-height: 1.3;
}

.bp-tooltip-dl {
  display: grid;
  grid-template-columns: auto 1fr;
  gap: 0.18rem 0.5rem;
  margin: 0;
  font-size: 0.75rem;
}
.bp-tooltip-dl dt {
  color: #889;
  font-weight: 600;
  white-space: nowrap;
}
.bp-tooltip-dl dd {
  margin: 0;
  color: #334;
  line-height: 1.35;
}

.bp-tooltip-color-dot {
  display: inline-block;
  width: 10px;
  height: 10px;
  border-radius: 50%;
  border: 1px solid rgba(0,0,0,0.2);
  vertical-align: middle;
  margin-right: 3px;
}

/* ── Warning banner (non-blocking) ───────────────────────────────────────── */
.layout-warning {
  background: #fff3cd;
  border: 1px solid #ffc107;
  border-radius: 6px;
  padding: 0.4rem 0.7rem;
  margin-bottom: 0.5rem;
  font-size: 0.82rem;
  color: #664d03;
}

/* ── Planner error banner ────────────────────────────────────────────────── */
.planner-error {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  background: #fde8e8;
  border: 1px solid #e0a0a0;
  border-radius: 6px;
  padding: 0.4rem 0.7rem;
  margin-bottom: 0.5rem;
  font-size: 0.82rem;
  color: #8a2020;
}
.planner-error-close {
  margin-left: auto;
  background: none;
  border: none;
  color: #8a2020;
  cursor: pointer;
  font-size: 0.85rem;
}

/* ── Quarantine gate (contract §F) ───────────────────────────────────────── */
.blocked-screen {
  max-width: 680px;
  margin: 1.5rem auto;
  background: #fff8f0;
  border: 1px solid #e8b070;
  border-left: 5px solid #d97706;
  border-radius: 8px;
  padding: 1.25rem 1.5rem;
}
.blocked-screen h2 { margin: 0 0 0.5rem; color: #92400e; font-size: 1.1rem; }
.blocked-text { margin: 0 0 0.85rem; color: #6b4a22; font-size: 0.92rem; line-height: 1.45; }
.blocked-list { list-style: none; margin: 0 0 1rem; padding: 0; display: flex; flex-direction: column; gap: 0.35rem; }
.blocked-item {
  display: flex;
  align-items: baseline;
  gap: 0.6rem;
  flex-wrap: wrap;
  background: #fff;
  border: 1px solid #eee0cc;
  border-radius: 5px;
  padding: 0.4rem 0.6rem;
  font-size: 0.85rem;
}
.blocked-name { font-weight: 600; color: #333; }
.blocked-dims { color: #777; white-space: nowrap; }
.blocked-reason { color: #a05020; font-style: italic; margin-left: auto; }
.blocked-link {
  display: inline-block;
  background: #d97706;
  color: #fff;
  text-decoration: none;
  border-radius: 4px;
  padding: 0.4rem 0.9rem;
  font-size: 0.85rem;
  font-weight: 600;
}
.blocked-link:hover { background: #b45309; }

/* ── MOC / Sets sidebar entries ──────────────────────────────────────────── */
.sets-header { margin-top: 0.35rem; border-top: 1px solid #dde; padding-top: 0.35rem; }
.set-card { cursor: grab; }
.set-card--dragging { opacity: 0.5; }
.set-thumb {
  background: #e3ecf6;
  border: 1px dashed #9bb8d4;
}
.set-thumb-glyph {
  font-size: 0.62rem;
  font-weight: 700;
  color: #3a6ea5;
  letter-spacing: 0.05em;
}

/* ── MOC / Set group outline on canvas ───────────────────────────────────── */
.plate-group {
  position: absolute;
  border: 2px dashed rgba(58, 110, 165, 0.9);
  border-radius: 5px;
  pointer-events: none;
  z-index: 0;
  box-sizing: border-box;
}
.plate-group--selected {
  border-style: solid;
  border-color: #1a90d0;
  background: rgba(26, 144, 208, 0.06);
}
.plate-group-label {
  position: absolute;
  top: -15px;
  left: -2px;
  background: rgba(58, 110, 165, 0.95);
  color: #fff;
  font-size: 0.6rem;
  font-weight: 700;
  padding: 0.05rem 0.4rem;
  border-radius: 3px 3px 0 0;
  white-space: nowrap;
  max-width: 160px;
  overflow: hidden;
  text-overflow: ellipsis;
}

.plate--group-selected {
  border-color: #1a90d0;
  box-shadow: 0 0 0 2px rgba(26, 144, 208, 0.4), 0 2px 6px rgba(0, 0, 0, 0.18);
  z-index: 4;
}

.plate-set-tag {
  position: absolute;
  bottom: 2px;
  left: 50%;
  transform: translateX(-50%);
  background: rgba(58, 110, 165, 0.9);
  color: #fff;
  font-size: 0.5rem;
  font-weight: 700;
  padding: 0 0.25rem;
  border-radius: 2px;
  white-space: nowrap;
  max-width: 90%;
  overflow: hidden;
  text-overflow: ellipsis;
  pointer-events: none;
  z-index: 2;
}
</style>
