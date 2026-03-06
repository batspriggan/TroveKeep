<!-- Redesigned: 2026-03-06 — Two-column desktop layout; mobile order image → storage → edit → delete -->
<template>
  <div class="page">
    <RouterLink class="back-link" to="/sets">← Back to Sets</RouterLink>

    <p v-if="loading" class="loading-msg">Loading…</p>
    <p v-else-if="error" class="error">{{ error }}</p>

    <template v-else-if="set">
      <header class="set-header">
        <span class="set-number">{{ set.setNumber }}</span>
        <span class="set-desc">{{ set.description }}</span>
      </header>

      <div class="detail-layout">
        <!-- Image (top-left on desktop, first on mobile) -->
        <div class="col-image">
          <div v-if="set.imageCached" class="card image-card">
            <img :src="`/api/sets/${id}/image`" class="set-image" alt="" />
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
                <label>Set Number *</label>
                <input v-model="editForm.setNumber" required />
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
import { getSet, updateSet, deleteSet, allocateSetToBox, deallocateSetStorage, clearSetStorage } from '../../api/sets.js'
import { getAllBoxes } from '../../api/boxes.js'
import ConfirmDialog from '../../components/ConfirmDialog.vue'

const route = useRoute()
const router = useRouter()
const id = route.params.id

const set = ref(null)
const boxes = ref([])
const loading = ref(true)
const error = ref('')
const editError = ref('')
const storageError = ref('')
const showConfirm = ref(false)
const selectedBoxId = ref('')
const allocQty = ref(1)
const editForm = ref({ setNumber: '', description: '', photoUrl: '', quantity: 1 })

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
    const [s, b] = await Promise.all([getSet(id), getAllBoxes()])
    set.value = s
    boxes.value = b
    editForm.value = { setNumber: s.setNumber, description: s.description, photoUrl: s.photoUrl ?? '', quantity: s.quantity }
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
      setNumber: editForm.value.setNumber,
      description: editForm.value.description,
      photoUrl: editForm.value.photoUrl || null,
      quantity: editForm.value.quantity,
    })
    set.value = updated
  } catch (e) {
    editError.value = e.message
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
