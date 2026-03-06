<!-- Redesigned: 2026-03-06 — Two-column desktop layout; mobile order photo → drawers → edit → delete -->
<template>
  <div class="page">
    <RouterLink class="back-link" to="/drawercontainers">← Back to Drawer Containers</RouterLink>

    <p v-if="loading" class="loading-msg">Loading…</p>
    <p v-else-if="error" class="error">{{ error }}</p>

    <template v-else-if="container">
      <h1 class="container-title">{{ container.name }}</h1>
      <p v-if="container.description" class="container-desc">{{ container.description }}</p>

      <div class="detail-layout">
        <!-- Photo (top-left on desktop, first on mobile) -->
        <div class="col-photo">
          <div class="card photo-card">
            <div class="photo-display">
              <img v-if="container.imageCached" :src="`/api/drawercontainers/${id}/image?t=${imgTs}`" class="container-photo" alt="" />
              <div v-else class="photo-placeholder">No photo</div>
            </div>
            <div class="photo-actions">
              <label class="file-label">
                <input type="file" accept="image/*" class="file-input" @change="onFileChange" />
                <span>Choose file</span>
              </label>
              <button type="button" class="btn-secondary mobile-only" @click="cameraInput.click()">Take Photo</button>
              <input ref="cameraInput" type="file" accept="image/*" capture="environment" style="display:none" @change="onCameraCapture" />
              <button class="primary" :disabled="!photoFile" @click="uploadPhoto">Upload</button>
              <button v-if="container.imageCached" class="danger" @click="removePhoto">Remove</button>
            </div>
            <p v-if="photoError" class="error">{{ photoError }}</p>
          </div>
        </div>

        <!-- Drawers (right on desktop, middle on mobile) -->
        <div class="col-contents">
          <div class="card">
            <h2>Drawers</h2>

            <table v-if="drawers.length" class="drawers-table">
              <thead>
                <tr>
                  <th class="th-pos">Position</th>
                  <th>Contents</th>
                  <th v-if="settings.bulkPiecesEnabled" class="th-count">Bulk Pieces</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="d in drawers" :key="d.position">
                  <td class="td-pos">{{ d.position }}</td>
                  <td>
                    <RouterLink :to="`/drawers/${id}/${d.position}`" class="drawer-link">
                      {{ d.contentSummary?.join(', ') || '(empty)' }}
                    </RouterLink>
                  </td>
                  <td v-if="settings.bulkPiecesEnabled" class="td-count">{{ d.bulkPieceCount }}</td>
                  <td class="td-action">
                    <button class="btn-delete" @click="confirmDeleteDrawer(d)">Delete</button>
                  </td>
                </tr>
              </tbody>
            </table>
            <p v-else class="empty-msg">No drawers yet.</p>

            <div class="add-drawer">
              <p class="add-drawer-label">Add Drawer</p>
              <form class="add-drawer-row" @submit.prevent="submitDrawer">
                <label class="pos-label">
                  Position
                  <input v-model.number="drawerForm.position" type="number" min="1" required class="pos-input" />
                </label>
                <button class="primary" type="submit">Add Drawer</button>
              </form>
              <p v-if="drawerError" class="error">{{ drawerError }}</p>
            </div>
          </div>
        </div>

        <!-- Edit + delete (bottom-left on desktop, last on mobile) -->
        <div class="col-edit">
          <div class="card">
            <h2>Edit</h2>
            <div class="edit-grid">
              <div class="form-field">
                <label>Name *</label>
                <input v-model="editForm.name" required />
              </div>
              <div class="form-field">
                <label>Description</label>
                <input v-model="editForm.description" placeholder="Optional" />
              </div>
            </div>
            <button class="primary" @click="submitEdit">Save</button>
            <p v-if="editError" class="error">{{ editError }}</p>
          </div>

          <div class="card danger-zone">
            <button class="danger" @click="showConfirm = true">Delete Container</button>
          </div>
        </div>
      </div>
    </template>

    <ConfirmDialog
      :open="showConfirm"
      :message="`Delete container '${container?.name}'?`"
      @confirm="doDelete"
      @cancel="showConfirm = false"
    />

    <ConfirmDialog
      :open="!!deleteDrawerTarget"
      :message="`Delete drawer at position ${deleteDrawerTarget?.position}?`"
      @confirm="doDeleteDrawer"
      @cancel="deleteDrawerTarget = null"
    />
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  getDrawerContainer, updateDrawerContainer, deleteDrawerContainer,
  getDrawerContainerDrawers, addDrawer, uploadContainerImage, deleteContainerImage,
} from '../../api/drawercontainers.js'
import { deleteDrawer } from '../../api/drawers.js'
import ConfirmDialog from '../../components/ConfirmDialog.vue'
import { useSettings } from '../../composables/useSettings.js'

const settings = useSettings()

const route = useRoute()
const router = useRouter()
const id = route.params.id

const container = ref(null)
const drawers = ref([])
const loading = ref(true)
const error = ref('')
const editError = ref('')
const drawerError = ref('')
const showConfirm = ref(false)
const deleteDrawerTarget = ref(null)
const editForm = ref({ name: '', description: '' })
const drawerForm = ref({ position: 1 })
const photoFile = ref(null)
const photoError = ref('')
const imgTs = ref(Date.now())
const cameraInput = ref(null)

function nextPosition(drawerList) {
  if (!drawerList.length) return 1
  return Math.max(...drawerList.map(d => d.position)) + 1
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    const [c, d] = await Promise.all([
      getDrawerContainer(id),
      getDrawerContainerDrawers(id),
    ])
    container.value = c
    drawers.value = d.drawers
    drawerForm.value = { position: nextPosition(d.drawers) }
    editForm.value = { name: c.name, description: c.description ?? '' }
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

async function submitEdit() {
  editError.value = ''
  try {
    const updated = await updateDrawerContainer(id, {
      name: editForm.value.name,
      description: editForm.value.description || null,
    })
    container.value = { ...container.value, ...updated }
  } catch (e) {
    editError.value = e.message
  }
}

async function submitDrawer() {
  drawerError.value = ''
  try {
    await addDrawer(id, { position: drawerForm.value.position, label: null })
    drawers.value = (await getDrawerContainerDrawers(id)).drawers
    drawerForm.value = { position: nextPosition(drawers.value) }
  } catch (e) {
    drawerError.value = e.message
  }
}

function confirmDeleteDrawer(d) {
  deleteDrawerTarget.value = d
}

async function doDeleteDrawer() {
  error.value = ''
  try {
    await deleteDrawer(id, deleteDrawerTarget.value.position)
    deleteDrawerTarget.value = null
    drawers.value = (await getDrawerContainerDrawers(id)).drawers
  } catch (e) {
    error.value = e.message
    deleteDrawerTarget.value = null
  }
}

async function doDelete() {
  try {
    await deleteDrawerContainer(id)
    router.push('/drawercontainers')
  } catch (e) {
    error.value = e.message
    showConfirm.value = false
  }
}

function onFileChange(e) {
  photoFile.value = e.target.files[0] ?? null
}

async function onCameraCapture(e) {
  const file = e.target.files[0]
  if (!file) return
  photoFile.value = file
  await uploadPhoto()
}

async function uploadPhoto() {
  photoError.value = ''
  try {
    await uploadContainerImage(id, photoFile.value)
    photoFile.value = null
    container.value = { ...container.value, imageCached: true }
    imgTs.value = Date.now()
  } catch (e) {
    photoError.value = e.message
  }
}

async function removePhoto() {
  photoError.value = ''
  try {
    await deleteContainerImage(id)
    container.value = { ...container.value, imageCached: false }
  } catch (e) {
    photoError.value = e.message
  }
}

onMounted(load)
</script>

<style scoped>
.page {
  font-family: var(--font-body);
}

.loading-msg {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
  margin: var(--space-4) 0;
}

.container-title {
  margin-bottom: var(--space-1);
}

.container-desc {
  color: var(--color-text-secondary);
  font-size: var(--text-sm);
  margin-bottom: var(--space-4);
}

/* ── Layout ── */
.detail-layout {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}

.col-photo    { order: 1; }
.col-contents { order: 2; }
.col-edit     { order: 3; display: flex; flex-direction: column; gap: var(--space-4); }

@media (min-width: 640px) {
  .detail-layout {
    display: grid;
    grid-template-columns: 280px 1fr;
    grid-template-rows: auto 1fr;
    grid-template-areas:
      "photo    contents"
      "edit     contents";
    align-items: start;
  }

  .col-photo    { grid-area: photo; }
  .col-contents { grid-area: contents; }
  .col-edit     { grid-area: edit; }
}

/* ── Photo card ── */
.photo-display {
  margin-bottom: var(--space-3);
}

.container-photo {
  width: 100%;
  max-height: 240px;
  object-fit: contain;
  border-radius: 4px;
  display: block;
}

.photo-placeholder {
  height: 120px;
  background: var(--color-surface-alt);
  border: 1px dashed var(--color-border);
  border-radius: 6px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--color-text-muted);
  font-size: var(--text-sm);
}

.photo-actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
  align-items: center;
}

.file-label {
  position: relative;
  cursor: pointer;
}

.file-label span {
  display: inline-block;
  padding: 0.35rem 0.8rem;
  border: 1px solid var(--color-border);
  border-radius: 4px;
  font-size: var(--text-sm);
  background: var(--color-surface);
  color: var(--color-text-primary);
  cursor: pointer;
  transition: background var(--transition-fast);
}

.file-label span:hover {
  background: var(--color-surface-alt);
}

.file-input {
  position: absolute;
  inset: 0;
  opacity: 0;
  cursor: pointer;
  width: 100%;
  padding: 0;
  border: none;
}

.btn-secondary {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  color: var(--color-text-primary);
}

.mobile-only {
  display: none;
}

@media (max-width: 640px) {
  .mobile-only {
    display: inline-flex;
  }
}

/* ── Drawers table ── */
.drawers-table {
  font-size: var(--text-sm);
  margin-bottom: var(--space-4);
}

.drawers-table th {
  font-family: var(--font-display);
  font-size: var(--text-xs);
  font-weight: 600;
  color: var(--color-text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.04em;
  background: var(--color-surface-alt);
}

.th-pos   { width: 80px; }
.th-count { width: 90px; text-align: right; }

.td-pos {
  font-family: var(--font-mono);
  font-weight: 500;
}

.drawer-link {
  color: var(--color-text-primary);
  font-size: var(--text-sm);
}

.td-count {
  font-family: var(--font-mono);
  text-align: right;
  color: var(--color-text-secondary);
}

.td-action {
  text-align: right;
}

.btn-delete {
  font-size: var(--text-xs);
  padding: 0.2rem 0.55rem;
  background: transparent;
  border: 1px solid var(--color-border);
  color: var(--color-text-secondary);
  border-radius: 4px;
  cursor: pointer;
  transition: background var(--transition-fast), color var(--transition-fast);
}

.btn-delete:hover {
  background: #fee2e2;
  border-color: #fca5a5;
  color: #b91c1c;
}

.empty-msg {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
  margin-bottom: var(--space-4);
}

/* ── Add drawer ── */
.add-drawer {
  border-top: 1px solid var(--color-border);
  padding-top: var(--space-3);
}

.add-drawer-label {
  font-size: var(--text-xs);
  font-weight: 600;
  color: var(--color-text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.04em;
  margin-bottom: var(--space-2);
}

.add-drawer-row {
  display: flex;
  align-items: flex-end;
  gap: var(--space-3);
}

.pos-label {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
  font-size: var(--text-sm);
  color: var(--color-text-secondary);
}

.pos-input {
  width: 80px;
  font-family: var(--font-mono);
}

/* ── Edit card ── */
.edit-grid {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  margin-bottom: var(--space-3);
}

.edit-grid .form-field label {
  display: block;
  font-size: var(--text-sm);
  font-weight: 500;
  color: var(--color-text-secondary);
  margin-bottom: var(--space-1);
}

/* ── Danger zone ── */
.danger-zone {
  border-color: #fee2e2;
}
</style>
