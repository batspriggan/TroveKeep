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
      </header>

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
const selectedBoxId = ref('')
const allocQty = ref(1)
const editForm = ref({ setNumber: '', description: '', quantity: 1, isMoc: false })

const boxNameMap = computed(() => Object.fromEntries(boxes.value.map(b => [b.id, b.name])))

const unallocated = computed(() => {
  if (!set.value) return 0
  const allocated = (set.value.storageAllocations ?? []).reduce((sum, a) => sum + a.quantity, 0)
  return set.value.quantity - allocated
})

const fullyAllocated = computed(() => unallocated.value === 0)

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
</style>
