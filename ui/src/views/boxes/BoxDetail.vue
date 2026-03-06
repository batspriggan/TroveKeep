<!-- Redesigned: 2026-03-06 — Two-column desktop layout; contents tables prominent -->
<template>
  <div class="page">
    <RouterLink class="back-link" to="/boxes">← Back to Boxes</RouterLink>

    <p v-if="loading" class="loading-msg">Loading…</p>
    <p v-else-if="error" class="error">{{ error }}</p>

    <template v-else-if="box">
      <h1 class="box-title">{{ box.name }}</h1>

      <div class="detail-layout">
        <!-- Photo (top-left on desktop, first on mobile) -->
        <div class="col-photo">
          <div class="card photo-card">
            <div class="photo-display">
              <img v-if="box.imageCached" :src="`/api/boxes/${id}/image?t=${imgTs}`" class="box-photo" alt="" />
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
              <button v-if="box.imageCached" class="danger" @click="removePhoto">Remove</button>
            </div>
            <p v-if="photoError" class="error">{{ photoError }}</p>
          </div>

        </div>

        <!-- Contents (right on desktop, middle on mobile) -->
        <div class="col-contents">
          <div class="card">
            <h2>Sets in this Box</h2>
            <table v-if="box.sets?.length" class="contents-table">
              <thead>
                <tr>
                  <th>Set Number</th>
                  <th>Description</th>
                  <th class="th-qty">Qty</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="s in box.sets" :key="s.id">
                  <td class="td-id">
                    <RouterLink :to="`/sets/${s.id}`">{{ s.setNumber }}</RouterLink>
                  </td>
                  <td class="td-desc">{{ s.description }}</td>
                  <td class="td-qty">{{ getAllocQty(s, id) }}</td>
                </tr>
              </tbody>
            </table>
            <p v-else class="empty-msg">No sets stored here.</p>
          </div>

          <div v-if="settings.bulkPiecesEnabled" class="card">
            <h2>Bulk Pieces in this Box</h2>
            <table v-if="box.bulkPieces?.length" class="contents-table">
              <thead>
                <tr>
                  <th>Lego ID</th>
                  <th>Color</th>
                  <th>Description</th>
                  <th class="th-qty">Qty</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="p in box.bulkPieces" :key="p.id">
                  <td class="td-id">
                    <RouterLink :to="`/bulkpieces/${p.id}`">{{ p.legoId }}</RouterLink>
                  </td>
                  <td class="td-color">
                    <span v-if="p.legoColorRgb" class="swatch" :style="{ background: '#' + p.legoColorRgb }"></span>
                    <span>{{ p.legoColorName ?? `#${p.legoColorId}` }}</span>
                  </td>
                  <td class="td-desc">{{ p.description }}</td>
                  <td class="td-qty">{{ getAllocQty(p, id) }}</td>
                </tr>
              </tbody>
            </table>
            <p v-else class="empty-msg">No bulk pieces stored here.</p>
          </div>
        </div>

        <!-- Rename + delete (bottom-left on desktop, last on mobile) -->
        <div class="col-edit">
          <div class="card">
            <h2>Rename</h2>
            <div class="edit-row">
              <input v-model="editForm.name" required placeholder="Box name" />
              <button class="primary" @click="submitEdit">Save</button>
            </div>
            <p v-if="editError" class="error">{{ editError }}</p>
          </div>

          <div class="card danger-zone">
            <button class="danger" @click="showConfirm = true">Delete Box</button>
          </div>
        </div>
      </div>
    </template>

    <ConfirmDialog
      :open="showConfirm"
      :message="`Delete box '${box?.name}'?`"
      @confirm="doDelete"
      @cancel="showConfirm = false"
    />
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getBoxContents, updateBox, deleteBox, uploadBoxImage, deleteBoxImage } from '../../api/boxes.js'
import ConfirmDialog from '../../components/ConfirmDialog.vue'
import { useSettings } from '../../composables/useSettings.js'

const settings = useSettings()

const route = useRoute()
const router = useRouter()
const id = route.params.id

const box = ref(null)
const loading = ref(true)
const error = ref('')
const editError = ref('')
const photoError = ref('')
const showConfirm = ref(false)
const editForm = ref({ name: '' })
const photoFile = ref(null)
const imgTs = ref(Date.now())
const cameraInput = ref(null)

async function load() {
  loading.value = true
  error.value = ''
  try {
    const detail = await getBoxContents(id)
    box.value = detail
    editForm.value = { name: detail.name }
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

async function submitEdit() {
  editError.value = ''
  try {
    const updated = await updateBox(id, { name: editForm.value.name })
    box.value = { ...box.value, ...updated }
  } catch (e) {
    editError.value = e.message
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
    await uploadBoxImage(id, photoFile.value)
    photoFile.value = null
    box.value = { ...box.value, imageCached: true }
    imgTs.value = Date.now()
  } catch (e) {
    photoError.value = e.message
  }
}

async function removePhoto() {
  photoError.value = ''
  try {
    await deleteBoxImage(id)
    box.value = { ...box.value, imageCached: false }
  } catch (e) {
    photoError.value = e.message
  }
}

function getAllocQty(item, boxId) {
  const alloc = (item.storageAllocations ?? []).find(a => a.storageId === boxId)
  return alloc?.quantity ?? '—'
}

async function doDelete() {
  try {
    await deleteBox(id)
    router.push('/boxes')
  } catch (e) {
    error.value = e.message
    showConfirm.value = false
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

.box-title {
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

.box-photo {
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

/* ── Rename card ── */
.edit-row {
  display: flex;
  gap: var(--space-2);
  align-items: center;
}

.edit-row input {
  flex: 1;
}

/* ── Danger zone ── */
.danger-zone {
  border-color: #fee2e2;
}

/* ── Contents tables ── */
.contents-table {
  font-size: var(--text-sm);
}

.contents-table th {
  font-family: var(--font-display);
  font-size: var(--text-xs);
  font-weight: 600;
  color: var(--color-text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.04em;
  background: var(--color-surface-alt);
}

.th-qty { width: 60px; text-align: right; }

.td-id a {
  font-family: var(--font-mono);
  font-size: var(--text-sm);
  font-weight: 500;
  color: var(--color-text-primary);
}

.td-desc {
  color: var(--color-text-secondary);
}

.td-qty {
  font-family: var(--font-mono);
  text-align: right;
  font-weight: 500;
}

.td-color {
  white-space: nowrap;
  font-size: var(--text-sm);
}

.swatch {
  display: inline-block;
  width: 10px;
  height: 10px;
  border-radius: 2px;
  border: 1px solid rgba(0,0,0,0.15);
  vertical-align: middle;
  margin-right: var(--space-1);
  flex-shrink: 0;
}

.empty-msg {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
}
</style>
