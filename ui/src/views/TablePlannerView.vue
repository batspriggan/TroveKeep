<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import {
  getAllRooms, createRoom, updateRoom, deleteRoom,
  exportRoom, importRoom,
} from '../api/tableplanner.js'

const router = useRouter()

// ── Rooms ────────────────────────────────────────────────────────────────────
const rooms = ref([])
const roomForm = ref({ name: '', widthCm: 1000, depthCm: 800 })
const roomError = ref('')
const importError = ref('')
const importSuccess = ref('')
const importFileRef = ref(null)

async function loadRooms() {
  rooms.value = await getAllRooms()
}

async function submitRoom() {
  roomError.value = ''
  if (!roomForm.value.name.trim()) { roomError.value = 'Name is required.'; return }
  await createRoom({ name: roomForm.value.name.trim(), widthCm: Number(roomForm.value.widthCm), depthCm: Number(roomForm.value.depthCm) })
  roomForm.value = { name: '', widthCm: 1000, depthCm: 800 }
  await loadRooms()
}

async function removeRoom(id) {
  if (!confirm('Delete this room and its layout?')) return
  await deleteRoom(id)
  await loadRooms()
}

function openRoom(id) {
  router.push(`/table-planner/rooms/${id}`)
}

function triggerImport() {
  importFileRef.value.click()
}

async function handleImportFile(e) {
  const file = e.target.files[0]
  if (!file) return
  e.target.value = ''
  importError.value = ''
  importSuccess.value = ''
  try {
    await importRoom(file)
    await loadRooms()
    importSuccess.value = 'Room imported successfully.'
    setTimeout(() => { importSuccess.value = '' }, 3000)
  } catch (err) {
    importError.value = err.message
  }
}

onMounted(() => loadRooms())
</script>

<template>
  <div class="hub-page">
    <h1>Table Planner</h1>

    <!-- ── Rooms ── -->
    <section class="section">
      <h2>Rooms</h2>

      <form class="inline-form" @submit.prevent="submitRoom">
        <input v-model="roomForm.name" placeholder="Room name" />
        <label>W (cm) <input v-model.number="roomForm.widthCm" type="number" min="100" max="10000" style="width:80px" /></label>
        <label>D (cm) <input v-model.number="roomForm.depthCm" type="number" min="100" max="10000" style="width:80px" /></label>
        <button class="primary" type="submit">+ Add Room</button>
        <button type="button" @click="triggerImport">Import Room</button>
        <input type="file" ref="importFileRef" accept=".zip" style="display:none" @change="handleImportFile" />
        <span v-if="roomError" class="form-error">{{ roomError }}</span>
      </form>
      <p v-if="importError" class="form-error">{{ importError }}</p>
      <p v-if="importSuccess" class="form-success">{{ importSuccess }}</p>

      <p v-if="rooms.length === 0" class="empty-hint">No rooms yet.</p>

      <table v-else class="data-table">
        <thead>
          <tr>
            <th>Name</th>
            <th>Width (cm)</th>
            <th>Depth (cm)</th>
            <th>Tables in layout</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="r in rooms" :key="r.id">
            <td>{{ r.name }}</td>
            <td>{{ r.widthCm }}</td>
            <td>{{ r.depthCm }}</td>
            <td>{{ r.layout.length }}</td>
            <td class="actions">
              <button class="primary small" @click="openRoom(r.id)">Open</button>
              <button class="small" @click="exportRoom(r.id)">Export</button>
              <button class="danger small" @click="removeRoom(r.id)">Delete</button>
            </td>
          </tr>
        </tbody>
      </table>
    </section>

  </div>
</template>

<style scoped>
.hub-page {
  padding: 1.25rem 1.5rem;
  max-width: 900px;
}

h1 { margin: 0 0 1.25rem; }

.section {
  margin-bottom: 2.5rem;
}

.section h2 {
  margin: 0 0 0.75rem;
  font-size: 1.1rem;
  border-bottom: 1px solid #ddd;
  padding-bottom: 0.35rem;
}

.inline-form {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.5rem;
  margin-bottom: 1rem;
}

.inline-form input[type="text"],
.inline-form input:not([type="number"]):not([type="color"]) {
  padding: 0.35rem 0.5rem;
  border: 1px solid #ccc;
  border-radius: 4px;
  font-size: 0.9rem;
}

.inline-form input[type="number"] {
  padding: 0.35rem 0.4rem;
  border: 1px solid #ccc;
  border-radius: 4px;
  font-size: 0.9rem;
}

.inline-form label {
  display: flex;
  align-items: center;
  gap: 0.3rem;
  font-size: 0.85rem;
  color: #555;
}

.form-error {
  color: #c00;
  font-size: 0.85rem;
}

.form-success {
  color: #2a7a2a;
  font-size: 0.85rem;
}

.empty-hint {
  color: #888;
  font-size: 0.9rem;
}

.data-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.9rem;
}

.data-table th,
.data-table td {
  text-align: left;
  padding: 0.45rem 0.6rem;
  border-bottom: 1px solid #eee;
}

.data-table th {
  font-weight: 600;
  background: #f5f5f5;
}

.actions {
  display: flex;
  gap: 0.4rem;
}

button.small {
  font-size: 0.8rem;
  padding: 0.25rem 0.55rem;
}

button.primary {
  background: #3a6ea5;
  color: #fff;
  border: none;
  border-radius: 4px;
  padding: 0.35rem 0.75rem;
  cursor: pointer;
}

button.primary:hover { background: #2e5a8a; }

button.danger {
  background: #c0392b;
  color: #fff;
  border: none;
  border-radius: 4px;
  padding: 0.35rem 0.75rem;
  cursor: pointer;
}

button.danger:hover { background: #a93226; }

button:not(.primary):not(.danger) {
  background: #f0f0f0;
  border: 1px solid #ccc;
  border-radius: 4px;
  padding: 0.35rem 0.75rem;
  cursor: pointer;
}

button:not(.primary):not(.danger):hover { background: #e0e0e0; }
</style>
