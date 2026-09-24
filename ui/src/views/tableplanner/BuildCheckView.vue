<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { getAllRooms, getAllTemplates } from '../../api/tableplanner.js'
import { checkFeasibility } from '../../api/baseplates.js'

const router = useRouter()
const rooms = ref([])
const templates = ref([])
const loading = ref(true)

// Volatile selection only — never persisted.
const selected = ref(new Set())
const checking = ref(false)
const error = ref('')
const result = ref(null)

onMounted(async () => {
  try {
    const [r, t] = await Promise.all([getAllRooms(), getAllTemplates()])
    rooms.value = r
    templates.value = t
  } catch (e) {
    error.value = e.message ?? 'Failed to load rooms.'
  } finally {
    loading.value = false
  }
})

const templateMap = computed(() => {
  const m = {}
  for (const t of templates.value) m[t.id] = t
  return m
})

// ── Same BFS aggregate detection as AggregatePlannerListView ───────────────
function rangeOverlaps(a1, a2, b1, b2) {
  return Math.min(a2, b2) - Math.max(a1, b1) > 0
}

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

function aggRepId(group) { return [...group].sort()[0] }

function aggBounds(group, layout, tMap) {
  let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity
  for (const id of group) {
    const t = layout.find(p => p.instanceId === id)
    const tpl = tMap[t?.templateId]
    if (!t || !tpl) continue
    const w = t.rotation % 180 === 0 ? tpl.widthCm : tpl.depthCm
    const h = t.rotation % 180 === 0 ? tpl.depthCm : tpl.widthCm
    if (t.xCm < minX) minX = t.xCm
    if (t.yCm < minY) minY = t.yCm
    if (t.xCm + w > maxX) maxX = t.xCm + w
    if (t.yCm + h > maxY) maxY = t.yCm + h
  }
  return { w: maxX - minX, h: maxY - minY }
}

// All aggregates across all rooms
const allAggregates = computed(() => {
  const tMap = templateMap.value
  const result = []
  for (const room of rooms.value) {
    const aggs = computeAggregates(room.layout, tMap)
    for (const group of aggs) {
      const repId = aggRepId(group)
      const bounds = aggBounds(group, room.layout, tMap)
      const bpLayout = room.aggregateBpLayouts?.find(l => l.representativeId === repId)
      result.push({
        key: `${room.id}::${repId}`,
        roomId: room.id,
        roomName: room.name,
        repId,
        tableCount: group.length,
        widthCm: bounds.w,
        depthCm: bounds.h,
        placedCount: bpLayout?.placedBaseplates?.length ?? 0,
      })
    }
  }
  return result
})

const selectedCount = computed(() => selected.value.size)
const allSelected = computed(() =>
  allAggregates.value.length > 0 && selected.value.size === allAggregates.value.length
)

function toggle(agg) {
  const next = new Set(selected.value)
  if (next.has(agg.key)) next.delete(agg.key)
  else next.add(agg.key)
  selected.value = next
}

function toggleAll() {
  if (allSelected.value) {
    selected.value = new Set()
  } else {
    selected.value = new Set(allAggregates.value.map(a => a.key))
  }
}

function clearSelection() {
  selected.value = new Set()
}

function isShort(line) {
  return line.status === 'short' || line.deficit > 0
}

async function runCheck() {
  error.value = ''
  const aggregates = allAggregates.value
    .filter(a => selected.value.has(a.key))
    .map(a => ({ roomId: a.roomId, representativeId: a.repId }))
  if (aggregates.length === 0) return
  checking.value = true
  try {
    result.value = await checkFeasibility(aggregates)
  } catch (e) {
    error.value = e.message ?? 'Check failed.'
    result.value = null
  } finally {
    checking.value = false
  }
}
</script>

<template>
  <div class="build-check-page">
    <h1>Table Planner</h1>
    <div class="tab-bar">
      <button class="tab" @click="router.push('/table-planner')">Rooms</button>
      <button class="tab" @click="router.push('/table-planner/baseplates')">Baseplate Planner</button>
      <button class="tab active">Build Check</button>
    </div>

    <p class="page-hint">
      Select one or more configurations to check whether their combined baseplate need fits the stock.
      Selection is temporary — nothing is reserved or saved.
    </p>

    <div v-if="loading" class="loading">Loading…</div>

    <template v-else>
      <p v-if="allAggregates.length === 0" class="empty">
        No table aggregates found. Open a room in Table Planner and place adjacent tables to create aggregates.
      </p>

      <template v-else>
        <table class="agg-table">
          <thead>
            <tr>
              <th class="col-check">
                <input type="checkbox" :checked="allSelected" @change="toggleAll" />
              </th>
              <th>Room</th>
              <th>Tables</th>
              <th>Dimensions</th>
              <th>Baseplates placed</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="agg in allAggregates"
              :key="agg.key"
              :class="{ 'row-selected': selected.has(agg.key) }"
              @click="toggle(agg)"
            >
              <td class="col-check" @click.stop>
                <input type="checkbox" :checked="selected.has(agg.key)" @change="toggle(agg)" />
              </td>
              <td class="col-room">{{ agg.roomName }}</td>
              <td class="col-tables">{{ agg.tableCount }}</td>
              <td class="col-dims">{{ agg.widthCm.toFixed(2) }} × {{ agg.depthCm.toFixed(2) }} cm</td>
              <td class="col-bp">
                <span v-if="agg.placedCount > 0" class="bp-count">{{ agg.placedCount }}</span>
                <span v-else class="bp-none">—</span>
              </td>
            </tr>
          </tbody>
        </table>

        <div class="action-bar">
          <button class="primary" :disabled="selectedCount === 0 || checking" @click="runCheck">
            {{ checking ? 'Checking…' : `Check ${selectedCount} configuration${selectedCount === 1 ? '' : 's'}` }}
          </button>
          <button v-if="selectedCount > 0" class="ghost" @click="clearSelection">Clear selection</button>
        </div>

        <p v-if="error" class="form-error">{{ error }}</p>

        <!-- Empty state -->
        <p v-if="!result && !error && selectedCount === 0" class="empty">
          Select at least one configuration to run the build check.
        </p>

        <!-- Result -->
        <section v-if="result" class="result">
          <h2>Result</h2>

          <div v-if="result.hasShortage" class="summary summary-short">
            <strong>Shortage:</strong> you need to buy {{ result.totalDeficit }}
            baseplate{{ result.totalDeficit === 1 ? '' : 's' }} for the selected configurations.
          </div>
          <div v-else class="summary summary-ok">
            <strong>OK:</strong> all selected configurations fit in the current stock.
          </div>

          <table v-if="result.lines.length > 0" class="result-table">
            <thead>
              <tr>
                <th>Baseplate</th>
                <th class="num">Need</th>
                <th class="num">Stock</th>
                <th class="num">Reserved</th>
                <th class="num">Available</th>
                <th class="num">Deficit</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="line in result.lines"
                :key="line.baseplateId"
                :class="{ 'row-short': isShort(line) }"
              >
                <td class="col-name">
                  <span class="line-name">{{ line.name || 'Baseplate' }}</span>
                  <span class="line-type">{{ line.type }}</span>
                </td>
                <td class="num">{{ line.need }}</td>
                <td class="num">{{ line.quantity }}</td>
                <td class="num">{{ line.reserved }}</td>
                <td class="num">{{ line.available }}</td>
                <td class="num col-deficit">{{ line.deficit }}</td>
              </tr>
            </tbody>
          </table>
          <p v-else class="empty">No baseplate need for the selected configurations (no plated layout).</p>
        </section>
      </template>
    </template>
  </div>
</template>

<style scoped>
.build-check-page {
  max-width: 900px;
  padding: 1.25rem 1.5rem;
}

h1 { margin: 0 0 1rem; }

.tab-bar {
  display: flex;
  gap: 0;
  border-bottom: 2px solid #ddd;
  margin-bottom: 1.5rem;
}

.tab {
  background: none;
  border: none;
  border-bottom: 2px solid transparent;
  margin-bottom: -2px;
  padding: 0.45rem 1.1rem;
  font-size: 0.9rem;
  cursor: pointer;
  color: #555;
  border-radius: 0;
}
.tab:hover { color: #222; }
.tab.active {
  color: #3a6ea5;
  border-bottom-color: #3a6ea5;
  font-weight: 600;
}

.page-hint {
  color: #667;
  font-size: 0.85rem;
  margin: 0 0 1rem;
}

.loading, .empty {
  color: #888;
  padding: 1rem 0;
}

.agg-table,
.result-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.9rem;
}

.agg-table th, .agg-table td,
.result-table th, .result-table td {
  text-align: left;
  padding: 0.45rem 0.6rem;
  border-bottom: 1px solid #eee;
}

.agg-table th,
.result-table th {
  font-weight: 600;
  background: #f5f5f5;
}

.agg-table tbody tr { cursor: pointer; }
.agg-table tr:hover td { background: #fafbfc; }
.agg-table tr.row-selected td { background: #eef4fb; }

.col-check { width: 34px; text-align: center; }
.col-room { font-weight: 500; }
.col-dims { color: #555; }
.col-tables { text-align: center; }

.bp-count { font-weight: 600; color: #2a5a2a; }
.bp-none { color: #aaa; }

.action-bar {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  margin: 1rem 0;
}

button.primary {
  background: #3a6ea5;
  color: #fff;
  border: none;
  border-radius: 4px;
  padding: 0.45rem 1rem;
  cursor: pointer;
  font-size: 0.9rem;
}
button.primary:hover:not(:disabled) { background: #2e5a8a; }
button.primary:disabled { background: #a9bdd4; cursor: not-allowed; }

button.ghost {
  background: none;
  border: 1px solid #ccc;
  border-radius: 4px;
  padding: 0.4rem 0.8rem;
  cursor: pointer;
  font-size: 0.85rem;
  color: #555;
}
button.ghost:hover { background: #f0f0f0; }

.form-error {
  color: #c00;
  font-size: 0.85rem;
  margin: 0.5rem 0;
}

.result { margin-top: 1.5rem; }

.result h2 {
  margin: 0 0 0.75rem;
  font-size: 1.1rem;
  border-bottom: 1px solid #ddd;
  padding-bottom: 0.35rem;
}

.summary {
  border-radius: 6px;
  padding: 0.6rem 0.9rem;
  font-size: 0.9rem;
  margin-bottom: 0.9rem;
}
.summary-short {
  background: #fdecea;
  border: 1px solid #e6a39a;
  color: #8a2117;
}
.summary-ok {
  background: #eaf6ea;
  border: 1px solid #a9d5a9;
  color: #256b25;
}

.result-table td.num,
.result-table th.num { text-align: right; }

.result-table tr.row-short td {
  background: #fdecea;
  color: #8a2117;
  font-weight: 500;
}

.col-name { display: flex; flex-direction: column; gap: 0.1rem; }
.line-name { font-weight: 600; color: #334; }
.line-type { font-size: 0.75rem; color: #99a; }

.col-deficit { font-weight: 700; }
</style>
