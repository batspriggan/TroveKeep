<script setup>
import { ref, watch, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import {
  getAllRooms, createRoom, updateRoom, deleteRoom,
  getAllTemplates, createTemplate, updateTemplate, deleteTemplate,
  exportRoom, importRoom,
  getAllBaseplates, createBaseplate, deleteBaseplate,
} from '../api/tableplanner.js'
import { searchArchivePartsBaseplates } from '../api/archives.js'

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

// ── Templates ─────────────────────────────────────────────────────────────────
const templates = ref([])
const tplForm = ref({ description: '', widthCm: 200, depthCm: 80, color: '#8b6340' })
const tplError = ref('')
const editingId = ref(null)
const editRow = ref({ description: '', widthCm: 200, depthCm: 80, color: '#8b6340' })

async function loadTemplates() {
  templates.value = await getAllTemplates()
}

async function submitTemplate() {
  tplError.value = ''
  if (!tplForm.value.description.trim()) { tplError.value = 'Description is required.'; return }
  await createTemplate({
    description: tplForm.value.description.trim(),
    widthCm: Number(tplForm.value.widthCm),
    depthCm: Number(tplForm.value.depthCm),
    color: tplForm.value.color,
  })
  tplForm.value = { description: '', widthCm: 200, depthCm: 80, color: '#8b6340' }
  await loadTemplates()
}

function startEdit(t) {
  editingId.value = t.id
  editRow.value = { description: t.description, widthCm: t.widthCm, depthCm: t.depthCm, color: t.color }
}

function cancelEdit() {
  editingId.value = null
}

async function saveEdit(id) {
  await updateTemplate(id, {
    description: editRow.value.description,
    widthCm: Number(editRow.value.widthCm),
    depthCm: Number(editRow.value.depthCm),
    color: editRow.value.color,
  })
  editingId.value = null
  await loadTemplates()
}

async function removeTemplate(id) {
  if (!confirm('Delete this template?')) return
  await deleteTemplate(id)
  await loadTemplates()
}

// ── Baseplates ────────────────────────────────────────────────────────────────
const baseplates = ref([])
const newBpQuery = ref('')
const newBpResults = ref([])
const newBpSelected = ref(null)
const newBpWidth = ref(null)
const newBpDepth = ref(null)

async function loadBaseplates() {
  baseplates.value = await getAllBaseplates()
}

watch(newBpQuery, async (q) => {
  if (q.length >= 2) newBpResults.value = await searchArchivePartsBaseplates(q, 10)
  else newBpResults.value = []
})

function selectBpResult(result) {
  newBpSelected.value = { partNum: result.partNum, name: result.name }
  newBpWidth.value = result.guessedStudX > 0 ? result.guessedStudX : null
  newBpDepth.value = result.guessedStudY > 0 ? result.guessedStudY : null
  newBpResults.value = []
  newBpQuery.value = ''
}

async function addBaseplate() {
  if (!newBpSelected.value || newBpWidth.value < 1 || newBpDepth.value < 1) return
  const bp = await createBaseplate({
    partNum: newBpSelected.value.partNum,
    name: newBpSelected.value.name,
    widthStuds: newBpWidth.value,
    depthStuds: newBpDepth.value,
  })
  baseplates.value.push(bp)
  newBpSelected.value = null
  newBpWidth.value = null
  newBpDepth.value = null
}

async function removeBaseplate(id) {
  if (!confirm('Delete this baseplate?')) return
  await deleteBaseplate(id)
  baseplates.value = baseplates.value.filter(b => b.id !== id)
}

onMounted(() => Promise.all([loadRooms(), loadTemplates(), loadBaseplates()]))
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

    <!-- ── Baseplates ── -->
    <section class="section">
      <h2>Baseplates</h2>

      <p v-if="baseplates.length === 0" class="empty-hint">No baseplates yet. Add one below to enable the plate calculator in rooms.</p>

      <table v-else class="data-table">
        <thead>
          <tr>
            <th>Part #</th>
            <th>Name</th>
            <th>Size (studs)</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="b in baseplates" :key="b.id">
            <td>{{ b.partNum }}</td>
            <td>{{ b.name }}</td>
            <td>{{ b.widthStuds }}×{{ b.depthStuds }}</td>
            <td class="actions">
              <button class="danger small" @click="removeBaseplate(b.id)">Delete</button>
            </td>
          </tr>
        </tbody>
      </table>

      <form class="bp-add-form" @submit.prevent="addBaseplate">
        <div class="bp-search-wrap">
          <input
            v-model="newBpQuery"
            placeholder="Search part..."
            class="bp-search-input"
          />
          <ul v-if="newBpResults.length > 0" class="bp-dropdown">
            <li
              v-for="r in newBpResults"
              :key="r.partNum"
              class="bp-dropdown-item"
              @click="selectBpResult(r)"
            >{{ r.partNum }} — {{ r.name }}</li>
          </ul>
        </div>
        <span v-if="newBpSelected" class="bp-selected-badge">{{ newBpSelected.partNum }} — {{ newBpSelected.name }}</span>
        <label>W <input v-model.number="newBpWidth" type="number" min="1" max="256" style="width:56px" required /> studs</label>
        <label>D <input v-model.number="newBpDepth" type="number" min="1" max="256" style="width:56px" required /> studs</label>
        <button class="primary" type="submit" :disabled="!newBpSelected">Add Baseplate</button>
      </form>
    </section>

    <!-- ── Templates ── -->
    <section class="section">
      <h2>Table Templates</h2>

      <form class="inline-form" @submit.prevent="submitTemplate">
        <input v-model="tplForm.description" placeholder="Description" />
        <label>W (cm) <input v-model.number="tplForm.widthCm" type="number" min="10" max="2000" style="width:70px" /></label>
        <label>D (cm) <input v-model.number="tplForm.depthCm" type="number" min="10" max="2000" style="width:70px" /></label>
        <label>Color <input v-model="tplForm.color" type="color" style="width:40px;height:32px;padding:2px;cursor:pointer" /></label>
        <button class="primary" type="submit">+ Add Template</button>
        <span v-if="tplError" class="form-error">{{ tplError }}</span>
      </form>

      <p v-if="templates.length === 0" class="empty-hint">No templates yet.</p>

      <table v-else class="data-table">
        <thead>
          <tr>
            <th>Color</th>
            <th>Description</th>
            <th>Width (cm)</th>
            <th>Depth (cm)</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="t in templates" :key="t.id">
            <template v-if="editingId === t.id">
              <td><input v-model="editRow.color" type="color" style="width:40px;height:28px;padding:2px;cursor:pointer" /></td>
              <td><input v-model="editRow.description" style="width:100%" /></td>
              <td><input v-model.number="editRow.widthCm" type="number" min="10" max="2000" style="width:70px" /></td>
              <td><input v-model.number="editRow.depthCm" type="number" min="10" max="2000" style="width:70px" /></td>
              <td class="actions">
                <button class="primary small" @click="saveEdit(t.id)">Save</button>
                <button class="small" @click="cancelEdit">Cancel</button>
              </td>
            </template>
            <template v-else>
              <td><span class="color-swatch" :style="{ background: t.color }"></span></td>
              <td>{{ t.description }}</td>
              <td>{{ t.widthCm }}</td>
              <td>{{ t.depthCm }}</td>
              <td class="actions">
                <button class="small" @click="startEdit(t)">Edit</button>
                <button class="danger small" @click="removeTemplate(t.id)">Delete</button>
              </td>
            </template>
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

.color-swatch {
  display: inline-block;
  width: 22px;
  height: 22px;
  border-radius: 4px;
  border: 1px solid rgba(0,0,0,0.2);
  vertical-align: middle;
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

.bp-add-form {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.5rem;
  margin-top: 0.75rem;
}

.bp-search-wrap {
  position: relative;
}

.bp-search-input {
  padding: 0.35rem 0.5rem;
  border: 1px solid #ccc;
  border-radius: 4px;
  font-size: 0.9rem;
  min-width: 200px;
}

.bp-dropdown {
  position: absolute;
  top: 100%;
  left: 0;
  z-index: 100;
  background: #fff;
  border: 1px solid #ccc;
  border-radius: 4px;
  margin: 0;
  padding: 0;
  list-style: none;
  min-width: 300px;
  max-height: 200px;
  overflow-y: auto;
  box-shadow: 0 4px 12px rgba(0,0,0,0.15);
}

.bp-dropdown-item {
  padding: 0.35rem 0.6rem;
  font-size: 0.85rem;
  cursor: pointer;
}

.bp-dropdown-item:hover {
  background: #f0f5ff;
}

.bp-selected-badge {
  background: #dde8f5;
  border: 1px solid #aac2e8;
  border-radius: 4px;
  padding: 0.25rem 0.55rem;
  font-size: 0.85rem;
  color: #2a4e80;
}

.bp-add-form label {
  display: flex;
  align-items: center;
  gap: 0.3rem;
  font-size: 0.85rem;
  color: #555;
}

.bp-add-form input[type="number"] {
  padding: 0.35rem 0.4rem;
  border: 1px solid #ccc;
  border-radius: 4px;
  font-size: 0.9rem;
}
</style>
