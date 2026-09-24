<!-- Redesigned: 2026-03-06 — Two-column desktop layout; mobile order image → storage → edit → delete -->
<template>
  <div class="page">
    <RouterLink class="back-link" to="/sets">← Back to Sets</RouterLink>

    <p v-if="loading" class="loading-msg">Loading…</p>
    <p v-else-if="error" class="error">{{ error }}</p>

    <template v-else-if="set">
      <header class="set-header">
        <span v-if="set.setNumber" class="set-number">{{ set.setNumber }}</span>
        <span v-if="set.isMoc" class="moc-badge">MOC</span>
        <span class="set-desc">{{ set.description }}</span>
        <button class="secondary set-download" :disabled="downloadLoading" @click="downloadLabel">
          {{ downloadLoading ? (config.mode === 'server' ? 'Printing…' : 'Downloading…') : (config.mode === 'server' ? 'Print Label' : 'Download Label') }}
        </button>
      </header>
      <p v-if="downloadMessage" class="download-msg">{{ downloadMessage }}</p>

      <div class="detail-layout">
        <!-- Image (top-left on desktop, first on mobile) -->
        <div class="col-image">
          <!-- Rebrickable cached image -->
          <div v-if="set.imageCached" class="card image-card">
            <img :src="`/api/sets/${id}/image`" class="set-image" alt="" />
          </div>

          <!-- User-uploaded photos -->
          <div class="card photo-card">
            <div class="photo-header">
              <h2>Photos</h2>
            </div>
            <div v-if="photos.length" class="photo-grid">
              <div v-for="p in photos" :key="p.id" class="photo-item">
                <img :src="`/api/sets/${id}/photos/${p.id}`" class="photo-thumb" alt="" />
                <button class="photo-delete" @click="deletePhoto(p.id)" title="Delete photo">✕</button>
              </div>
            </div>
            <p v-else class="no-photos">No photos yet.</p>
            <label class="upload-btn">
              <input
                type="file"
                accept="image/*"
                capture="environment"
                multiple
                class="upload-input"
                @change="onPhotoUpload"
              />
              Add Photo
            </label>
            <p v-if="photoError" class="error photo-error">{{ photoError }}</p>
          </div>
        </div>

        <!-- Storage (right on desktop, middle on mobile) -->
        <div class="col-storage">
          <div class="card">
            <div class="storage-header">
              <h2>Storage</h2>
              <span class="alloc-pill" :class="{ 'alloc-full': fullyAllocated }">
                {{ unallocated }} unallocated of {{ set.quantity }}
              </span>
            </div>

            <table v-if="set.storageAllocations?.length" class="alloc-table">
              <thead>
                <tr>
                  <th>Box</th>
                  <th class="th-qty">Qty</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="a in set.storageAllocations" :key="a.storageId">
                  <td>
                    <RouterLink :to="`/boxes/${a.storageId}`">{{ boxNameMap[a.storageId] ?? a.storageId }}</RouterLink>
                  </td>
                  <td class="td-qty">{{ a.quantity }}</td>
                  <td class="td-action">
                    <button class="btn-remove" @click="deallocate(a.storageId)">Remove</button>
                  </td>
                </tr>
              </tbody>
            </table>
            <p v-else class="no-storage">Not stored anywhere yet.</p>

            <div class="alloc-form-group">
              <p class="alloc-form-label">Assign to Box</p>
              <form class="alloc-row" @submit.prevent="submitStorage">
                <select v-model="selectedBoxId" class="alloc-select">
                  <option value="">— select box —</option>
                  <option v-for="b in boxes" :key="b.id" :value="b.id">{{ b.name }}</option>
                </select>
                <input v-model.number="allocQty" type="number" min="1" required class="alloc-qty" />
                <button class="primary" type="submit" :disabled="!selectedBoxId || allocQty < 1">Assign</button>
              </form>
            </div>

            <div v-if="set.storageAllocations?.length" class="clear-row">
              <button type="button" class="danger" @click="clearStorage">Clear All Storage</button>
            </div>

            <p v-if="storageError" class="error">{{ storageError }}</p>
          </div>

          <!-- Baseplates used (MOC and regular sets) -->
          <div class="card bp-card">
            <div class="bp-header">
              <h2>Baseplates used</h2>
              <span v-if="bpFootprint" class="footprint-pill">
                {{ bpFootprint.cols }}×{{ bpFootprint.rows }}
                <template v-if="!bpFootprint.heterogeneous">
                  = {{ bpFootprint.width }}×{{ bpFootprint.depth }} stud
                </template>
                <template v-else>· mixed sizes</template>
              </span>
            </div>

            <table v-if="bpReservations.length" class="alloc-table bp-table">
              <thead>
                <tr>
                  <th>Baseplate</th>
                  <th>Size (studs)</th>
                  <th class="th-qty">Qty</th>
                  <th class="th-qty">Free</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="r in bpReservations" :key="r.baseplateId">
                  <td>
                    <span class="swatch" :style="{ background: r.legoColorRgb ? '#' + r.legoColorRgb : '#ccc' }"></span>
                    <span>{{ r.plateName }}</span>
                    <span v-if="r.legoColorName" class="bp-color">· {{ r.legoColorName }}</span>
                  </td>
                  <td>{{ r.widthStuds }}×{{ r.depthStuds }}</td>
                  <td class="td-qty">{{ r.quantity }}</td>
                  <td class="td-qty">
                    <span v-if="bpAvailable(r) !== null">{{ bpAvailable(r) }}</span>
                    <span v-else class="muted">—</span>
                  </td>
                  <td class="td-action">
                    <button class="btn-remove" :disabled="bpBusy" @click="removeBpReservation(r)">Remove</button>
                  </td>
                </tr>
              </tbody>
            </table>
            <p v-else class="no-storage">
              No baseplates declared for this {{ set?.isMoc ? 'MOC' : 'set' }}.
            </p>

            <div class="alloc-form-group bp-add-group">
              <p class="alloc-form-label">Add baseplate</p>
              <form class="alloc-row bp-add-row" @submit.prevent="addBpReservation">
                <select v-model="bpSelectedId" class="alloc-select">
                  <option value="">— select baseplate —</option>
                  <option
                    v-for="b in bpCatalog"
                    :key="b.id"
                    :value="b.id"
                  >
                    {{ b.partNum ? b.partNum + ' — ' : '' }}{{ b.name }} ·
                    {{ b.widthStuds }}×{{ b.depthStuds }} · {{ b.availableQuantity ?? 0 }} free
                  </option>
                </select>
                <input
                  v-model.number="bpQty"
                  type="number"
                  min="1"
                  class="alloc-qty"
                />
                <button
                  class="primary"
                  type="submit"
                  :disabled="!bpSelectedId || bpQty < 1 || bpBusy"
                >Add</button>
              </form>
              <p v-if="bpSelectedId" class="bp-hint">
                {{ bpSelectedMax }} free for this type.
              </p>
            </div>

            <p v-if="bpError" class="error">{{ bpError }}</p>
          </div>
        </div>

        <!-- Edit + delete (bottom-left on desktop, last on mobile) -->
        <div class="col-edit">
          <div class="card">
            <h2>Edit</h2>
            <div class="edit-grid">
              <div class="form-field">
                <label>{{ editForm.isMoc ? 'Custom ID' : 'Set Number *' }}</label>
                <input v-model="editForm.setNumber" :required="!editForm.isMoc" :placeholder="editForm.isMoc ? 'e.g. MOC-001 (optional)' : ''" />
              </div>
              <div class="form-field edit-qty-field">
                <label>Qty *</label>
                <input v-model.number="editForm.quantity" type="number" min="1" required />
              </div>
              <div class="form-field edit-desc-field">
                <label>Description *</label>
                <input v-model="editForm.description" required />
              </div>
            </div>
            <button class="primary" @click="submitEdit">Save</button>
            <p v-if="editError" class="error inline-error">{{ editError }}</p>
          </div>

          <div class="card danger-zone">
            <button class="danger" @click="showConfirm = true">Delete Set</button>
          </div>
        </div>
      </div>
    </template>

    <ConfirmDialog
      :open="showConfirm"
      :message="`Delete set ${set?.setNumber}?`"
      @confirm="doDelete"
      @cancel="showConfirm = false"
    />
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getSet, updateSet, deleteSet, allocateSetToBox, deallocateSetStorage, clearSetStorage, getSetPhotos, uploadSetPhoto, deleteSetPhoto } from '../../api/sets.js'
import { getAllBoxes } from '../../api/boxes.js'
import {
  getBaseplatesBySet, getAllBaseplates, addReservation, removeReservation,
} from '../../api/baseplates.js'
import { useLabels } from '../../composables/useLabels.js'
import ConfirmDialog from '../../components/ConfirmDialog.vue'

const route = useRoute()
const router = useRouter()
const id = route.params.id

const set = ref(null)
const boxes = ref([])
const photos = ref([])
const loading = ref(true)
const error = ref('')
const editError = ref('')
const storageError = ref('')
const photoError = ref('')
const showConfirm = ref(false)
const downloadLoading = ref(false)
const downloadMessage = ref('')
const selectedBoxId = ref('')
const allocQty = ref(1)
const editForm = ref({ setNumber: '', description: '', quantity: 1, isMoc: false })

// ── Baseplates used ──────────────────────────────────────────────────────────
const bpReservations = ref([])
const bpCatalog = ref([])
const bpError = ref('')
const bpSelectedId = ref('')
const bpQty = ref(1)
const bpBusy = ref(false)

const boxNameMap = computed(() => Object.fromEntries(boxes.value.map(b => [b.id, b.name])))

const unallocated = computed(() => {
  if (!set.value) return 0
  const allocated = (set.value.storageAllocations ?? []).reduce((sum, a) => sum + a.quantity, 0)
  return set.value.quantity - allocated
})

const fullyAllocated = computed(() => unallocated.value === 0)

// ── Baseplates: derived footprint + availability ─────────────────────────────
const bpCatalogById = computed(() => Object.fromEntries(bpCatalog.value.map(b => [b.id, b])))

const bpSelectedMax = computed(() => {
  const bp = bpCatalogById.value[bpSelectedId.value]
  return bp?.availableQuantity ?? 0
})

// Compact arrangement of the reserved plates: cols = ceil(sqrt(N)). The footprint
// is only exact when all reserved plates share the same type; otherwise we warn.
const bpFootprint = computed(() => {
  const list = bpReservations.value
  const total = list.reduce((sum, r) => sum + (r.quantity ?? 0), 0)
  if (total <= 0) return null
  const first = list[0]
  const homogeneous = list.every(r =>
    r.widthStuds === first.widthStuds && r.depthStuds === first.depthStuds)
  const cols = Math.ceil(Math.sqrt(total))
  const rows = Math.ceil(total / cols)
  return {
    total,
    cols,
    rows,
    width: homogeneous ? cols * first.widthStuds : null,
    depth: homogeneous ? rows * first.depthStuds : null,
    heterogeneous: !homogeneous,
  }
})

function bpAvailable(r) {
  const bp = bpCatalogById.value[r.baseplateId]
  return bp ? (bp.availableQuantity ?? 0) : null
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    const [s, b, p] = await Promise.all([getSet(id), getAllBoxes(), getSetPhotos(id)])
    set.value = s
    boxes.value = b
    photos.value = p
    editForm.value = { setNumber: s.setNumber ?? '', description: s.description, quantity: s.quantity, isMoc: s.isMoc }
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
  loadBaseplateData()
}

async function loadBaseplateData() {
  bpError.value = ''
  try {
    const [reservations, catalog] = await Promise.all([
      getBaseplatesBySet(id),
      getAllBaseplates(),
    ])
    bpReservations.value = reservations ?? []
    // Quarantined rows are not placeable and must not be offered for reservation.
    bpCatalog.value = (catalog ?? []).filter(b => !b.quarantined)
  } catch (e) {
    bpError.value = e.message
  }
}

async function addBpReservation() {
  bpError.value = ''
  const bp = bpCatalogById.value[bpSelectedId.value]
  if (!bp) return
  const qty = Math.max(1, Math.floor(Number(bpQty.value) || 1))
  bpBusy.value = true
  try {
    await addReservation(bp.id, id, qty)
    bpSelectedId.value = ''
    bpQty.value = 1
    await loadBaseplateData()
  } catch (e) {
    bpError.value = e.message
  } finally {
    bpBusy.value = false
  }
}

async function removeBpReservation(r) {
  bpError.value = ''
  bpBusy.value = true
  try {
    await removeReservation(r.baseplateId, id)
    await loadBaseplateData()
  } catch (e) {
    bpError.value = e.message
  } finally {
    bpBusy.value = false
  }
}

async function submitEdit() {
  editError.value = ''
  try {
    const updated = await updateSet(id, {
      setNumber: editForm.value.setNumber || null,
      description: editForm.value.description,
      quantity: editForm.value.quantity,
      isMoc: editForm.value.isMoc,
    })
    set.value = updated
  } catch (e) {
    editError.value = e.message
  }
}

async function onPhotoUpload(event) {
  photoError.value = ''
  const files = Array.from(event.target.files ?? [])
  event.target.value = ''
  for (const file of files) {
    try {
      const photo = await uploadSetPhoto(id, file)
      photos.value.push(photo)
    } catch (e) {
      photoError.value = e.message
    }
  }
}

async function deletePhoto(photoId) {
  photoError.value = ''
  try {
    await deleteSetPhoto(id, photoId)
    photos.value = photos.value.filter(p => p.id !== photoId)
  } catch (e) {
    photoError.value = e.message
  }
}

async function submitStorage() {
  storageError.value = ''
  try {
    const updated = await allocateSetToBox(id, selectedBoxId.value, allocQty.value)
    set.value = updated
    selectedBoxId.value = ''
    allocQty.value = 1
  } catch (e) {
    storageError.value = e.message
  }
}

async function deallocate(storageId) {
  storageError.value = ''
  try {
    const updated = await deallocateSetStorage(id, storageId)
    set.value = updated
  } catch (e) {
    storageError.value = e.message
  }
}

async function clearStorage() {
  storageError.value = ''
  try {
    const updated = await clearSetStorage(id)
    set.value = updated
  } catch (e) {
    storageError.value = e.message
  }
}

async function doDelete() {
  try {
    await deleteSet(id)
    router.push('/sets')
  } catch (e) {
    error.value = e.message
    showConfirm.value = false
  }
}

async function downloadLabel() {
  downloadLoading.value = true
  downloadMessage.value = ''
  try {
    const { message, error } = await printOrDownload('set', id, { size: null })
    downloadMessage.value = error ? `${message} ${error}` : message
  } catch (e) {
    downloadMessage.value = e.message
  } finally {
    downloadLoading.value = false
  }
}

const { config, ensureConfig, printOrDownload } = useLabels()
onMounted(() => {
  ensureConfig()
  load()
})
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

/* ── Header ── */
.set-header {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: var(--space-3);
  margin-bottom: var(--space-4);
}

.set-number {
  font-family: var(--font-mono);
  font-size: var(--text-2xl);
  font-weight: 500;
  color: var(--color-text-primary);
}

.moc-badge {
  display: inline-block;
  font-size: 0.7rem;
  font-weight: 700;
  letter-spacing: 0.04em;
  background: #dbeafe;
  color: #1d4ed8;
  border: 1px solid #93c5fd;
  border-radius: 4px;
  padding: 2px 6px;
  vertical-align: middle;
}

.set-desc {
  font-size: var(--text-lg);
  color: var(--color-text-secondary);
}

.set-download {
  margin-left: auto;
}

.download-msg {
  font-size: var(--text-sm);
  color: var(--color-text-muted);
  margin: var(--space-2) 0 var(--space-4);
}

/* ── Layout ── */
.detail-layout {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}

.col-image   { order: 1; }
.col-storage { order: 2; }
.col-edit    { order: 3; display: flex; flex-direction: column; gap: var(--space-4); }

@media (min-width: 640px) {
  .detail-layout {
    display: grid;
    grid-template-columns: 280px 1fr;
    grid-template-rows: auto 1fr;
    grid-template-areas:
      "image   storage"
      "edit    storage";
    align-items: start;
  }

  .col-image   { grid-area: image; }
  .col-storage { grid-area: storage; }
  .col-edit    { grid-area: edit; }
}

/* ── Image card ── */
.image-card {
  display: flex;
  justify-content: center;
  padding: var(--space-4);
}

.set-image {
  width: 100%;
  max-height: 220px;
  object-fit: contain;
}

/* ── Photo card ── */
.photo-card { margin-top: var(--space-4); }

.photo-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: var(--space-3);
}

.photo-header h2 { margin: 0; }

.photo-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(80px, 1fr));
  gap: var(--space-2);
  margin-bottom: var(--space-3);
}

.photo-item {
  position: relative;
}

.photo-thumb {
  width: 100%;
  aspect-ratio: 1;
  object-fit: cover;
  border-radius: 4px;
  border: 1px solid var(--color-border);
  display: block;
}

.photo-delete {
  position: absolute;
  top: 2px;
  right: 2px;
  width: 20px;
  height: 20px;
  border-radius: 50%;
  border: none;
  background: rgba(0,0,0,0.5);
  color: #fff;
  font-size: 0.6rem;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 0;
  line-height: 1;
}

.photo-delete:hover { background: #b91c1c; }

.no-photos {
  font-size: var(--text-sm);
  color: var(--color-text-muted);
  margin-bottom: var(--space-3);
}

.upload-btn {
  display: inline-block;
  cursor: pointer;
  font-size: var(--text-sm);
  font-weight: 500;
  padding: 0.4rem var(--space-3);
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-surface);
  color: var(--color-text-secondary);
  transition: background var(--transition-fast), border-color var(--transition-fast);
}

.upload-btn:hover {
  background: var(--color-surface-alt);
  border-color: var(--color-text-muted);
}

.upload-input {
  display: none;
}

.photo-error { margin-top: var(--space-2); }

/* ── Storage card ── */
.storage-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: var(--space-3);
}

.storage-header h2 { margin: 0; }

.alloc-pill {
  font-size: var(--text-xs);
  font-family: var(--font-mono);
  background: var(--color-surface-alt);
  border: 1px solid var(--color-border);
  border-radius: 20px;
  padding: 2px var(--space-3);
  color: var(--color-text-secondary);
}

.alloc-pill.alloc-full {
  background: var(--color-accent-soft);
  border-color: #fca5a5;
  color: var(--color-accent);
}

.alloc-table {
  font-size: var(--text-sm);
  margin-bottom: var(--space-4);
}

.alloc-table th {
  font-size: var(--text-xs);
  font-weight: 600;
  color: var(--color-text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.04em;
  background: var(--color-surface-alt);
}

.th-qty { width: 60px; text-align: right; }

.td-qty {
  font-family: var(--font-mono);
  text-align: right;
  font-weight: 500;
}

.td-action { text-align: right; }

.btn-remove {
  font-size: var(--text-xs);
  padding: 0.2rem 0.5rem;
  background: transparent;
  border: 1px solid var(--color-border);
  color: var(--color-text-secondary);
  border-radius: 4px;
  cursor: pointer;
  transition: background var(--transition-fast), color var(--transition-fast);
}

.btn-remove:hover {
  background: #fee2e2;
  border-color: #fca5a5;
  color: #b91c1c;
}

.no-storage {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
  margin-bottom: var(--space-4);
}

.alloc-form-group {
  margin-bottom: var(--space-3);
}

.alloc-form-label {
  font-size: var(--text-xs);
  font-weight: 600;
  color: var(--color-text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.04em;
  margin-bottom: var(--space-2);
}

.alloc-row {
  display: flex;
  gap: var(--space-2);
  align-items: center;
}

.alloc-select {
  flex: 1;
  font-size: var(--text-sm);
  min-width: 0;
}

.alloc-qty {
  width: 64px;
  flex-shrink: 0;
  font-family: var(--font-mono);
  font-size: var(--text-sm);
  text-align: right;
}

.clear-row {
  padding-top: var(--space-3);
  border-top: 1px solid var(--color-border);
}

/* ── Edit card ── */
.edit-grid {
  display: grid;
  grid-template-columns: 1fr 56px;
  gap: var(--space-3);
  margin-bottom: var(--space-3);
}

.edit-qty-field input {
  max-width: 56px;
  min-width: 0;
}

.edit-desc-field {
  grid-column: 1 / -1;
}

.edit-grid .form-field label {
  display: block;
  font-size: var(--text-sm);
  font-weight: 500;
  color: var(--color-text-secondary);
  margin-bottom: var(--space-1);
}

.inline-error { margin: var(--space-2) 0 0; }

/* ── Danger zone ── */
.danger-zone { border-color: #fee2e2; }

/* ── Baseplates used ── */
.bp-card { margin-top: var(--space-4); }

.bp-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-2);
  flex-wrap: wrap;
  margin-bottom: var(--space-3);
}

.bp-header h2 { margin: 0; }

.footprint-pill {
  font-size: var(--text-xs);
  font-family: var(--font-mono);
  background: var(--color-surface-alt);
  border: 1px solid var(--color-border);
  border-radius: 20px;
  padding: 2px var(--space-3);
  color: var(--color-text-secondary);
  white-space: nowrap;
}

.bp-table { margin-bottom: 0; }

.bp-color {
  color: var(--color-text-muted);
  font-size: var(--text-xs);
}

.swatch {
  display: inline-block;
  width: 0.9rem;
  height: 0.9rem;
  border-radius: 2px;
  border: 1px solid rgba(0, 0, 0, 0.15);
  vertical-align: middle;
  margin-right: 0.35rem;
}

.muted { color: var(--color-text-muted); }

.bp-add-group { margin-top: var(--space-3); }

.bp-add-row { flex-wrap: wrap; }

.bp-hint {
  font-size: var(--text-xs);
  color: var(--color-text-muted);
  margin: var(--space-1) 0 0;
}
</style>
