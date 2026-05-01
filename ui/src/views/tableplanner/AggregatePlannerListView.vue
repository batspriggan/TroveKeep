<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { getAllRooms, getAllTemplates } from '../../api/tableplanner.js'

const router = useRouter()
const rooms = ref([])
const templates = ref([])
const loading = ref(true)

onMounted(async () => {
  const [r, t] = await Promise.all([getAllRooms(), getAllTemplates()])
  rooms.value = r
  templates.value = t
  loading.value = false
})

const templateMap = computed(() => {
  const m = {}
  for (const t of templates.value) m[t.id] = t
  return m
})

// ── Same BFS aggregate detection as RoomPlannerView ────────────────────────
function rangeOverlaps(a1, a2, b1, b2) {
  return Math.min(a2, b2) - Math.max(a1, b1) > 0
}

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

function aggRepId(group) { return [...group].sort()[0] }

function aggBounds(group, layout, tMap) {
  let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity
  for (const id of group) {
    const t = layout.find(p => p.instanceId === id)
    const tpl = tMap[t?.templateId]
    if (!t || !tpl) continue
    if (t.xCm < minX) minX = t.xCm
    if (t.yCm < minY) minY = t.yCm
    if (t.xCm + tpl.widthCm > maxX) maxX = t.xCm + tpl.widthCm
    if (t.yCm + tpl.depthCm > maxY) maxY = t.yCm + tpl.depthCm
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
</script>

<template>
  <div class="agg-list-page">
    <h1>Table Planner</h1>
    <div class="tab-bar">
      <button class="tab" @click="router.push('/table-planner')">Rooms</button>
      <button class="tab active">Baseplate Planner</button>
    </div>

    <div v-if="loading" class="loading">Loading…</div>

    <template v-else>
      <p v-if="allAggregates.length === 0" class="empty">
        No table aggregates found. Open a room in Table Planner and place adjacent tables to create aggregates.
      </p>

      <table v-else class="agg-table">
        <thead>
          <tr>
            <th>Room</th>
            <th>Tables</th>
            <th>Dimensions</th>
            <th>Baseplates placed</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="agg in allAggregates" :key="agg.roomId + agg.repId">
            <td class="col-room">{{ agg.roomName }}</td>
            <td class="col-tables">{{ agg.tableCount }}</td>
            <td class="col-dims">{{ agg.widthCm }} × {{ agg.depthCm }} cm</td>
            <td class="col-bp">
              <span v-if="agg.placedCount > 0" class="bp-count">{{ agg.placedCount }}</span>
              <span v-else class="bp-none">—</span>
            </td>
            <td class="col-action">
              <button class="primary small" @click="router.push(`/table-planner/baseplates/${agg.roomId}/${agg.repId}`)">
                Open →
              </button>
            </td>
          </tr>
        </tbody>
      </table>
    </template>
  </div>
</template>

<style scoped>
.agg-list-page {
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

.loading, .empty {
  color: #888;
  padding: 1rem 0;
}

.agg-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.9rem;
}

.agg-table th, .agg-table td {
  text-align: left;
  padding: 0.45rem 0.6rem;
  border-bottom: 1px solid #eee;
}

.agg-table th {
  font-weight: 600;
  background: #f5f5f5;
}

.agg-table tr:hover td { background: #fafbfc; }

.col-room { font-weight: 500; }
.col-dims { color: #555; }
.col-tables { text-align: center; }

.bp-count { font-weight: 600; color: #2a5a2a; }
.bp-none { color: #aaa; }

button.primary {
  background: #3a6ea5;
  color: #fff;
  border: none;
  border-radius: 4px;
  padding: 0.35rem 0.75rem;
  cursor: pointer;
}
button.primary:hover { background: #2e5a8a; }

button.small {
  font-size: 0.8rem;
  padding: 0.25rem 0.55rem;
}
</style>
